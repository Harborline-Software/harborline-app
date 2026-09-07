using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.App.Blazor.ReferenceHost.Navigation;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;
using Harborline.UIAdapters.Blazor.Components.Layout;
using Microsoft.Extensions.DependencyInjection;

namespace Harborline.App.Blazor.Tests;

public sealed class PackNavigationTests : BunitContext
{
    [Fact]
    public void Shared_declaration_renders_Access_and_panels_removal_removes_them_and_a_refused_read_retries()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Harborline.App.slnx"))) root = root.Parent;
        var fixture = File.ReadAllText(Path.Combine(root!.FullName, "tests/fixtures/access-navigation.json"));
        var response = JsonSerializer.Deserialize<PackNavigationResponse>(fixture, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var handler = new Handler(fixture);
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7308/") }));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var first = Render<Shell>();
        first.WaitForAssertion(() => Assert.Contains("Access", first.Markup, StringComparison.Ordinal));
        Assert.Equal("/api/local-node/navigation/workspaces", handler.LastPath);
        Assert.Equal("access", Assert.Single(first.FindComponent<HarborlineAppShell>().Instance.Navigation.SeedWorkspaces).Id);
        Assert.Equal("access-details", Assert.Single(first.FindComponent<HarborlineAppShell>().Instance.Navigation.PanelSet!).Id);
        first.Dispose();

        handler.Json = JsonSerializer.Serialize(response with { Pack = response.Pack! with { SeedWorkspaces = [], PanelSet = [] } });
        var removed = Render<Shell>();
        removed.WaitForAssertion(() => Assert.Empty(removed.FindComponent<HarborlineAppShell>().Instance.Navigation.SeedWorkspaces));
        Assert.DoesNotContain("Access", removed.Markup, StringComparison.Ordinal);
        Assert.Empty(removed.FindComponent<HarborlineAppShell>().Instance.Navigation.PanelSet!);
        removed.Dispose();

        handler.Status = HttpStatusCode.Forbidden;
        var refused = Render<Shell>();
        refused.WaitForAssertion(() => Assert.Contains("Unable to load application navigation", refused.Find("[role='alert']").TextContent, StringComparison.Ordinal));
        handler.Status = HttpStatusCode.OK;
        handler.Json = fixture;
        refused.Find("[role='alert'] button").Click();
        refused.WaitForAssertion(() => Assert.Contains("Access", refused.Markup, StringComparison.Ordinal));
        Assert.Empty(refused.FindAll("[role='alert']"));
    }

    private sealed class Handler(string json) : HttpMessageHandler
    {
        public string Json = json;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string? LastPath;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Json, Encoding.UTF8, "application/json") });
        }
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
