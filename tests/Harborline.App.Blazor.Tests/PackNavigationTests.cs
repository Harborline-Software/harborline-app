using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.App.Blazor.ReferenceHost.Navigation;
using Harborline.App.Blazor.ReferenceHost.Workshop;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;
using Harborline.UIAdapters.Blazor.Components.DataDisplay;
using Harborline.UIAdapters.Blazor.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Harborline.App.Blazor.Tests;

public sealed class PackNavigationTests : BunitContext
{
    private const string WorkshopFixture = """
        {"configured":true,"pack":{"seedWorkspaces":[{"id":"workshop","labelKey":"workshop.workspace","groups":[{"id":"workshop-definitions","labelKey":"workshop.definitions","itemIds":["forms"]}]}],"panelSet":[{"id":"inspector","labelKey":"Inspector","binding":"panels.inspector.toggle","shortcut":"mod+shift+i","defaultWidth":400,"minimumHeight":300,"defaultOpen":false,"traits":["Scoped"]}]}}
        """;

    [Fact]
    public void Addressed_workshop_forms_renders_its_seeded_grid_and_lifts_a_row_into_the_declared_inspector_panel()
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        Services.AddSingleton<IWorkshopCatalogueClient>(new FormsCatalogueClient());
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(
            new HttpClient(new Handler(WorkshopFixture)) { BaseAddress = new Uri("http://localhost:7308/") }));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var shell = Render<Shell>();

        shell.WaitForAssertion(() => shell.Find("a[href='/workspaces/workshop']").Click());
        shell.WaitForAssertion(() => shell.Find("a[href='/workspaces/forms']").Click());
        shell.WaitForAssertion(() =>
        {
            Assert.Equal("Forms", shell.Find("main h1").TextContent.Trim());
            Assert.Equal(["Key", "Title", "Version", "Cascade layer"], shell.FindAll("[role=columnheader]").Select(cell => cell.TextContent.Trim()));
            Assert.DoesNotContain("Browse and manage the physical assets", shell.Markup, StringComparison.Ordinal);
        });

        shell.Find("[data-row-id='inspection@1.0.0']").DoubleClick();
        shell.WaitForAssertion(() =>
        {
            var inspector = shell.Find("[data-shell-panel-id='inspector']");
            Assert.Contains("Inspection", inspector.TextContent, StringComparison.Ordinal);
            Assert.Contains("Pack", inspector.TextContent, StringComparison.Ordinal);
            Assert.Empty(shell.FindAll("main aside[aria-label='Definition inspector']"));
            Assert.EndsWith("?item=forms&selected=inspection%401.0.0&panels=inspector", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Explicit_address_restores_the_declared_workshop_item_selection_and_inspector()
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        Services.AddSingleton<IWorkshopCatalogueClient>(new FormsCatalogueClient());
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(
            new HttpClient(new Handler(WorkshopFixture)) { BaseAddress = new Uri("http://localhost:7308/") }));
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/?item=forms&selected=inspection%401.0.0&panels=inspector");

        var shell = Render<Shell>();

        shell.WaitForAssertion(() =>
        {
            Assert.Equal("Forms", shell.Find("main h1").TextContent.Trim());
            Assert.Contains("Inspection", shell.Find("[data-shell-panel-id='inspector']").TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Unaddressed_session_preserves_the_legacy_assets_body()
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(
            new HttpClient(new Handler(WorkshopFixture)) { BaseAddress = new Uri("http://localhost:7308/") }));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var shell = Render<Shell>();

        shell.WaitForAssertion(() =>
        {
            Assert.Equal("Assets", shell.Find("main h1").TextContent.Trim());
            Assert.Contains("Browse and manage the physical assets", shell.Markup, StringComparison.Ordinal);
            Assert.Equal("http://localhost/", Services.GetRequiredService<NavigationManager>().Uri);
        });
    }

    [Fact]
    public void Seeded_workshop_workspace_has_a_user_facing_label()
    {
        const string fixture = """
            {"configured":true,"pack":{"packId":"harborline.active-pack-composition","seedWorkspaces":[{"id":"workshop","labelKey":"workshop.workspace","groups":[]}],"panelSet":[]}}
            """;
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(
            new HttpClient(new Handler(fixture)) { BaseAddress = new Uri("http://localhost:7308/") }));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var shell = Render<Shell>();

        shell.WaitForAssertion(() => Assert.Equal("Workshop",
            shell.Find("a[href='/workspaces/workshop']").TextContent.Trim()));
    }

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

    private sealed class FormsCatalogueClient : IWorkshopCatalogueClient
    {
        private static readonly ViewRenderPlan Plan = new(
            "sha256:test", "platform.list.forms", "1.0.0", "harborline.platform", "1.0.0", "ViewDefinition",
            new ViewRenderPlanBindings("views.entity-list/grid", new ViewRenderPlanParameters([
                new("formId", "Key"), new("title", "Title"), new("version", "Version"), new("cascadeLayer", "Cascade layer")] )));
        private static readonly JsonElement Body = JsonElement.Parse("""{"cascadeLayer":"Pack"}""");
        private static readonly WorkshopCatalogueEntry Entry = new("inspection", "1.0.0", "Active", new WorkshopLocalizedText("en", new Dictionary<string, string> { ["en"] = "Inspection" }), Body, null);

        public Task<WorkshopCatalogueEntry> ReadViewAsync(string itemId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Entry with { Id = $"platform.list.{itemId}", RenderPlan = Plan });
        public Task<IReadOnlyList<WorkshopCatalogueEntry>> ListAsync(string kind, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkshopCatalogueEntry>>([Entry]);
        public Task<WorkshopCatalogueEntry> ReadFormAsync(string id, string? version = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JsonElement> ReadJsonAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JsonElement> PostJsonAsync(string path, object body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JsonElement> PostArtifactAsync(string path, byte[] artifact, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportAsync(JsonElement candidate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
