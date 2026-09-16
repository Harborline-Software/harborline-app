using System.Net;
using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.App.Blazor.ReferenceHost.Navigation;
using Harborline.App.Blazor.ReferenceHost.Runtime;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Harborline.App.Blazor.Tests;

public sealed class AccessHoldersTests : BunitContext
{
    [Theory]
    [InlineData("access.holders")]
    [InlineData("example.dynamic-view")]
    public void Declared_pack_item_uses_generic_browser_host_and_shared_rows_without_legacy_holder_read(string viewId)
    {
        var handler = new NavigationHandler(viewId);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://node.example") };
        var browser = new Browser(viewId);
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver, Media>();
        Services.AddSingleton<IJSRuntime>(browser);
        Services.AddSingleton<IAuthorizationAdminClient>(new HttpAuthorizationAdminClient(http));
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(http));
        var component = Render<Shell>();
        component.WaitForAssertion(() => Assert.Contains("Access", component.Markup, StringComparison.Ordinal));
        component.Find("a[href='/workspaces/access']").Click();
        component.FindAll("a").Single(link => link.TextContent.Trim() == "Access").Click();
        component.Find($"a[href='/workspaces/{viewId}']").Click();
        component.WaitForAssertion(() => Assert.Contains("/records/one", component.Markup, StringComparison.Ordinal));
        Assert.Equal(viewId, component.FindComponent<PackActionHost>().Instance.ViewId);
        Assert.Contains(viewId, browser.Loaded);
        Assert.DoesNotContain(handler.Paths, path => path.EndsWith("/authorization/holders", StringComparison.Ordinal));
        Assert.Empty(component.FindComponents<Harborline.App.Blazor.Tests.Fixtures.LegacyAccessHoldersPage>());
    }

    private sealed class NavigationHandler(string viewId) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath; Paths.Add(path);
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "Harborline.App.slnx"))) root = root.Parent;
            var body = path.EndsWith("/navigation/workspaces", StringComparison.Ordinal)
                ? File.ReadAllText(Path.Combine(root!.FullName, "tests/fixtures/access-navigation.json")).Replace("access.holders", viewId, StringComparison.Ordinal)
                : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private sealed class Browser(string viewId) : IJSRuntime, IJSObjectReference
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        public List<string> Loaded { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            try { return InvokeAsync<TValue>(identifier, CancellationToken.None, args); }
            catch (JSException) { throw; }
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier is "import" or "createPackActionRuntime") return ValueTask.FromResult((TValue)(object)this);
            if (identifier != "load") return ValueTask.FromResult(default(TValue)!);
            Loaded.Add((string)args![0]!);
            var snapshot = JsonSerializer.Deserialize<PackActionSnapshot>("""
                {"plan":{"definitionHash":"hash","definitionId":"example","definitionVersion":"1.0.0","definitionKind":"ViewDefinition",
                 "packKey":"example.pack","packVersion":"1.0.0","bindings":{"viewKind":"views.entity-list/grid",
                 "parameters":{"entityType":"Example","fields":[{"id":"scope","label":"Scope"}]},"actions":[]}},
                 "rows":[{"id":"row-one","values":{"scope":"/records/one"}}],"selectedId":null,
                 "activeAction":null,"inputPlan":null,"receipt":null,"error":null,"busy":false}
                """, JsonOptions)!;
            Assert.Equal(viewId, args[0]);
            return ValueTask.FromResult((TValue)(object)snapshot);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Media : IMediaQueryObserver
    {
        public ValueTask<IMediaQuerySubscription> ObserveAsync(string query, Func<MediaQueryChange, ValueTask> changed, CancellationToken cancellationToken = default) => new(new Subscription(query));
        private sealed class Subscription(string query) : IMediaQuerySubscription
        {
            public string Query => query;
            public bool Matches => !query.Contains("max-width", StringComparison.Ordinal);
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

public sealed class LiveHostFactAttribute : FactAttribute
{
    public LiveHostFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HARBORLINE_LIVE_API_ORIGIN"))) Skip = "Set HARBORLINE_LIVE_API_ORIGIN and HARBORLINE_LIVE_API_TOKEN for the explicit live-host proof.";
    }
}
