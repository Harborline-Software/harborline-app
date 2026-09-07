using System.Net;
using System.Text;
using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Admin.Forms;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// Verifies the HTTP forms administration client's wire contract and error mapping.
/// </summary>
public sealed class HttpFormsAdminClientTests
{
    [Fact]
    public async Task List_definitions_maps_wire_rows_and_reads_the_cascade_layer_off_the_wire()
    {
        // Ticket 153: cascadeLayer is server-supplied — mapped verbatim, absent key -> null,
        // never fabricated client-side.
        const string json = """
            [
              {"formId":"incident-intake","version":"1.0.3","title":{"defaultLocale":"en","values":{"en":"Incident intake"}},"updatedAt":"2026-08-05T12:00:00Z","cascadeLayer":"Pack"},
              {"formId":"crew-manifest","version":"1.0.0","updatedAt":"2026-08-03T08:00:00Z"}
            ]
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));
        var client = CreateClient(handler);

        var rows = await client.ListDefinitionsAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/forms/definitions", request.Uri.AbsoluteUri);
        Assert.Equal("Incident intake", rows[0].Title!.Values["en"]);
        Assert.Null(rows[1].Title);
        Assert.Equal("Pack", rows[0].CascadeLayer);
        Assert.Null(rows[1].CascadeLayer);
    }

    [Fact]
    public async Task List_versions_escapes_the_form_id_and_maps_optional_fields()
    {
        const string json = """
            [{"formId":"a b/c","version":"1.2.3","status":"Draft","createdAt":"2026-08-01T01:02:03Z","updatedAt":"2026-08-02T04:05:06Z","syncsToPeers":true,"safeForStaging":false}]
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));
        var client = CreateClient(handler);

        var rows = await client.ListVersionsAsync("a b/c");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/forms/definitions/a%20b%2Fc/versions", request.Uri.AbsoluteUri);
        var row = Assert.Single(rows);
        Assert.Equal("Draft", row.Status);
        Assert.Null(row.Owner);
        Assert.Null(row.DerivedFrom);
        Assert.Equal(DateTimeOffset.Parse("2026-08-01T01:02:03Z"), row.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-08-02T04:05:06Z"), row.UpdatedAt);
        Assert.True(row.SyncsToPeers);
        Assert.False(row.SafeForStaging);
    }

    [Fact]
    public async Task Restore_posts_the_selected_version_and_maps_the_result()
    {
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            "{\"formId\":\"incident-intake\",\"version\":\"1.0.5\"}")));
        var client = CreateClient(handler);

        var result = await client.RestoreVersionAsync("incident-intake", "1.0.1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/forms/definitions/incident-intake/restore", request.Uri.AbsoluteUri);
        using var body = JsonDocument.Parse(request.Body!);
        var property = Assert.Single(body.RootElement.EnumerateObject());
        Assert.Equal("version", property.Name);
        Assert.Equal("1.0.1", property.Value.GetString());
        Assert.Equal(new RestoreResult("incident-intake", "1.0.5"), result);
    }

    [Fact]
    public async Task A_plain_code_envelope_maps_to_the_bare_code()
    {
        // Ticket 094: `{ code }` with no detail — the message is the code itself.
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.Conflict, "{\"code\":\"form_definition.revision_conflict\"}")));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<FormsAdminException>(
            () => client.RestoreVersionAsync("incident-intake", "1.0.1"));

        Assert.Equal(409, error.Status);
        Assert.Equal("form_definition.revision_conflict", error.Message);
        Assert.Equal("form_definition.revision_conflict", error.Code);
        Assert.Null(error.Detail);
    }

    [Fact]
    public async Task A_parameterized_diagnostic_carries_its_named_detail_fields()
    {
        // Ticket 094: `detail` is an object of named fields (FormsRoutes.cs:294 sends header+maxLength).
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.BadRequest,
            "{\"code\":\"forms.idempotency_key_too_long\",\"detail\":{\"header\":\"Idempotency-Key\",\"maxLength\":200}}")));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<FormsAdminException>(() => client.ListDefinitionsAsync());

        Assert.Equal(400, error.Status);
        Assert.Equal(
            "forms.idempotency_key_too_long (header=Idempotency-Key, maxLength=200)", error.Message);
        Assert.Equal("forms.idempotency_key_too_long", error.Code);
        Assert.Equal("Idempotency-Key", error.Detail!["header"]);
        Assert.Equal("200", error.Detail!["maxLength"]);
    }

    [Fact]
    public async Task Bodies_without_a_code_fall_back_to_the_reason_phrase()
    {
        // The retired `{ error: "..." }` shape and a non-JSON body both degrade to the reason phrase.
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse(HttpStatusCode.NotFound, "{\"error\":\"No revision '9.9.9' of form 'incident-intake' to restore.\"}"),
            new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("upstream unavailable", Encoding.UTF8, "text/plain"),
                ReasonPhrase = "Bad Gateway",
            },
        ]);
        var handler = new RecordingHandler(_ => Task.FromResult(responses.Dequeue()));
        var client = CreateClient(handler);

        var legacyError = await Assert.ThrowsAsync<FormsAdminException>(
            () => client.RestoreVersionAsync("incident-intake", "9.9.9"));
        var textError = await Assert.ThrowsAsync<FormsAdminException>(
            () => client.ListDefinitionsAsync());

        Assert.Equal(404, legacyError.Status);
        Assert.Equal("Not Found", legacyError.Message);
        Assert.Null(legacyError.Code);
        Assert.Null(legacyError.Detail);
        Assert.Equal(502, textError.Status);
        Assert.Equal("Bad Gateway", textError.Message);
    }

    private static HttpFormsAdminClient CreateClient(RecordingHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") });

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body));
            return await respond(request);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body);
}
