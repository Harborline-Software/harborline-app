using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost.Workshop;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Components.DataDisplay;
using Microsoft.Extensions.DependencyInjection;

namespace Harborline.App.Blazor.Tests;

public sealed class CatalogueAvailabilityTests : BunitContext
{
    private const string View = """
        {"id":"platform.list.forms","version":"1.0.0","status":"Published","renderPlan":{"definitionHash":"hash","definitionId":"platform.list.forms","definitionVersion":"1.0.0","packKey":"harborline.platform","packVersion":"1.0.0","definitionKind":"ViewDefinition","bindings":{"viewKind":"views.entity-list/grid","parameters":{"fields":[{"id":"status","label":"Lifecycle"},{"id":"formId","label":"Key"}]},"actions":[{"id":"inspect","label":"Inspect"}]}}}
        """;
    private const string Entry = """
        {"id":"inspection","version":"2.0.0","kind":0,"status":"Published","sealed":true,"provenance":{"packKey":"acme.assets","packVersion":"1.0.0","kind":"pack"},"updatedAt":"2026-09-15T00:00:00Z","definitionHash":"sha256:definition","body":{"id":"body-id","version":"body-version","provenance":{"kind":"body"}}}
        """;

    [Theory]
    [InlineData("platform.health.forms")]
    [InlineData("platform.browse.forms")]
    public async Task Http_client_reads_the_exact_view_identity(string viewId)
    {
        var handler = new CatalogueHandler("""{"entries":[],"kindsUnavailable":[]}""");
        var client = new HttpWorkshopCatalogueClient(new HttpClient(handler) { BaseAddress = new Uri("https://node.test/") });
        await client.ReadViewAsync(viewId);
        Assert.Equal($"/api/local-node/catalogue/definitions/ViewDefinition/{viewId}", Assert.Single(handler.Paths));
    }

    [Theory]
    [InlineData("platform.health.forms")]
    [InlineData("platform.browse.forms")]
    public void Exact_view_renders_declared_fields_in_order(string viewId)
    {
        Configure("""{"entries":[],"kindsUnavailable":[]}""");
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms").Add(page => page.ViewId, viewId));
        cut.WaitForAssertion(() => Assert.Contains(viewId, cut.Find("[data-definition-source]").GetAttribute("data-definition-source"), StringComparison.Ordinal));
        Assert.Equal(["Lifecycle", "Key"], cut.FindAll("[role=columnheader]").Select(cell => cell.TextContent));
        Assert.Contains("No definitions.", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll("button"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public void Denied_or_absent_view_does_not_fall_back_or_expose_actions(HttpStatusCode status)
    {
        var handler = new CatalogueHandler("""{"entries":[],"kindsUnavailable":[]}""") { ViewStatus = status };
        Configure(string.Empty, handler);
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms").Add(page => page.ViewId, "platform.health.forms"));
        cut.WaitForAssertion(() => Assert.Contains(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("button,form,[role=grid]"));
        Assert.Equal("/api/local-node/catalogue/definitions/ViewDefinition/platform.health.forms", Assert.Single(handler.Paths, path => path.Contains("/ViewDefinition/", StringComparison.Ordinal)));
    }

    [Fact]
    public void Inert_view_does_not_restore_selection_or_expose_actions()
    {
        Configure(string.Empty, new CatalogueHandler($$"""{"entries":[{{Entry}}],"kindsUnavailable":[]}""") { ViewJson = View.Replace("\"definitionKind\":\"ViewDefinition\"", "\"definitionKind\":\"UnknownDefinition\"", StringComparison.Ordinal) });
        ViewRuntimeRow? restored = null;
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms")
            .Add(page => page.ViewId, "platform.health.forms").Add(page => page.SelectedRowId, "inspection@2.0.0")
            .Add(page => page.SelectionRestored, row => restored = row));
        cut.WaitForAssertion(() => Assert.Equal(string.Empty, cut.Markup.Trim()));
        Assert.Null(restored);
    }

    [Fact]
    public async Task A_late_previous_surface_response_cannot_replace_the_current_view()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        Configure(string.Empty, new CatalogueHandler("""{"entries":[],"kindsUnavailable":[]}""") { PendingHealth = pending });
        var cut = Render<TrackedSeededList>(parameters => parameters.Add(page => page.ItemId, "forms").Add(page => page.ViewId, "platform.health.forms"));
        var firstLoad = cut.Instance.Load;
        Assert.False(firstLoad.IsCompleted);
        cut.Render(parameters => parameters.Add(page => page.ViewId, "platform.browse.forms"));
        cut.WaitForAssertion(() => Assert.Contains("platform.browse.forms", cut.Find("[data-definition-source]").GetAttribute("data-definition-source"), StringComparison.Ordinal));
        pending.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(View.Replace("platform.list.forms", "platform.health.forms", StringComparison.Ordinal), Encoding.UTF8, "application/json") });
        await firstLoad;
        Assert.Contains("platform.browse.forms", cut.Find("[data-definition-source]").GetAttribute("data-definition-source"), StringComparison.Ordinal);
    }

    public sealed class TrackedSeededList : SeededListPage
    {
        public Task Load { get; private set; } = Task.CompletedTask;
        protected override Task OnParametersSetAsync() => Load = base.OnParametersSetAsync();
        protected override Task OnAfterRenderAsync(bool firstRender) => firstRender ? Load = base.OnAfterRenderAsync(firstRender) : base.OnAfterRenderAsync(firstRender);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("\"FormDefinition\"")]
    public void Unavailable_catalogue_has_no_grid_or_actions(string unavailable)
    {
        Configure($$"""{"entries":[],"kindsUnavailable":[{{unavailable}}]}""");
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms"));
        cut.WaitForAssertion(() => Assert.Contains("FormDefinition is unavailable in this host.", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("button,form,[role=grid]"));
        Assert.DoesNotContain("No definitions.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Http_client_preserves_the_availability_envelope()
    {
        var client = Configure("""{"entries":[],"kindsUnavailable":[0]}""");
        var result = JsonSerializer.SerializeToElement(await client.ListAsync("FormDefinition"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(0, result.GetProperty("kindsUnavailable")[0].GetInt32());
    }

    [Fact]
    public void Catalogue_metadata_and_body_remain_separate_when_selection_is_restored()
    {
        Configure($$"""{"entries":[{{Entry}}],"kindsUnavailable":[]}""");
        ViewRuntimeRow? restored = null;
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms")
            .Add(page => page.SelectedRowId, "inspection@2.0.0")
            .Add(page => page.SelectionRestored, row => restored = row));
        cut.WaitForAssertion(() => Assert.NotNull(restored));
        Assert.Equal("inspection@2.0.0", restored!.Id);
        var json = JsonSerializer.SerializeToElement(restored.Values, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("body-id", json.GetProperty("body").GetProperty("id").GetString());
        var metadata = json.GetProperty("catalogue");
        Assert.Equal("pack", json.GetProperty("provenance").GetProperty("kind").GetString());
        Assert.Equal("inspection", metadata.GetProperty("id").GetString());
        Assert.Equal(0, metadata.GetProperty("kind").GetInt32());
        Assert.Equal("acme.assets", metadata.GetProperty("provenance").GetProperty("packKey").GetString());
        Assert.True(metadata.GetProperty("sealed").GetBoolean());
        Assert.Equal("sha256:definition", metadata.GetProperty("definitionHash").GetString());
    }

    private HttpWorkshopCatalogueClient Configure(string list, CatalogueHandler? handler = null)
    {
        var client = new HttpWorkshopCatalogueClient(new HttpClient(handler ?? new CatalogueHandler(list)) { BaseAddress = new Uri("https://node.test/") });
        Services.AddSingleton<IWorkshopCatalogueClient>(client);
        Services.AddHarborlineUiAdapters();
        JSInterop.Mode = JSRuntimeMode.Loose;
        return client;
    }

    private sealed class CatalogueHandler(string list) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public HttpStatusCode ViewStatus { get; init; } = HttpStatusCode.OK;
        public string ViewJson { get; init; } = View;
        public TaskCompletionSource<HttpResponseMessage>? PendingHealth { get; init; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.PathAndQuery);
            if (PendingHealth is not null && request.RequestUri.AbsolutePath.EndsWith("platform.health.forms", StringComparison.Ordinal)) return PendingHealth.Task;
            var isView = request.RequestUri.AbsolutePath.Contains("/ViewDefinition/", StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(isView ? ViewStatus : HttpStatusCode.OK)
            {
                Content = new StringContent(isView ? ViewJson.Replace("platform.list.forms", request.RequestUri.Segments[^1], StringComparison.Ordinal) : list, Encoding.UTF8, "application/json"),
            });
        }
    }
}
