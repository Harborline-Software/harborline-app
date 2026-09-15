using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Harborline.App.Blazor.ReferenceHost;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.App.Blazor.ReferenceHost.Navigation;
using Harborline.App.Blazor.ReferenceHost.Workshop;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;
using Harborline.UIAdapters.Blazor.Components.DataDisplay;
using Harborline.UIAdapters.Blazor.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Harborline.App.Blazor.Tests;

public sealed class PackNavigationTests : BunitContext
{
    [Theory]
    [InlineData("admin-forms")]
    [InlineData("admin-reports")]
    [InlineData("admin-views")]
    [InlineData("admin-data-exchange")]
    [InlineData("admin-scheduling")]
    public void Retired_routes_cannot_restore_when_navigation_seed_is_unconfigured(string item)
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        Services.AddSingleton<IPackNavigationClient>(new FixturePackNavigationClient());
        JSInterop.Mode = JSRuntimeMode.Loose;
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"http://localhost/?item={item}");
        var shell = Render<Shell>();
        shell.WaitForAssertion(() => Assert.Equal("assets", QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query)["item"].ToString()));
        Assert.Equal("Assets", shell.Find("main h1").TextContent.Trim());
        Assert.Empty(shell.FindAll("[role=grid]"));
        Assert.DoesNotContain(shell.FindComponent<HarborlineAppShell>().Instance.NavigationState!.Items!.Keys,
            id => id is "admin-forms" or "admin-reports" or "admin-views" or "admin-data-exchange" or "admin-scheduling");
    }

    private static string WorkshopFixture => ReadFixture("workshop-navigation.json");
    private static string AccessFirstFixture
    {
        get
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var workshop = JsonSerializer.Deserialize<PackNavigationResponse>(WorkshopFixture, options)!;
            var access = JsonSerializer.Deserialize<PackNavigationResponse>(ReadFixture("access-navigation.json"), options)!;
            return JsonSerializer.Serialize(workshop with { Pack = workshop.Pack! with { SeedWorkspaces = [.. access.Pack!.SeedWorkspaces, .. workshop.Pack!.SeedWorkspaces] } }, options);
        }
    }
    private static readonly (string Id, string Label)[] WorkshopItems =
    [
        ("asset-types", "Asset types"), ("forms", "Forms"), ("workflows", "Workflows"),
        ("standards", "Standards"), ("defaults", "Defaults"), ("terminology", "Terminology"),
        ("documents", "Documents"), ("taxonomies", "Taxonomies"), ("reports", "Reports"),
        ("data-exchanges", "Data exchanges"), ("standing-rules", "Standing rules"),
        ("schedules", "Schedules"), ("views", "Views"),
    ];
    private static readonly string[] WorkshopKeys =
    [
        "workshop.workspace", "workshop.definitions", "workshop.asset-types", "workshop.forms",
        "workshop.workflows", "workshop.standards", "workshop.defaults", "workshop.terminology",
        "workshop.documents", "workshop.taxonomies", "workshop.reports", "workshop.data-exchanges",
        "workshop.standing-rules", "workshop.schedules", "workshop.views",
    ];

    private static string ReadFixture(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Harborline.App.slnx"))) root = root.Parent;
        return File.ReadAllText(Path.Combine(root!.FullName, "tests/fixtures", name));
    }

    [Theory]
    [InlineData("health")]
    [InlineData("browse")]
    public void Restores_an_explicit_surface_for_a_declared_workshop_item(string surface)
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        Services.AddSingleton<IWorkshopCatalogueClient>(new FormsCatalogueClient());
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(
            new HttpClient(new Handler(WorkshopFixture)) { BaseAddress = new Uri("http://localhost:7308/") }));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"http://localhost/?item=forms&surface={surface}&selected=inspection%401.0.0&panels=inspector");
        var shell = Render<Shell>();
        shell.WaitForAssertion(() => Assert.Contains($"platform.{surface}.forms", shell.Find("[data-definition-source]").GetAttribute("data-definition-source"), StringComparison.Ordinal));
        Assert.Contains("Inspection", shell.Find("[data-shell-panel-id=inspector]").TextContent, StringComparison.Ordinal);
        Assert.Equal(surface, QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query)["surface"].ToString());
    }

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

    [Theory]
    [InlineData("", "inspection@1.0.0")]
    [InlineData(" \t\r\n ", "inspection@1.0.0")]
    [InlineData("Inspection", "Inspection")]
    [InlineData("  Inspection  ", "Inspection")]
    public void Inspector_shows_a_stable_identity_on_activation_and_restore(string title, string expectedIdentity)
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        Services.AddSingleton<IWorkshopCatalogueClient>(new FormsCatalogueClient(title));
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(
            new HttpClient(new Handler(WorkshopFixture)) { BaseAddress = new Uri("http://localhost:7308/") }));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/?source=shared%20link&item=forms#details");
        var shell = Render<Shell>();
        shell.WaitForAssertion(() => Assert.NotEmpty(shell.FindAll("[data-row-id='inspection@1.0.0']")));
        shell.Find("[data-row-id='inspection@1.0.0']").DoubleClick();

        void AssertIdentity(IRenderedComponent<Shell> rendered)
        {
            var inspector = rendered.Find("[data-shell-panel-id='inspector']");
            Assert.Equal(expectedIdentity, inspector.QuerySelector("h2")!.TextContent);
            Assert.Contains($"{expectedIdentity} · follows selection", inspector.QuerySelectorAll("p").Select(paragraph => paragraph.TextContent));
            var address = QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query);
            Assert.Equal("inspection@1.0.0", address["selected"].ToString());
            Assert.Equal("inspector", address["panels"].ToString());
            Assert.Equal("shared link", address["source"].ToString());
            Assert.Equal("#details", new Uri(navigation.Uri).Fragment);
        }

        shell.WaitForAssertion(() => AssertIdentity(shell));
        var copiedAddress = navigation.Uri;
        shell.Dispose();
        var restored = Render<Shell>();
        restored.WaitForAssertion(() => AssertIdentity(restored));
        Assert.Equal(copiedAddress, navigation.Uri);
    }

    [Theory]
    [InlineData(480, "compact", "bottom-sheet")]
    [InlineData(720, "medium", "side-sheet")]
    [InlineData(1024, "expanded", "side-sheet")]
    public void Shared_workshop_address_restores_the_same_frame_and_command_census(int width, string breakpoint, string containerKind)
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media(width));
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        Services.AddSingleton<IWorkshopCatalogueClient>(new FormsCatalogueClient());
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(
            new HttpClient(new Handler(WorkshopFixture)) { BaseAddress = new Uri("http://localhost:7308/") }));
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Harborline.UIAdapters.Blazor/dock-divider.js")
            .Setup<double>("measureInlineSize", _ => true).SetResult(width);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/?item=forms&selected=inspection%401.0.0&panels=inspector,pilot");

        var shell = Render<Shell>();

        shell.WaitForAssertion(() =>
        {
            Assert.Equal("Forms", shell.Find("main h1").TextContent.Trim());
            Assert.Contains("Inspection", shell.Find("[data-shell-panel-id='inspector']").TextContent, StringComparison.Ordinal);
        });
        var declaration = JsonSerializer.Deserialize<PackNavigationResponse>(WorkshopFixture, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Pack!;
        var workspace = Assert.Single(declaration.SeedWorkspaces);
        var group = Assert.Single(workspace.Groups!);
        var panel = Assert.Single(declaration.PanelSet!);
        Assert.Equal(("workshop", "workshop.workspace"), (workspace.Id, workspace.LabelKey));
        Assert.Equal(("definitions", "workshop.definitions"), (group.Id, group.LabelKey));
        Assert.Equal(WorkshopItems.Select(item => item.Id), group.ItemIds);
        Assert.Equal(WorkshopItems.Select(item => (item.Id, $"workshop.{item.Id}", item.Label)),
            group.Items!.Select(item => (item.Id, item.LabelKey, item.Label)));
        const string inspectorLabel = "Inspector";
        if (width < 840) shell.Find("button[aria-label='Navigation'][aria-controls]").Click();
        var rail = shell.Find("[data-shell-region='rail']");
        shell.Find("button.hl-app-shell__show-more").Click();
        Assert.Equal(new[] { ("/workspaces/workshop", "Workshop") }
            .Concat(WorkshopItems.Select(item => ($"/workspaces/{item.Id}", item.Label))),
            rail.QuerySelectorAll("a").Select(link => (link.GetAttribute("href")!, link.TextContent.Trim())));
        Assert.Contains("Definitions", rail.TextContent, StringComparison.Ordinal);
        Assert.Contains("Asset types", rail.TextContent, StringComparison.Ordinal);
        Assert.True(rail.TextContent.IndexOf("Definitions", StringComparison.Ordinal)
            < rail.TextContent.IndexOf("Asset types", StringComparison.Ordinal));
        Assert.All(WorkshopKeys, key => Assert.DoesNotContain(key, rail.TextContent, StringComparison.Ordinal));
        Assert.Equal("page", shell.Find("a[href='/workspaces/forms']").GetAttribute("aria-current"));
        shell.Find("button[aria-label='Panels']").Click();
        Assert.Equal(declaration.PanelSet!.Select(item => item.Id), shell.FindAll("[data-action-id]").Select(control => control.GetAttribute("data-action-id")));
        Assert.Equal(inspectorLabel, shell.Find("[data-action-id='inspector'] [role='menuitem']").TextContent.Trim());
        Assert.Equal($"panels.{panel.Id}.toggle", panel.Binding);
        Assert.Null(declaration.ModeSwitch);
        Assert.Empty(shell.FindAll("[data-shell-zone='mode']"));
        var inspector = shell.Find("[data-shell-panel-id='inspector']");
        var address = QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query);
        // One implicit facet and one open record: the selected definition in this bounded fixture.
        Assert.Equal((workspace.Id, breakpoint, (string?)null, "inspection@1.0.0", "default", panel.Id, containerKind),
            (shell.Find("a[href='/workspaces/workshop']").GetAttribute("aria-current") == "page" ? workspace.Id : null,
             shell.Find("[data-shell-breakpoint]").GetAttribute("data-shell-breakpoint"),
             address.TryGetValue("mode", out var mode) ? mode.ToString() : null,
             address["selected"].ToString(), inspector.QuerySelector("[role='tablist']") is null ? "default" : null,
             Assert.Single(shell.FindAll("[data-shell-panel-id]")).GetAttribute("data-shell-panel-id"),
             inspector.GetAttribute("data-shell-container-kind")));
        Assert.Equal(new[] { "inspection@1.0.0" }, inspector.QuerySelectorAll("h2").Select(heading => heading.TextContent == "Inspection" ? "inspection@1.0.0" : null));
        Assert.Equal(panel.Id, address["panels"].ToString());
        var beforeUndeclared = navigation.Uri;
        shell.Find("[data-shell-id]").KeyDown(new KeyboardEventArgs { Key = "p", MetaKey = true, ShiftKey = true });
        Assert.Equal(beforeUndeclared, navigation.Uri);
        Assert.Empty(shell.FindAll("[data-action-id='pilot'], [data-shell-panel-id='pilot'], button[aria-label='Pilot']"));
        shell.Find("button[aria-label='Close inspector']").Click();
        Assert.Empty(shell.FindAll("[data-shell-panel-id]"));
        Assert.False(QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query).ContainsKey("panels"));
        shell.Find("[data-action-id='inspector'] button").Click();
        Assert.Equal(panel.Id, QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query)["panels"].ToString());
        shell.Find("button[aria-label='Close inspector']").Click();
        shell.Find("[data-shell-id]").KeyDown(new KeyboardEventArgs
        {
            Key = panel.Shortcut.Split('+')[^1], MetaKey = panel.Shortcut.Contains("mod", StringComparison.Ordinal),
            ShiftKey = panel.Shortcut.Contains("shift", StringComparison.Ordinal),
        });
        Assert.Contains("Inspection", shell.Find("[data-shell-panel-id='inspector']").TextContent, StringComparison.Ordinal);
        Assert.Equal(panel.Id, QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query)["panels"].ToString());
    }

    [Theory]
    [InlineData("http://localhost/?item=forms")]
    [InlineData("http://localhost/?item=forms&selected=inspection%401.0.0&panels=inspector")]
    [InlineData("http://localhost/?source=shared%20link&item=forms&selected=inspection%401.0.0&panels=inspector#details")]
    public void Canonical_workshop_address_with_Access_first_restores_owner_after_delayed_configuration_and_reload_without_replacing_navigation(string address)
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        Services.AddSingleton<IWorkshopCatalogueClient>(new FormsCatalogueClient());
        var navigationReady = new TaskCompletionSource();
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(
            new HttpClient(new Handler(AccessFirstFixture) { Ready = navigationReady.Task }) { BaseAddress = new Uri("http://localhost:7308/") }));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var navigation = Assert.IsType<BunitNavigationManager>(Services.GetRequiredService<NavigationManager>());
        navigation.NavigateTo(address);
        var initialHistory = navigation.History.ToArray();

        var shell = Render<Shell>();
        Assert.Empty(shell.FindAll("a[href='/workspaces/workshop']"));
        navigationReady.SetResult();

        shell.WaitForAssertion(() =>
        {
            Assert.Equal("Forms", shell.Find("main h1").TextContent.Trim());
            Assert.Equal("page", shell.Find("a[href='/workspaces/workshop']").GetAttribute("aria-current"));
            Assert.Equal("page", shell.Find("a[href='/workspaces/forms']").GetAttribute("aria-current"));
            Assert.Equal("Harborline / Workshop / Forms", shell.Find(".happ-breadcrumb").TextContent.Trim());
            Assert.NotEmpty(shell.FindAll("[role=grid]"));
            if (address.Contains("panels=inspector", StringComparison.Ordinal))
                Assert.Contains("Inspection", shell.Find("[data-shell-panel-id='inspector']").TextContent, StringComparison.Ordinal);
            Assert.Equal(address, navigation.Uri);
            Assert.Equal(initialHistory, navigation.History);
        });
        shell.Find("button.hl-app-shell__show-more").Click();
        shell.WaitForAssertion(() => Assert.Equal(new[] { "/workspaces/access", "/workspaces/workshop" }.Concat(WorkshopItems.Select(item => $"/workspaces/{item.Id}")),
            shell.FindAll("[data-shell-region='rail'] a").Select(link => link.GetAttribute("href"))));
        shell.Dispose();
        var restored = Render<Shell>();
        restored.WaitForAssertion(() =>
        {
            Assert.Equal("page", restored.Find("a[href='/workspaces/workshop']").GetAttribute("aria-current"));
            Assert.Equal("page", restored.Find("a[href='/workspaces/forms']").GetAttribute("aria-current"));
            Assert.Equal("Harborline / Workshop / Forms", restored.Find(".happ-breadcrumb").TextContent.Trim());
            if (address.Contains("panels=inspector", StringComparison.Ordinal))
                Assert.Contains("Inspection", restored.Find("[data-shell-panel-id='inspector']").TextContent, StringComparison.Ordinal);
            Assert.Equal(address, navigation.Uri);
            Assert.Equal(initialHistory, navigation.History);
        });
        restored.Find("a[href='/workspaces/access']").Click();
        Assert.Equal("page", restored.Find("a[href='/workspaces/access']").GetAttribute("aria-current"));
        restored.Find("a[href='/workspaces/access.holders']").Click();
        Assert.Equal("Harborline / Access / Holders", restored.Find(".happ-breadcrumb").TextContent.Trim());
        Assert.False(QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query).ContainsKey("selected"));
        restored.Find("a[href='/workspaces/workshop']").Click();
        restored.Find("a[href='/workspaces/forms']").Click();
        Assert.Equal("page", restored.Find("a[href='/workspaces/workshop']").GetAttribute("aria-current"));
        Assert.Equal("Harborline / Workshop / Forms", restored.Find(".happ-breadcrumb").TextContent.Trim());
        Assert.Equal(new Uri(address).Fragment, new Uri(navigation.Uri).Fragment);
        Assert.Equal(QueryHelpers.ParseQuery(new Uri(address).Query).GetValueOrDefault("source"), QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query).GetValueOrDefault("source"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("assets")]
    [InlineData("unavailable")]
    [InlineData("forms")]
    [InlineData("empty")]
    public void Address_without_a_workshop_item_clears_stale_selection_and_undeclared_panels(string item)
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var declaration = JsonSerializer.Deserialize<PackNavigationResponse>(AccessFirstFixture, options)!;
        if (item == "forms") declaration = declaration with { Pack = declaration.Pack! with { SeedWorkspaces = declaration.Pack!.SeedWorkspaces.Where(workspace => workspace.Id != "workshop").ToArray() } };
        if (item == "empty") declaration = declaration with { Pack = declaration.Pack! with { SeedWorkspaces = [], PanelSet = [] } };
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(
            new HttpClient(new Handler(JsonSerializer.Serialize(declaration, options))) { BaseAddress = new Uri("http://localhost:7308/") }));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var navigation = Assert.IsType<BunitNavigationManager>(Services.GetRequiredService<NavigationManager>());
        navigation.NavigateTo($"http://localhost/?item={item}&selected=inspection%401.0.0&panels=pilot");
        var shell = Render<Shell>();
        shell.WaitForAssertion(() =>
        {
            Assert.Equal("Assets", shell.Find("main h1").TextContent.Trim());
            Assert.Equal("Harborline / Portfolio / Assets", shell.Find(".happ-breadcrumb").TextContent.Trim());
            if (item == "empty") Assert.Empty(shell.FindAll("[data-shell-region='rail'] a"));
            var address = QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query);
            Assert.Equal("assets", address["item"].ToString());
            Assert.False(address.ContainsKey("selected"));
            Assert.False(address.ContainsKey("panels"));
            var replacement = Assert.Single(navigation.History);
            Assert.Equal("http://localhost/?item=assets", replacement.Uri);
            Assert.True(replacement.Options.ReplaceHistoryEntry);
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
        var fixture = WorkshopFixture;
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
        public Task Ready = Task.CompletedTask;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            await Ready.WaitAsync(cancellationToken);
            return new HttpResponseMessage(Status) { Content = new StringContent(Json, Encoding.UTF8, "application/json") };
        }
    }
    private sealed class Media(int width = 1600) : IMediaQueryObserver
    {
        public ValueTask<IMediaQuerySubscription> ObserveAsync(string query, Func<MediaQueryChange, ValueTask> changed, CancellationToken cancellationToken = default) => new(new Subscription(query, width));
        private sealed class Subscription(string query, int width) : IMediaQuerySubscription
        {
            public string Query => query;
            public bool Matches => System.Text.RegularExpressions.Regex.Matches(query, @"(min|max)-width:\s*(\d+)px")
                .All(match => match.Groups[1].Value == "min" ? width >= int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)
                    : width <= int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FormsCatalogueClient(string title = "Inspection") : IWorkshopCatalogueClient
    {
        private static readonly ViewRenderPlan Plan = new(
            "sha256:test", "platform.list.forms", "1.0.0", "harborline.platform", "1.0.0", "ViewDefinition",
            new ViewRenderPlanBindings("views.entity-list/grid", new ViewRenderPlanParameters([
                new("formId", "Key"), new("title", "Title"), new("version", "Version"), new("cascadeLayer", "Cascade layer")] )));
        private static readonly JsonElement Body = JsonElement.Parse("""{"cascadeLayer":"Pack","privateNote":"Private body is not an identity"}""");
        private readonly WorkshopCatalogueEntry Entry = new("inspection", "1.0.0", "Active", new WorkshopLocalizedText("en", new Dictionary<string, string> { ["en"] = title }), Body, null);

        public Task<WorkshopCatalogueEntry> ReadViewAsync(string viewId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Entry with { Id = viewId, RenderPlan = Plan with { DefinitionId = viewId } });
        public Task<WorkshopCatalogueList> ListAsync(string kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkshopCatalogueList([Entry], []));
        public Task<WorkshopCatalogueEntry> ReadFormAsync(string id, string? version = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JsonElement> ReadJsonAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JsonElement> PostJsonAsync(string path, object body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JsonElement> PostArtifactAsync(string path, byte[] artifact, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportAsync(JsonElement candidate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
