using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Workshop;

namespace Harborline.App.Blazor.Tests;

public sealed class WorkshopWorkflowTests
{
    internal const string PackJson = """{"key":"example.pack","version":"1.0.0","contents":[{"key":"example.record","kind":"AssetTypeDefinition"}]}""";
    internal const string Actions = """[{"id":"a","label":"Check this draft","operation":"pack.validate","inputForm":"declared.author"},{"id":"b","label":"Download this artifact","operation":"pack.export"},{"id":"c","label":"Check signature","operation":"pack.verify"},{"id":"d","label":"Add artifact","operation":"pack.install"},{"id":"e","label":"Use artifact","operation":"pack.activate"},{"id":"f","label":"Capture entry","operation":"record.create","input":"active-pack.property-form"},{"id":"g","label":"Inspect entry","operation":"record.read"}]""";
    internal const string ActionsWithCheck = """[{"id":"a","label":"Check this draft","operation":"pack.validate","inputForm":"declared.author"},{"id":"b","label":"Download this artifact","operation":"pack.export"},{"id":"c","label":"Check signature","operation":"pack.verify"},{"id":"check","label":"Check candidate","operation":"pack.check"},{"id":"d","label":"Add artifact","operation":"pack.install"},{"id":"e","label":"Use artifact","operation":"pack.activate"},{"id":"f","label":"Capture entry","operation":"record.create","input":"active-pack.property-form"},{"id":"g","label":"Inspect entry","operation":"record.read"}]""";

    [Fact]
    public async Task Declared_pack_check_must_pass_without_refusals_before_install()
    {
        var handler = new WorkflowHandler { IncludeCheck = true, RefuseCheck = true };
        var client = Client(handler);
        var workflow = new WorkshopWorkflow(client);
        workflow.Bind(await client.ReadViewAsync("platform.list.forms"));
        await workflow.ActivateActionAsync("a");
        await workflow.SubmitAsync(new Dictionary<string, object?> { ["packJson"] = PackJson });
        foreach (var id in new[] { "b", "c" }) await workflow.ActivateActionAsync(id);

        await workflow.ActivateActionAsync("d");
        Assert.Contains("Check", workflow.Error, StringComparison.Ordinal);
        await workflow.ActivateActionAsync("check");
        Assert.Contains("Pack check refused", workflow.Error, StringComparison.Ordinal);
        Assert.Contains("pack.requirement.unmet", workflow.ResultText, StringComparison.Ordinal);
        Assert.Contains("/requirements/0", workflow.ResultText, StringComparison.Ordinal);
        await workflow.ActivateActionAsync("d");
        Assert.DoesNotContain(handler.Requests, request => request.Path.EndsWith("/install", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Declared_actions_execute_the_pack_and_bound_record_workflow_over_the_authenticated_client()
    {
        var handler = new WorkflowHandler();
        var client = Client(handler);
        var workflow = new WorkshopWorkflow(client);
        workflow.Bind(await client.ReadViewAsync("platform.list.forms"));
        await workflow.ActivateActionAsync("a");
        Assert.Equal("Authored title", workflow.Form!.Title!.Values["en"]);
        Assert.Equal("textarea", Assert.Single(Assert.Single(workflow.Form.Sections).Fields).ControlHint);
        var validation = await workflow.SubmitAsync(new Dictionary<string, object?> { ["packJson"] = PackJson });
        Assert.True(validation!.IsValid);
        await workflow.ActivateActionAsync("b");
        Assert.Equal("example.pack-1.0.0.pack", workflow.DownloadName);
        Assert.Equal("data:application/octet-stream;base64,AQIDBA==", workflow.DownloadUrl);
        await workflow.ActivateActionAsync("c");
        await workflow.ActivateActionAsync("d");
        Assert.True(await workflow.ActivateActionAsync("e"));
        await workflow.ActivateActionAsync("f");
        Assert.Equal("example.capture", workflow.Form!.FormId);
        Assert.Equal("2.1.0", workflow.Form.Version);
        Assert.True((await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "User supplied value" }))!.IsValid);
        await workflow.ActivateActionAsync("g");
        Assert.Null(workflow.Error);
        using var read = JsonDocument.Parse(workflow.ResultText!);
        Assert.Equal("User supplied value", read.RootElement.GetProperty("record").GetProperty("values").GetProperty("subject").GetString());
        Assert.Equal("verdict", read.RootElement.GetProperty("trace").GetProperty("steps")[0].GetProperty("stage").GetString());
        Assert.All(handler.Requests, request => Assert.Equal("Bearer founder-session", request.Authorization));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, handler.Requests.Single(request => request.Path.EndsWith("/verify", StringComparison.Ordinal)).Bytes);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, handler.Requests.Single(request => request.Path.EndsWith("/install", StringComparison.Ordinal)).Bytes);
        var creation = JsonElement.Parse(handler.Requests.Single(request => request.Path == "/api/local-node/asset-registry/entities").Bytes);
        Assert.Equal("example.record", creation.GetProperty("type").GetString());
        Assert.Equal("User supplied value", creation.GetProperty("values").GetProperty("subject").GetString());
    }

    [Fact]
    public async Task Editing_the_author_candidate_invalidates_artifact_and_all_later_operations()
    {
        var handler = new WorkflowHandler();
        var client = Client(handler);
        var workflow = new WorkshopWorkflow(client);
        workflow.Bind(await client.ReadViewAsync("platform.list.forms"));
        await workflow.ActivateActionAsync("b");
        Assert.Contains("Validate", workflow.Error, StringComparison.Ordinal);
        await workflow.ActivateActionAsync("a");
        await workflow.SubmitAsync(new Dictionary<string, object?> { ["packJson"] = PackJson });
        await workflow.ActivateActionAsync("b");
        var count = handler.Requests.Count;
        workflow.ChangeValues(new Dictionary<string, object?> { ["packJson"] = "{}" });
        Assert.Null(workflow.DownloadUrl);
        foreach (var id in new[] { "b", "c", "d", "e", "f", "g" }) await workflow.ActivateActionAsync(id);
        Assert.Equal(count, handler.Requests.Count);
        Assert.NotNull(workflow.Error);
    }

    [Fact]
    public async Task Missing_audit_receipt_never_reports_workflow_completion_or_enables_record_read()
    {
        var handler = new WorkflowHandler { OmitAuditId = true };
        var client = Client(handler);
        var workflow = new WorkshopWorkflow(client);
        workflow.Bind(await client.ReadViewAsync("platform.list.forms"));
        await workflow.ActivateActionAsync("a");
        await workflow.SubmitAsync(new Dictionary<string, object?> { ["packJson"] = PackJson });
        foreach (var id in new[] { "b", "c", "d", "e", "f" }) await workflow.ActivateActionAsync(id);
        var result = await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "User supplied value" });
        Assert.False(result!.IsValid);
        Assert.Contains("record may exist", workflow.Error, StringComparison.Ordinal);
        Assert.Contains("record-1", workflow.ResultText, StringComparison.Ordinal);
        var count = handler.Requests.Count;
        await workflow.ActivateActionAsync("g");
        Assert.Equal(count, handler.Requests.Count);
    }

    [Fact]
    public async Task Validation_refusal_displays_actual_server_codes_and_cannot_export()
    {
        var handler = new WorkflowHandler { RefuseValidation = true };
        var client = Client(handler);
        var workflow = new WorkshopWorkflow(client);
        workflow.Bind(await client.ReadViewAsync("platform.list.forms"));
        await workflow.ActivateActionAsync("a");
        var result = await workflow.SubmitAsync(new Dictionary<string, object?> { ["packJson"] = PackJson });
        Assert.False(result!.IsValid);
        Assert.Contains("pack.render-plan.unsupported-kind", workflow.ResultText, StringComparison.Ordinal);
        Assert.Equal("/packJson", Assert.Single(result.Errors).JsonPointer);
        var count = handler.Requests.Count;
        await workflow.ActivateActionAsync("b");
        Assert.Equal(count, handler.Requests.Count);
        Assert.Null(workflow.DownloadUrl);
    }

    [Fact]
    public async Task Busy_operations_ignore_edits_and_opening_a_form_clears_previous_evidence()
    {
        var handler = new WorkflowHandler { ExportRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var client = Client(handler);
        var workflow = new WorkshopWorkflow(client);
        workflow.Bind(await client.ReadViewAsync("platform.list.forms"));
        await workflow.ActivateActionAsync("a");
        await workflow.SubmitAsync(new Dictionary<string, object?> { ["packJson"] = PackJson });
        var exporting = workflow.ActivateActionAsync("b");
        Assert.True(workflow.Busy);
        workflow.ChangeValues(new Dictionary<string, object?> { ["packJson"] = "{}" });
        Assert.Equal(PackJson, workflow.Values["packJson"]);
        handler.ExportRelease.SetResult();
        await exporting;
        Assert.NotNull(workflow.DownloadUrl);
        Assert.NotNull(workflow.ResultText);
        await workflow.ActivateActionAsync("a");
        Assert.Null(workflow.ResultText);
        Assert.Null(workflow.Error);
        workflow.ChangeValues(new Dictionary<string, object?> { ["packJson"] = "{}" });
        Assert.Null(workflow.DownloadUrl);
    }

    [Fact]
    public async Task Unsupported_plans_remove_retained_controls_and_cannot_dispatch_declared_actions()
    {
        var handler = new WorkflowHandler();
        var client = Client(handler);
        var workflow = new WorkshopWorkflow(client);
        var entry = await client.ReadViewAsync("platform.list.forms");
        workflow.Bind(entry);
        await workflow.ActivateActionAsync("a");
        await workflow.SubmitAsync(new Dictionary<string, object?> { ["packJson"] = PackJson });
        await workflow.ActivateActionAsync("b");
        workflow.Bind(entry with { RenderPlan = entry.RenderPlan! with { DefinitionKind = "UnknownDefinition" } });
        Assert.Null(workflow.Form);
        Assert.Null(workflow.DownloadUrl);
        Assert.Null(workflow.ResultText);
        var count = handler.Requests.Count;
        await workflow.ActivateActionAsync("a");
        Assert.Equal(count, handler.Requests.Count);
        Assert.Null(workflow.Form);
    }

    [Fact]
    public async Task Refused_write_preserves_server_evidence_and_maps_value_pointers_to_form_fields()
    {
        var handler = new WorkflowHandler();
        var client = Client(handler);
        var workflow = new WorkshopWorkflow(client);
        workflow.Bind(await client.ReadViewAsync("platform.list.forms"));
        await workflow.ActivateActionAsync("a");
        await workflow.SubmitAsync(new Dictionary<string, object?> { ["packJson"] = PackJson });
        foreach (var id in new[] { "b", "c", "d", "e", "f" }) await workflow.ActivateActionAsync(id);
        handler.RefuseCreate = true;
        var invalid = await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "" });
        Assert.False(invalid!.IsValid);
        Assert.Equal("/subject", Assert.Single(invalid.Errors).JsonPointer);
        Assert.Equal(WorkflowHandler.Refusal, workflow.ResultText);
        var count = handler.Requests.Count;
        await workflow.ActivateActionAsync("g");
        Assert.Equal(count, handler.Requests.Count);
    }

    internal static HttpWorkshopCatalogueClient Client(WorkflowHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5050/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "founder-session");
        return new HttpWorkshopCatalogueClient(http);
    }

    internal sealed record Request(string Path, byte[] Bytes, string? Authorization);
    internal sealed class WorkflowHandler : HttpMessageHandler
    {
        internal const string Refusal = """{"code":"entity.validation.body_invalid","pointers":["/values/subject"]}""";
        public List<Request> Requests { get; } = [];
        public bool RefuseCreate;
        public bool RefuseValidation;
        public bool IncludeCheck;
        public bool RefuseCheck;
        public bool OmitAuditId;
        public TaskCompletionSource? ExportRelease;
        public TaskCompletionSource? FormRelease;
        public bool RefuseForm;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            Requests.Add(new Request(path, request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken), request.Headers.Authorization?.ToString()));
            if (path == "/api/local-node/catalogue/definitions/FormDefinition/declared.author")
            {
                if (FormRelease is not null) await FormRelease.Task.WaitAsync(cancellationToken);
                if (RefuseForm) return Json("Form temporarily unavailable", HttpStatusCode.ServiceUnavailable);
            }
            if (path == "/api/local-node/packs/export")
            {
                if (ExportRelease is not null) await ExportRelease.Task.WaitAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3, 4]) };
            }
            if (path == "/api/local-node/asset-registry/entities" && RefuseCreate)
                return Json(Refusal, HttpStatusCode.UnprocessableEntity);
            if (path == "/api/local-node/asset-registry/entities" && OmitAuditId)
                return Json("""{"id":"record-1"}""");
            if (path == "/api/local-node/packs/export?validateOnly=true" && RefuseValidation)
                return Json("""{"valid":false,"codes":["pack.render-plan.unsupported-kind"]}""", HttpStatusCode.UnprocessableEntity);
            var response = path switch
            {
                "/api/local-node/catalogue/definitions/ViewDefinition/platform.list.forms" => ViewEntry(IncludeCheck),
                "/api/local-node/catalogue/definitions?kind=FormDefinition" => """{"entries":[],"kindsUnavailable":[]}""",
                "/api/local-node/catalogue/definitions/FormDefinition/declared.author" => FormEntry("declared.author", "1.0.0", "packJson", "textarea"),
                "/api/local-node/catalogue/definitions/FormDefinition/example.capture?version=2.1.0" => FormEntry("example.capture", "2.1.0", "subject", "text"),
                "/api/local-node/packs/export?validateOnly=true" => """{"valid":true,"codes":[]}""",
                "/api/local-node/packs/verify" => """{"verdict":"Verified"}""",
                "/api/local-node/packs/check" => RefuseCheck
                    ? """{"verdict":"WouldInstall","conflicts":[],"watermarkHits":[],"admissionRefusals":[],"refusalCodes":["pack.requirement.unmet"],"refusals":[{"code":"pack.requirement.unmet","pointer":"/requirements/0"}],"crossPackCollisions":[],"unmetContentReferences":[],"unmetDependencies":[]}"""
                    : """{"verdict":"WouldInstall","conflicts":[],"watermarkHits":[],"admissionRefusals":[],"refusalCodes":[],"refusals":[],"crossPackCollisions":[],"unmetContentReferences":[],"unmetDependencies":[]}""",
                "/api/local-node/packs/install" => """{"installed":true}""",
                "/api/local-node/packs/activate" => """{"activated":true,"projectionRefusals":[],"platformRefusals":[]}""",
                "/api/local-node/asset-registry/types/example.record" => """{"displayName":"Entry","propertyForm":{"definition":"example.capture","version":"2.1.0"}}""",
                "/api/local-node/asset-registry/entities" => """{"id":"record-1","auditId":"11111111-1111-1111-1111-111111111111"}""",
                "/api/local-node/asset-registry/entities/record-1" => """{"id":"record-1","values":{"subject":"User supplied value"}}""",
                "/api/local-node/authorization/traces/11111111-1111-1111-1111-111111111111" => """{"availability":"Available","steps":[{"stage":"verdict","facts":["verdict:allowed"]}]}""",
                _ => throw new InvalidOperationException($"Unexpected request: {path}"),
            };
            return Json(response);
        }

        private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static string ViewEntry(bool includeCheck) => """
            {"id":"platform.list.forms","version":"1.0.0","status":"Published","body":{},"renderPlan":{"definitionHash":"hash","definitionId":"platform.list.forms","definitionVersion":"1.0.0","packKey":"platform","packVersion":"1.0.0","definitionKind":"ViewDefinition","bindings":{"viewKind":"views.entity-list/grid","parameters":{"fields":[{"id":"formId","label":"Key"}],"actions":ACTIONS},"actions":ACTIONS}}}
            """.Replace("ACTIONS", includeCheck ? ActionsWithCheck : Actions, StringComparison.Ordinal);

        private static string FormEntry(string id, string version, string field, string hint) => JsonSerializer.Serialize(new
        {
            id, version, status = "Published", body = new { },
            renderPlan = new
            {
                definitionHash = "hash", definitionId = id, definitionVersion = version, packKey = "platform", packVersion = "1.0.0", definitionKind = "FormDefinition",
                bindings = new
                {
                    fields = new Dictionary<string, object> { [field] = new { type = hint, required = true } },
                    overlay = new
                    {
                        title = new { kind = "Literal", value = "Authored title" },
                        fields = new Dictionary<string, object> { [field] = new { label = new { kind = "Literal", value = "Authored field" }, controlHint = hint } },
                        sections = new[] { new { id = "main", title = new { kind = "Literal", value = "Authored section" }, fields = new[] { field } } },
                    },
                },
            },
        });
    }
}
