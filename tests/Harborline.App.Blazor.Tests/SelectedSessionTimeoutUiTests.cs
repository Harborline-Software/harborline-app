using Bunit;
using Harborline.App.Blazor.ReferenceHost;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.App.Blazor.ReferenceHost.Navigation;
using Harborline.App.Blazor.ReferenceHost.Transport;
using Harborline.App.Blazor.ReferenceHost.Workshop;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Harborline.App.Blazor.Tests;

public sealed class SelectedSessionTimeoutUiTests : BunitContext
{
    [Fact]
    public async Task Mounted_shell_exposes_import_timeout_and_recovers_only_after_explicit_retry()
    {
        var browser = new TimeoutBrowser();
        await using var transport = new BrowserSelectedSessionTransport(browser);
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver, Media>();
        Services.AddSingleton<IAuthorizationAdminClient, FixtureAuthorizationAdminClient>();
        Services.AddSingleton<IPackNavigationClient>(new BrowserPackNavigationClient(transport));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<Shell>();
        browser.FirstImport.SetCanceled();
        cut.WaitForAssertion(() => Assert.Contains("Unable to load application navigation", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Empty(browser.Requests);
        Assert.Equal(1, browser.Imports);
        cut.Find("[role=alert] button").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role=alert]")));
        Assert.Equal(2, browser.Imports);
        Assert.Equal("/api/local-node/navigation/workspaces", Assert.Single(browser.Requests));
        Assert.Equal("Assets", cut.Find("main h1").TextContent);
    }

    [Fact]
    public async Task Mounted_catalogue_exposes_import_timeout_and_recovers_only_after_explicit_retry()
    {
        var browser = new TimeoutBrowser();
        await using var transport = new BrowserSelectedSessionTransport(browser);
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IWorkshopCatalogueClient>(new BrowserWorkshopCatalogueClient(transport));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms"));
        browser.FirstImport.SetCanceled();
        cut.WaitForAssertion(() => Assert.Contains("The selected-session request could not complete", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Empty(browser.Requests);
        Assert.Equal(1, browser.Imports);
        Assert.Empty(cut.FindAll("[role=grid],form"));
        cut.Find("[role=alert] button").Click();
        cut.WaitForAssertion(() => Assert.Contains("No definitions.", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("[role=alert]"));
        Assert.Equal(2, browser.Imports);
        Assert.Equal(2, browser.Requests.Count);
    }

    private sealed class TimeoutBrowser : IJSRuntime, IJSObjectReference
    {
        public TaskCompletionSource<IJSObjectReference> FirstImport { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Imports { get; private set; }
        public List<string> Requests { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            try { return InvokeAsync<TValue>(identifier, CancellationToken.None, args); }
            catch (JSException) { throw; }
        }
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import")
            {
                Imports++;
                return (TValue)(object)(Imports == 1 ? await FirstImport.Task : this);
            }
            Assert.Equal("sendForCaller", identifier);
            Assert.True(Assert.IsType<DotNetObjectReference<SelectedSessionDispatchGuard>>(args![0]).Value.CanDispatch());
            var path = Assert.IsType<string>(args[1]);
            Assert.Equal("GET", args[2]);
            Requests.Add(path);
            var body = path switch
            {
                "/api/local-node/navigation/workspaces" => """{"configured":false,"pack":null}""",
                "/api/local-node/catalogue/definitions?kind=FormDefinition" => """{"entries":[],"kindsUnavailable":[]}""",
                _ => """{"id":"platform.list.forms","version":"1.0.0","status":"Published","renderPlan":{"definitionHash":"hash","definitionId":"platform.list.forms","definitionVersion":"1.0.0","packKey":"harborline.platform","packVersion":"1.0.0","definitionKind":"ViewDefinition","bindings":{"viewKind":"views.entity-list/grid","parameters":{"fields":[{"id":"formId","label":"Key"}]}}}}""",
            };
            return (TValue)(object)new SelectedSessionResponse(200, body, null);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Media : IMediaQueryObserver
    {
        public ValueTask<IMediaQuerySubscription> ObserveAsync(string query, Func<MediaQueryChange, ValueTask> changed,
            CancellationToken cancellationToken = default) => new(new Subscription(query));
        private sealed class Subscription(string query) : IMediaQuerySubscription
        {
            public string Query => query;
            public bool Matches => true;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
