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
        {"id":"platform.list.forms","version":"1.0.0","status":"Published","renderPlan":{"definitionHash":"hash","definitionId":"platform.list.forms","definitionVersion":"1.0.0","packKey":"harborline.platform","packVersion":"1.0.0","definitionKind":"ViewDefinition","bindings":{"viewKind":"layout.table","parameters":{"fields":[{"id":"status","label":"Lifecycle"},{"id":"formId","label":"Key"}]},"actions":[{"id":"inspect","label":"Inspect"}]}}}
        """;
    private const string Entry = """
        {"id":"inspection","version":"2.0.0","kind":0,"status":"Published","sealed":true,"provenance":{"packKey":"acme.assets","packVersion":"1.0.0","kind":"pack"},"updatedAt":"2026-09-15T00:00:00Z","definitionHash":"sha256:definition","body":{"id":"body-id","version":"body-version","provenance":{"kind":"body"}}}
        """;

    // Public wire literals from API Catalogue.Map/RequestAuthorization.Write at API 2496ab5d.
    // Diagnostic, owner/tenant data, pointers and inline traces are not public catalogue refusal fields.
    private const string PublicRefusal = """
        {"code":"authorization.permission_required","permission":"catalogue:read","title":"You do not have permission for this action.","detail":"The action was refused because a required permission is missing.","remediation":"Ask an administrator to grant the permission this action requires.","auditId":"11111111-1111-1111-1111-111111111111","correlationId":"22222222-2222-2222-2222-222222222222","Diagnostic":"classified-diagnostic","tenant":"private-tenant","owner":"private-owner","pointers":["/private-field"],"trace":["private-stage"]}
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Catalogue_list_and_view_refusals_preserve_only_public_fields(bool failList)
    {
        var handler = new CatalogueHandler(failList ? PublicRefusal : $$"""{"entries":[{{Entry}}],"kindsUnavailable":[]}""")
        {
            ViewStatus = failList ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
            ViewJson = failList ? View : PublicRefusal,
            ListStatus = failList ? HttpStatusCode.Forbidden : HttpStatusCode.OK,
        };
        Configure(string.Empty, handler);
        ViewRuntimeRow? restored = null;
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms")
            .Add(page => page.SelectedRowId, "inspection@2.0.0").Add(page => page.SelectionRestored, row => restored = row));
        cut.WaitForAssertion(() => Assert.Contains("authorization.permission_required", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        var text = cut.Find("[role=alert]").TextContent;
        Assert.Contains("catalogue:read", text, StringComparison.Ordinal);
        Assert.Contains("You do not have permission for this action.", text, StringComparison.Ordinal);
        Assert.Contains("The action was refused because a required permission is missing.", text, StringComparison.Ordinal);
        Assert.Contains("Ask an administrator to grant the permission this action requires.", text, StringComparison.Ordinal);
        Assert.Contains("Audit ID: 11111111-1111-1111-1111-111111111111", text, StringComparison.Ordinal);
        Assert.Contains("Correlation ID: 22222222-2222-2222-2222-222222222222", text, StringComparison.Ordinal);
        foreach (var hidden in new[] { "classified-diagnostic", "private-tenant", "private-owner", "private-field", "private-stage" })
            Assert.DoesNotContain(hidden, text, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("button,form,[role=grid],a"));
        Assert.Null(restored);
        Assert.Equal(2, handler.Paths.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"code":"catalogue.kind_unknown","detail":"private-detail"}""", "catalogue.kind_unknown")]
    [InlineData(HttpStatusCode.NotFound, """{"code":"catalogue.definition_not_found","Diagnostic":"private-diagnostic"}""", "catalogue.definition_not_found")]
    public void Catalogue_codes_survive_without_unrelated_payload_members(HttpStatusCode status, string body, string code)
    {
        Configure(string.Empty, new CatalogueHandler("""{"entries":[],"kindsUnavailable":[]}""") { ViewStatus = status, ViewJson = body });
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms"));
        cut.WaitForAssertion(() => Assert.Contains(code, cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.DoesNotContain("private-", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("button,form,[role=grid]"));
    }

    [Fact]
    public void A_server_owned_reason_and_withheld_detail_survive_without_synthetic_receipts()
    {
        const string body = """
            {"code":"authorization.grant.attenuation_failed","permission":"catalogue:read","title":"You do not have permission for this action.","detail":"Further details of this refusal are withheld because they are not yours to read.","remediation":"Ask an administrator to grant the permission this action requires."}
            """;
        Configure(string.Empty, new CatalogueHandler("""{"entries":[],"kindsUnavailable":[]}""") { ViewStatus = HttpStatusCode.Forbidden, ViewJson = body });
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms"));
        cut.WaitForAssertion(() => Assert.Contains("authorization.grant.attenuation_failed", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        var text = cut.Find("[role=alert]").TextContent;
        Assert.Contains("Further details of this refusal are withheld because they are not yours to read.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Audit ID:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Correlation ID:", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("private raw response")]
    [InlineData("{\"code\":")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"code":"future.unknown","detail":"private-detail"}""")]
    [InlineData("""{"code":"authorization.permission_required","permission":"records:read","title":"private-title","detail":"private-detail","remediation":"private-remediation"}""")]
    [InlineData("""{"code":"authorization.permission_required","permission":"catalogue:read","title":"private-title","detail":{"owner":"private-owner"},"remediation":"private-remediation"}""")]
    [InlineData("""{"code":"authorization.permission_required","permission":"catalogue:read","title":"private-title","detail":"private-detail","remediation":"private-remediation","auditId":"private-identifier"}""")]
    [InlineData("""{"code":"authorization.permission_required","permission":"catalogue:read","title":"private-title","detail":"private-detail","remediation":"private-remediation","correlationId":null}""")]
    public void Unknown_or_malformed_refusals_use_only_the_status_fallback(string body)
    {
        Configure(string.Empty, new CatalogueHandler("""{"entries":[],"kindsUnavailable":[]}""") { ViewStatus = HttpStatusCode.Forbidden, ViewJson = body });
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms"));
        cut.WaitForAssertion(() => Assert.Equal("Workshop catalogue request failed (403).", cut.Find("[role=alert]").TextContent));
        Assert.Empty(cut.FindAll("button,form,[role=grid]"));
    }

    [Fact]
    public void Public_refusal_text_is_encoded_without_interpreting_markup()
    {
        var body = PublicRefusal.Replace("The action was refused because a required permission is missing.", "<img src='private' onerror='alert(1)'>", StringComparison.Ordinal);
        Configure(string.Empty, new CatalogueHandler("""{"entries":[],"kindsUnavailable":[]}""") { ViewStatus = HttpStatusCode.Forbidden, ViewJson = body });
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms"));
        cut.WaitForAssertion(() => Assert.Contains("<img src='private' onerror='alert(1)'>", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("img"));
    }

    [Fact]
    public async Task Catalogue_read_sanitizing_does_not_change_command_or_form_error_bodies()
    {
        var client = Configure(PublicRefusal, new CatalogueHandler(PublicRefusal) { ListStatus = HttpStatusCode.Forbidden });
        var command = await Assert.ThrowsAsync<WorkshopRequestException>(() => client.PostJsonAsync("api/local-node/packs/check", new { }));
        Assert.Equal(PublicRefusal, command.ResponseBody);
        var form = await Assert.ThrowsAsync<WorkshopRequestException>(() => client.ReadFormAsync("declared.author"));
        Assert.Equal(PublicRefusal, form.ResponseBody);
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
    }

    [Fact]
    public async Task A_previous_surface_refusal_cannot_replace_current_results()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        Configure(string.Empty, new CatalogueHandler("""{"entries":[],"kindsUnavailable":[]}""") { PendingHealth = pending });
        var cut = Render<TrackedSeededList>(parameters => parameters.Add(page => page.ItemId, "forms").Add(page => page.ViewId, "platform.health.forms"));
        var firstLoad = cut.Instance.Load;
        cut.Render(parameters => parameters.Add(page => page.ViewId, "platform.browse.forms"));
        cut.WaitForAssertion(() => Assert.Contains("platform.browse.forms", cut.Find("[data-definition-source]").GetAttribute("data-definition-source"), StringComparison.Ordinal));
        pending.SetResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent(PublicRefusal, Encoding.UTF8, "application/json") });
        await firstLoad;
        Assert.Empty(cut.FindAll("[role=alert]"));
        Assert.DoesNotContain("11111111-1111-1111-1111-111111111111", cut.Markup, StringComparison.Ordinal);
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
        public HttpStatusCode ListStatus { get; init; } = HttpStatusCode.OK;
        public string ViewJson { get; init; } = View;
        public TaskCompletionSource<HttpResponseMessage>? PendingHealth { get; init; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.PathAndQuery);
            if (PendingHealth is not null && request.RequestUri.AbsolutePath.EndsWith("platform.health.forms", StringComparison.Ordinal)) return PendingHealth.Task;
            var isView = request.RequestUri.AbsolutePath.Contains("/ViewDefinition/", StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(isView ? ViewStatus : ListStatus)
            {
                Content = new StringContent(isView ? ViewJson.Replace("platform.list.forms", request.RequestUri.Segments[^1], StringComparison.Ordinal) : list, Encoding.UTF8, "application/json"),
            });
        }
    }
}
