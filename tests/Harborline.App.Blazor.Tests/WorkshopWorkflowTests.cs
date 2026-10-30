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
    public async Task Refused_write_preserves_safe_canonical_facts_and_actual_value_document_pointers()
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
        Assert.Equal("entity.validation.body_invalid", Assert.Single(invalid.Errors).Message);
        using var result = JsonDocument.Parse(workflow.ResultText!);
        Assert.Equal("validate", result.RootElement.GetProperty("stage").GetString());
        Assert.Equal("/subject", result.RootElement.GetProperty("refusals")[0].GetProperty("pointer").GetString());
        Assert.False(result.RootElement.TryGetProperty("auditId", out _));
        Assert.Null(workflow.Error);
        var count = handler.Requests.Count;
        await workflow.ActivateActionAsync("g");
        Assert.Equal(count, handler.Requests.Count);
    }

    [Theory]
    [InlineData("entity.validation.body_invalid", "")]
    [InlineData("entity.validation.schema_unknown", "/")]
    [InlineData("entity.validation.rule_refused", "/items/0/a~1b~0c")]
    [InlineData("entity.validation.rules_unavailable", "/values/subject")]
    public async Task Canonical_refusals_keep_exact_codes_locations_and_actual_optional_receipt(string code, string pointer)
    {
        var handler = new WorkflowHandler { RefuseCreate = true, RefusalBody = JsonSerializer.Serialize(new
        {
            stage = "validate", refusals = new[] { new { code, pointer } }, auditId = "actual-refusal-receipt",
        }) };
        var workflow = await BoundWorkflow(handler);
        var result = await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "submitted-secret" });
        var error = Assert.Single(result!.Errors);
        Assert.False(result.IsValid);
        Assert.Equal(code, error.Message);
        Assert.Equal(pointer, error.JsonPointer);
        using var diagnostics = JsonDocument.Parse(workflow.ResultText!);
        Assert.Equal("validate", diagnostics.RootElement.GetProperty("stage").GetString());
        Assert.Equal(code, diagnostics.RootElement.GetProperty("refusals")[0].GetProperty("code").GetString());
        Assert.Equal(pointer, diagnostics.RootElement.GetProperty("refusals")[0].GetProperty("pointer").GetString());
        Assert.Equal("actual-refusal-receipt", diagnostics.RootElement.GetProperty("auditId").GetString());
        Assert.DoesNotContain("submitted-secret", workflow.ResultText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Canonical_refusals_preserve_individual_root_and_empty_property_locations()
    {
        var handler = new WorkflowHandler
        {
            RefuseCreate = true,
            RefusalBody = """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":""},{"code":"entity.validation.rule_refused","pointer":"/"}]}""",
        };
        var workflow = await BoundWorkflow(handler);
        var result = await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "value" });
        Assert.Equal(["", "/"], result!.Errors.Select(error => error.JsonPointer));
        Assert.Equal(["entity.validation.body_invalid", "entity.validation.rule_refused"], result.Errors.Select(error => error.Message));
        using var diagnostics = JsonDocument.Parse(workflow.ResultText!);
        Assert.Equal(2, diagnostics.RootElement.GetProperty("refusals").GetArrayLength());
        Assert.False(diagnostics.RootElement.TryGetProperty("auditId", out _));
    }

    public static TheoryData<int, string> InvalidRecordRefusals() => new()
    {
        { 422, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}]}""" },
        { 400, """{"error":"values_object_required","detail":"response-secret"}""" },
        { 403, """{"code":"authorization.permission_required","detail":"response-secret"}""" },
        { 409, "response-secret" }, { 409, "[]" }, { 409, "null" },
        { 409, """{"code":"entity.validation.body_invalid","pointers":["/subject"]}""" },
        { 409, """{"refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}]}""" },
        { 409, """{"stage":null,"refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}]}""" },
        { 409, """{"stage":1,"refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}]}""" },
        { 409, """{"stage":"bind","refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}]}""" },
        { 409, """{"stage":"author","refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}]}""" },
        { 409, """{"stage":"render","refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}]}""" },
        { 409, """{"stage":"Validate","refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}]}""" },
        { 409, """{"stage":"validate"}""" }, { 409, """{"stage":"validate","refusals":[]}""" },
        { 409, """{"stage":"validate","refusals":{}}""" }, { 409, """{"stage":"validate","refusals":[null]}""" },
        { 409, """{"stage":"validate","refusals":[{"code":42,"pointer":"/subject"}]}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"","pointer":"/subject"}]}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"Entity.validation.body_invalid","pointer":"/subject"}]}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid.extra","pointer":"/subject"}]}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid"}]}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":null}]}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":1}]}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":"subject"}]}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":"/bad~2escape"}]}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":"/bad~"}]}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"},{}]}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}],"auditId":" "}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}],"auditId":null}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}],"auditId":1}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}],"code":"entity.validation.body_invalid"}""" },
        { 409, """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}],"pointers":["/subject"]}""" },
    };

    [Theory]
    [MemberData(nameof(InvalidRecordRefusals))]
    public async Task Invalid_record_envelopes_are_closed_without_raw_error_or_fabricated_location(int status, string body)
    {
        var handler = new WorkflowHandler { RefuseCreate = true, RefusalBody = body, RefusalStatus = (HttpStatusCode)status };
        var workflow = await BoundWorkflow(handler);
        var result = await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "submitted-secret" });
        Assert.False(result!.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal($"Request failed ({status}).", workflow.Error);
        Assert.Null(workflow.ResultText);
        var count = handler.Requests.Count;
        await workflow.ActivateActionAsync("g");
        Assert.Equal(count, handler.Requests.Count);
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("reopen")]
    [InlineData("refuse")]
    [InlineData("read-failure")]
    [InlineData("verify")]
    [InlineData("install")]
    [InlineData("activate")]
    public async Task Invalidation_or_refusal_clears_completed_record_and_stale_trace(string invalidation)
    {
        var handler = new WorkflowHandler();
        var workflow = await BoundWorkflow(handler);
        await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "accepted value" });
        await workflow.ActivateActionAsync("g");
        Assert.Contains("verdict:allowed", workflow.ResultText, StringComparison.Ordinal);
        switch (invalidation)
        {
            case "edit": workflow.ChangeValues(new Dictionary<string, object?> { ["subject"] = "edited value" }); break;
            case "reopen": await workflow.ActivateActionAsync("f"); break;
            case "verify": await workflow.ActivateActionAsync("c"); break;
            case "install": await workflow.ActivateActionAsync("d"); break;
            case "activate": await workflow.ActivateActionAsync("e"); break;
            case "refuse":
                handler.RefuseCreate = true;
                await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "" });
                break;
            case "read-failure":
                handler.RefuseTrace = true;
                await workflow.ActivateActionAsync("g");
                Assert.Equal("Request failed (500).", workflow.Error);
                break;
        }
        Assert.DoesNotContain("verdict:allowed", workflow.ResultText ?? "", StringComparison.Ordinal);
        Assert.DoesNotContain("response-secret", workflow.Error ?? "", StringComparison.Ordinal);
        var count = handler.Requests.Count;
        await workflow.ActivateActionAsync("g");
        Assert.Equal(count, handler.Requests.Count);
        Assert.Null(workflow.ResultText);
    }

    [Theory]
    [InlineData("a", false)]
    [InlineData("a", true)]
    [InlineData("b", false)]
    [InlineData("b", true)]
    public async Task Pack_reopen_and_export_invalidate_record_evidence_before_await_even_when_the_request_fails(string action, bool refuse)
    {
        var handler = new WorkflowHandler();
        var workflow = await BoundWorkflow(handler);
        Assert.True((await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "accepted value" }))!.IsValid);
        await workflow.ActivateActionAsync("g");
        Assert.Contains("verdict:allowed", workflow.ResultText, StringComparison.Ordinal);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (action == "a") { handler.FormRelease = release; handler.RefuseForm = refuse; }
        else { handler.ExportRelease = release; handler.RefuseExport = refuse; }
        var pending = workflow.ActivateActionAsync(action);
        Assert.True(workflow.Busy);
        Assert.Null(workflow.ResultText);
        var count = handler.Requests.Count;
        await workflow.ActivateActionAsync("g");
        Assert.Equal(count, handler.Requests.Count);
        release.SetResult();
        await pending;
        Assert.False(workflow.Busy);
        Assert.Equal(refuse, workflow.Error is not null);
        foreach (var stale in new[] { "record-1", "11111111-1111-1111-1111-111111111111", "verdict:allowed" })
            Assert.DoesNotContain(stale, workflow.ResultText ?? "", StringComparison.Ordinal);
        count = handler.Requests.Count;
        await workflow.ActivateActionAsync("g");
        Assert.Equal(count, handler.Requests.Count);
        Assert.Equal("Request refused.", workflow.Error);
        Assert.Null(workflow.ResultText);
        Assert.Single(handler.Requests, request => request.Path == "/api/local-node/asset-registry/entities/record-1");
        Assert.Single(handler.Requests, request => request.Path == "/api/local-node/authorization/traces/11111111-1111-1111-1111-111111111111");
    }

    public static TheoryData<int, string> UndecodableAcceptedResponses => new()
    {
        { 201, "" },
        { 201, "response-secret" },
        { 201, "{\"id\":\"response-secret\"," },
        { 200, "" },
        { 202, "<html>response-secret</html>" },
        { 204, "" },
    };

    [Theory]
    [MemberData(nameof(UndecodableAcceptedResponses))]
    public async Task Undecodable_accepted_record_response_retains_actual_transport_status_without_response_text(int status, string body)
    {
        var handler = new WorkflowHandler { CreateStatus = (HttpStatusCode)status, CreateReceipt = body };
        var exception = await Assert.ThrowsAsync<WorkshopRequestException>(() => Client(handler)
            .PostJsonAsync("api/local-node/asset-registry/entities", new { subject = "submitted-secret" }));
        Assert.Equal((HttpStatusCode)status, exception.StatusCode);
        Assert.Equal(string.Empty, exception.ResponseBody);
        Assert.DoesNotContain("response-secret", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("submitted-secret", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(UndecodableAcceptedResponses))]
    public async Task Undecodable_accepted_record_response_warns_that_record_may_exist_and_never_enables_reads(int status, string body)
    {
        var handler = new WorkflowHandler { CreateStatus = (HttpStatusCode)status, CreateReceipt = body };
        var workflow = await BoundWorkflow(handler);
        var result = await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "submitted-secret" });
        Assert.False(result!.IsValid);
        Assert.Equal("The node accepted the request but returned no complete record and audit receipt. The record may exist; do not submit again without checking.", workflow.Error);
        Assert.Null(workflow.ResultText);
        Assert.Empty(result.Errors);
        var count = handler.Requests.Count;
        await workflow.ActivateActionAsync("g");
        Assert.Equal(count, handler.Requests.Count);
        Assert.DoesNotContain(handler.Requests, request => request.Path.StartsWith("/api/local-node/asset-registry/entities/", StringComparison.Ordinal)
            || request.Path.StartsWith("/api/local-node/authorization/traces/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(202)]
    [InlineData(204)]
    public async Task Unreadable_accepted_record_body_preserves_actual_status_without_private_cause(int status)
    {
        using var content = new BodyContent((_, _) => Task.FromException(new IOException("private-body-read-sentinel")));
        var handler = new WorkflowHandler { CreateStatus = (HttpStatusCode)status, CreateContent = content };
        var exception = await Assert.ThrowsAsync<WorkshopRequestException>(() => Client(handler)
            .PostJsonAsync("api/local-node/asset-registry/entities", new { subject = "submitted-secret" }));
        Assert.Equal((HttpStatusCode)status, exception.StatusCode);
        Assert.Equal(string.Empty, exception.ResponseBody);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("private-body-read-sentinel", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("submitted-secret", exception.ToString(), StringComparison.Ordinal);
        Assert.True(content.Disposed);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(201)]
    [InlineData(202)]
    public async Task Unreadable_accepted_record_body_warns_without_evidence_reads_or_duplicate_submission(int status)
    {
        var handler = new WorkflowHandler { CreateStatus = (HttpStatusCode)status };
        var workflow = await BoundWorkflow(handler);
        handler.CreateContent = new BodyContent((_, _) => Task.FromException(new IOException("private-body-read-sentinel")));
        var result = await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "submitted-secret" });
        Assert.False(result!.IsValid);
        Assert.Equal("The node accepted the request but returned no complete record and audit receipt. The record may exist; do not submit again without checking.", workflow.Error);
        Assert.Empty(result.Errors);
        Assert.Null(workflow.ResultText);
        Assert.Single(handler.Requests, request => request.Path == "/api/local-node/asset-registry/entities");
        var count = handler.Requests.Count;
        await workflow.ActivateActionAsync("g");
        Assert.Equal(count, handler.Requests.Count);
        Assert.DoesNotContain(handler.Requests, request => request.Path.StartsWith("/api/local-node/asset-registry/entities/", StringComparison.Ordinal)
            || request.Path.StartsWith("/api/local-node/authorization/traces/", StringComparison.Ordinal));
        Assert.False(workflow.Busy);
    }

    [Fact]
    public async Task Preheader_exception_status_is_not_an_accepted_record_response()
    {
        var failure = new HttpRequestException("private-preheader-sentinel", null, HttpStatusCode.Created);
        var handler = new WorkflowHandler { CreateFailure = failure };
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => Client(handler)
            .PostJsonAsync("api/local-node/asset-registry/entities", new { }));
        Assert.Same(failure, exception);
        handler.CreateFailure = null;
        var workflow = await BoundWorkflow(handler);
        handler.CreateFailure = failure;
        var result = await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "submitted-secret" });
        Assert.False(result!.IsValid);
        Assert.Equal("Request failed (201).", workflow.Error);
        Assert.Empty(result.Errors);
        Assert.Null(workflow.ResultText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unreadable_nonaccepted_record_or_pack_body_retains_existing_transport_failure(bool pack)
    {
        var content = new BodyContent((_, _) => Task.FromException(new IOException("private-body-read-sentinel")));
        var handler = new WorkflowHandler { CreateStatus = HttpStatusCode.Conflict };
        if (pack) handler.ValidationContent = content;
        else handler.CreateContent = content;
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => Client(handler).PostJsonAsync(
            pack ? "api/local-node/packs/export?validateOnly=true" : "api/local-node/asset-registry/entities", new { }));
        Assert.Null(exception.StatusCode);
        Assert.IsType<IOException>(exception.InnerException);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task Accepted_record_charset_failure_keeps_status_without_the_invalid_charset()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        content.Headers.ContentType!.CharSet = "private-invalid-charset";
        var handler = new WorkflowHandler { CreateContent = content };
        var exception = await Assert.ThrowsAsync<WorkshopRequestException>(() => Client(handler)
            .PostJsonAsync("api/local-node/asset-registry/entities", new { }));
        Assert.Equal(HttpStatusCode.Created, exception.StatusCode);
        Assert.Equal(string.Empty, exception.ResponseBody);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("private-invalid-charset", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Record_request_retains_configured_protocol_and_response_buffer_limit()
    {
        var handler = new WorkflowHandler();
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5050/"),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            MaxResponseContentBufferSize = 8,
        };
        var exception = await Assert.ThrowsAsync<WorkshopRequestException>(() => new HttpWorkshopCatalogueClient(http)
            .PostJsonAsync("api/local-node/asset-registry/entities", new { }));
        Assert.Equal(HttpStatusCode.Created, exception.StatusCode);
        Assert.Equal(string.Empty, exception.ResponseBody);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Version(2, 0), request.Version);
        Assert.Equal(HttpVersionPolicy.RequestVersionOrHigher, request.VersionPolicy);
    }

    [Theory]
    [InlineData(201, false)]
    [InlineData(201, true)]
    [InlineData(409, true)]
    public async Task Caller_cancellation_wins_over_body_completion_or_transport_failure(int status, bool fail)
    {
        using var cancellation = new CancellationTokenSource();
        var content = new BodyContent(async (stream, token) =>
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes("{\"id\":\"record-1\",\"auditId\":\"audit\"}"), token);
            cancellation.Cancel();
            if (fail) throw new IOException("private-body-read-sentinel");
        });
        var handler = new WorkflowHandler { CreateStatus = (HttpStatusCode)status, CreateContent = content };
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler)
            .PostJsonAsync("api/local-node/asset-registry/entities", new { }, cancellation.Token));
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task Configured_request_timeout_still_covers_accepted_body_reading()
    {
        using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reading = false;
        var content = new BodyContent((_, token) => { reading = true; return Task.Delay(Timeout.InfiniteTimeSpan, token); });
        var handler = new WorkflowHandler { CreateContent = content };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5050/"), Timeout = TimeSpan.FromSeconds(1) };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new HttpWorkshopCatalogueClient(http)
            .PostJsonAsync("api/local-node/asset-registry/entities", new { }, guard.Token));
        Assert.False(guard.IsCancellationRequested);
        Assert.True(reading);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task Canceled_record_body_read_leaves_workflow_idle_without_acceptance_or_evidence()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new WorkflowHandler();
        var workflow = await BoundWorkflow(handler);
        handler.CreateContent = new BodyContent((_, _) =>
        {
            cancellation.Cancel();
            return Task.FromException(new IOException("private-body-read-sentinel"));
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.SubmitAsync(
            new Dictionary<string, object?> { ["subject"] = "submitted-secret" }, cancellation.Token).AsTask());
        Assert.False(workflow.Busy);
        Assert.Null(workflow.Error);
        Assert.Null(workflow.ResultText);
        Assert.Single(handler.Requests, request => request.Path == "/api/local-node/asset-registry/entities");
    }

    [Fact]
    public async Task Request_serialization_failure_is_not_relabelled_as_an_accepted_record()
    {
        var body = new Dictionary<string, object?>();
        body["cycle"] = body;
        var handler = new WorkflowHandler();
        await Assert.ThrowsAsync<JsonException>(() => Client(handler).PostJsonAsync("api/local-node/asset-registry/entities", body));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not JSON")]
    [InlineData("{\"valid\":")]
    public async Task Undecodable_successful_pack_validation_retains_its_existing_json_failure(string body)
    {
        var handler = new WorkflowHandler { ValidationBody = body };
        await Assert.ThrowsAsync<JsonException>(() => Client(handler)
            .PostJsonAsync("api/local-node/packs/export?validateOnly=true", new { }));
    }

    [Fact]
    public async Task Record_transport_failure_does_not_claim_acceptance()
    {
        var handler = new WorkflowHandler();
        var workflow = await BoundWorkflow(handler);
        handler.FailCreateTransport = true;
        var result = await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "value" });
        Assert.False(result!.IsValid);
        Assert.Equal("Request refused.", workflow.Error);
        Assert.Null(workflow.ResultText);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("""{"id":"","auditId":"audit","detail":"response-secret"}""")]
    [InlineData("""{"id":"record-1","auditId":" ","detail":"response-secret"}""")]
    [InlineData("""{"id":42,"auditId":"audit","detail":"response-secret"}""")]
    public async Task Incomplete_accepted_receipt_does_not_complete_or_expose_extra_data(string receipt)
    {
        var handler = new WorkflowHandler { CreateReceipt = receipt };
        var workflow = await BoundWorkflow(handler);
        Assert.False((await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "value" }))!.IsValid);
        Assert.Contains("record may exist", workflow.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("response-secret", workflow.ResultText ?? "", StringComparison.Ordinal);
        var count = handler.Requests.Count;
        await workflow.ActivateActionAsync("g");
        Assert.Equal(count, handler.Requests.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pending_record_create_or_read_ignores_edits_and_concurrent_actions_until_completion(bool reading)
    {
        var handler = new WorkflowHandler();
        var workflow = await BoundWorkflow(handler);
        var values = new Dictionary<string, object?> { ["subject"] = "pending value" };
        if (reading) await workflow.SubmitAsync(values);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (reading) handler.TraceRelease = release;
        else handler.CreateRelease = release;
        Task pending = reading ? workflow.ActivateActionAsync("g") : workflow.SubmitAsync(values).AsTask();
        Assert.True(workflow.Busy);
        var count = handler.Requests.Count;
        workflow.ChangeValues(new Dictionary<string, object?> { ["subject"] = "later edit" });
        foreach (var action in new[] { "a", "b", "c", "d", "e", "f", "g" }) await workflow.ActivateActionAsync(action);
        Assert.False((await workflow.SubmitAsync(new Dictionary<string, object?> { ["subject"] = "concurrent submit" }))!.IsValid);
        Assert.Equal("pending value", workflow.Values["subject"]);
        Assert.Equal(count, handler.Requests.Count);
        release.SetResult();
        await pending;
        Assert.False(workflow.Busy);
        Assert.Contains(reading ? "verdict:allowed" : "record-1", workflow.ResultText, StringComparison.Ordinal);
        workflow.ChangeValues(new Dictionary<string, object?> { ["subject"] = "later edit" });
        Assert.Null(workflow.ResultText);
        count = handler.Requests.Count;
        await workflow.ActivateActionAsync("g");
        Assert.Equal(count, handler.Requests.Count);
    }

    internal static async Task<WorkshopWorkflow> BoundWorkflow(WorkflowHandler handler)
    {
        var client = Client(handler);
        var workflow = new WorkshopWorkflow(client);
        workflow.Bind(await client.ReadViewAsync("platform.list.forms"));
        await workflow.ActivateActionAsync("a");
        await workflow.SubmitAsync(new Dictionary<string, object?> { ["packJson"] = PackJson });
        foreach (var id in new[] { "b", "c", "d", "e", "f" }) await workflow.ActivateActionAsync(id);
        return workflow;
    }

    internal static HttpWorkshopCatalogueClient Client(WorkflowHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5050/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "founder-session");
        return new HttpWorkshopCatalogueClient(http);
    }

    private sealed class BodyContent(Func<Stream, CancellationToken, Task> write) : HttpContent
    {
        public bool Disposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => write(stream, CancellationToken.None);
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) => write(stream, cancellationToken);
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    internal sealed record Request(string Path, byte[] Bytes, string? Authorization, Version Version, HttpVersionPolicy VersionPolicy);
    internal sealed class WorkflowHandler : HttpMessageHandler
    {
        internal const string Refusal = """{"stage":"validate","refusals":[{"code":"entity.validation.body_invalid","pointer":"/subject"}]}""";
        public List<Request> Requests { get; } = [];
        public bool RefuseCreate;
        public string RefusalBody = Refusal;
        public HttpStatusCode RefusalStatus = HttpStatusCode.Conflict;
        public HttpStatusCode CreateStatus = HttpStatusCode.Created;
        public string? CreateReceipt;
        public string? ValidationBody;
        public HttpContent? CreateContent;
        public HttpContent? ValidationContent;
        public HttpRequestException? CreateFailure;
        public bool FailCreateTransport;
        public bool RefuseTrace;
        public bool RefuseValidation;
        public bool RefuseExport;
        public bool OmitAuditId;
        public TaskCompletionSource? ExportRelease;
        public TaskCompletionSource? FormRelease;
        public TaskCompletionSource? CreateRelease;
        public TaskCompletionSource? TraceRelease;
        public bool RefuseForm;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            Requests.Add(new Request(path, request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken),
                request.Headers.Authorization?.ToString(), request.Version, request.VersionPolicy));
            if (path == "/api/local-node/catalogue/definitions/FormDefinition/declared.author")
            {
                if (FormRelease is not null) await FormRelease.Task.WaitAsync(cancellationToken);
                if (RefuseForm) return Json("Form temporarily unavailable", HttpStatusCode.ServiceUnavailable);
            }
            if (path == "/api/local-node/packs/export")
            {
                if (ExportRelease is not null) await ExportRelease.Task.WaitAsync(cancellationToken);
                if (RefuseExport) return Json("Export temporarily unavailable", HttpStatusCode.ServiceUnavailable);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3, 4]) };
            }
            if (path == "/api/local-node/asset-registry/entities" && CreateRelease is not null)
                await CreateRelease.Task.WaitAsync(cancellationToken);
            if (path.StartsWith("/api/local-node/authorization/traces/", StringComparison.Ordinal) && TraceRelease is not null)
                await TraceRelease.Task.WaitAsync(cancellationToken);
            if (path == "/api/local-node/asset-registry/entities" && FailCreateTransport)
                throw new HttpRequestException("transport-secret");
            if (path == "/api/local-node/asset-registry/entities" && CreateFailure is not null)
                throw CreateFailure;
            if (path == "/api/local-node/asset-registry/entities" && CreateContent is not null)
                return new HttpResponseMessage(CreateStatus) { Content = CreateContent };
            if (path == "/api/local-node/packs/export?validateOnly=true" && ValidationContent is not null)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = ValidationContent };
            if (path == "/api/local-node/asset-registry/entities" && RefuseCreate)
                return Json(RefusalBody, RefusalStatus);
            if (path == "/api/local-node/asset-registry/entities" && CreateReceipt is not null)
                return Json(CreateReceipt, CreateStatus);
            if (path.StartsWith("/api/local-node/authorization/traces/", StringComparison.Ordinal) && RefuseTrace)
                return Json("response-secret", HttpStatusCode.InternalServerError);
            if (path == "/api/local-node/asset-registry/entities" && OmitAuditId)
                return Json("""{"id":"record-1"}""", CreateStatus);
            if (path == "/api/local-node/packs/export?validateOnly=true" && RefuseValidation)
                return Json("""{"valid":false,"codes":["pack.render-plan.unsupported-kind"]}""", HttpStatusCode.UnprocessableEntity);
            var response = path switch
            {
                "/api/local-node/catalogue/definitions/ViewDefinition/platform.list.forms" => ViewEntry(),
                "/api/local-node/catalogue/definitions?kind=FormDefinition" => """{"entries":[],"kindsUnavailable":[]}""",
                "/api/local-node/catalogue/definitions/FormDefinition/declared.author" => FormEntry("declared.author", "1.0.0", "packJson", "textarea"),
                "/api/local-node/catalogue/definitions/FormDefinition/example.capture?version=2.1.0" => FormEntry("example.capture", "2.1.0", "subject", "text"),
                "/api/local-node/packs/export?validateOnly=true" => ValidationBody ?? """{"valid":true,"codes":[]}""",
                "/api/local-node/packs/verify" => """{"verdict":"Verified"}""",
                "/api/local-node/packs/install" => """{"installed":true}""",
                "/api/local-node/packs/activate" => """{"activated":true,"projectionRefusals":[],"platformRefusals":[]}""",
                "/api/local-node/asset-registry/types/example.record" => """{"displayName":"Entry","propertyForm":{"definition":"example.capture","version":"2.1.0"}}""",
                "/api/local-node/asset-registry/entities" => """{"id":"record-1","auditId":"11111111-1111-1111-1111-111111111111"}""",
                "/api/local-node/asset-registry/entities/record-1" => """{"id":"record-1","values":{"subject":"User supplied value"}}""",
                "/api/local-node/authorization/traces/11111111-1111-1111-1111-111111111111" => """{"availability":"Available","steps":[{"stage":"verdict","facts":["verdict:allowed"]}]}""",
                _ => throw new InvalidOperationException($"Unexpected request: {path}"),
            };
            return Json(response, path == "/api/local-node/asset-registry/entities" ? CreateStatus : HttpStatusCode.OK);
        }

        private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static string ViewEntry() => """
            {"id":"platform.list.forms","version":"1.0.0","status":"Published","body":{},"renderPlan":{"definitionHash":"hash","definitionId":"platform.list.forms","definitionVersion":"1.0.0","packKey":"platform","packVersion":"1.0.0","definitionKind":"ViewDefinition","bindings":{"viewKind":"layout.table","parameters":{"fields":[{"id":"formId","label":"Key"}],"actions":ACTIONS},"actions":ACTIONS}}}
            """.Replace("ACTIONS", Actions, StringComparison.Ordinal);

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
