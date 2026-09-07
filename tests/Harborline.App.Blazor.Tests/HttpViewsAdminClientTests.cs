using System.Net;
using System.Text;
using Harborline.App.Blazor.ReferenceHost.Admin.Views;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// Verifies the HTTP views administration client's wire contract and error mapping.
/// </summary>
public sealed class HttpViewsAdminClientTests
{
    [Fact]
    public async Task List_definitions_maps_the_wire_rows()
    {
        const string json = """
            [
              {"key":"work-orders-table","version":"2.1.0","title":"Work orders table","viewKind":"table","cascadeLayer":"Tenant"},
              {"key":"occupancy-board","version":"0.3.0","title":"Occupancy board","viewKind":"board","cascadeLayer":"Tenant"}
            ]
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));
        var client = CreateClient(handler);

        var rows = await client.ListDefinitionsAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/views/definitions", request.Uri.AbsoluteUri);
        Assert.Equal(
            [
                new ViewDefinitionSummary("work-orders-table", "2.1.0", "Work orders table", "table", "Tenant"),
                new ViewDefinitionSummary("occupancy-board", "0.3.0", "Occupancy board", "board", "Tenant"),
            ],
            rows);
    }

    [Fact]
    public async Task Get_definition_escapes_the_key_and_maps_the_detail()
    {
        const string json = """
            {"key":"a b/c","version":"3.0.0","title":"Escaped report","viewKind":"table","cascadeLayer":"Tenant","schemaVersion":3,"parameters":{"entityType":"work-order"},"provenance":{"tier":"Vendor"}}
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));
        var client = CreateClient(handler);

        var detail = await client.GetDefinitionAsync("a b/c");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/views/definitions/a%20b%2Fc", request.Uri.AbsoluteUri);
        Assert.Equal("a b/c", detail.Key);
        Assert.Equal("3.0.0", detail.Version);
        Assert.Equal("Escaped report", detail.Title);
        Assert.Equal("table", detail.ViewKind);
        Assert.Equal("Tenant", detail.CascadeLayer);
        Assert.Equal(3, detail.SchemaVersion);
        Assert.Equal("work-order", detail.Parameters.GetProperty("entityType").GetString());
        Assert.Equal("Vendor", detail.Provenance.GetProperty("tier").GetString());
    }

    [Fact]
    public async Task List_versions_maps_the_ordering_and_rows()
    {
        const string json = """
            {"ordering":"ordinal","versions":[
              {"key":"occupancy-board","version":"2024-legacy","title":"Occupancy board","viewKind":"board","cascadeLayer":"Tenant"},
              {"key":"occupancy-board","version":"0.3.0","title":"Occupancy board","viewKind":"board","cascadeLayer":"Tenant"}
            ]}
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));
        var client = CreateClient(handler);

        var history = await client.ListVersionsAsync("occupancy-board");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/views/definitions/occupancy-board/versions", request.Uri.AbsoluteUri);
        Assert.Equal("ordinal", history.Ordering);
        Assert.Equal(["2024-legacy", "0.3.0"], history.Versions.Select(row => row.Version));
    }

    [Fact]
    public async Task A_plain_code_envelope_maps_to_the_bare_code()
    {
        // Ticket 094: `{ code }` with no detail — the message is the code itself.
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.NotFound, "{\"code\":\"view_definition.not_found\"}")));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<ViewsAdminException>(() => client.GetDefinitionAsync("nope"));

        Assert.Equal(404, error.Status);
        Assert.Equal("view_definition.not_found", error.Message);
        Assert.Equal("view_definition.not_found", error.Code);
        Assert.Null(error.Detail);
    }

    [Fact]
    public async Task A_parameterized_diagnostic_carries_its_named_detail_fields()
    {
        // Ticket 094: a `detail` object of named fields renders after the code.
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.BadRequest,
            "{\"code\":\"view_definition.version_not_found\",\"detail\":{\"key\":\"occupancy\",\"maxLength\":200}}")));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<ViewsAdminException>(() => client.ListDefinitionsAsync());

        Assert.Equal(400, error.Status);
        Assert.Equal("view_definition.version_not_found (key=occupancy, maxLength=200)", error.Message);
        Assert.Equal("view_definition.version_not_found", error.Code);
        Assert.Equal("occupancy", error.Detail!["key"]);
        Assert.Equal("200", error.Detail!["maxLength"]);
    }

    [Fact]
    public async Task Bodies_without_a_code_fall_back_to_the_reason_phrase()
    {
        // The retired `{ error: "..." }` shape and a non-JSON body both degrade to the reason phrase.
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse(HttpStatusCode.NotFound, "{\"error\":\"No definition 'nope' for this tenant.\"}"),
            new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("upstream unavailable", Encoding.UTF8, "text/plain"),
                ReasonPhrase = "Bad Gateway",
            },
        ]);
        var handler = new RecordingHandler(_ => Task.FromResult(responses.Dequeue()));
        var client = CreateClient(handler);

        var legacyError = await Assert.ThrowsAsync<ViewsAdminException>(() => client.GetDefinitionAsync("nope"));
        var textError = await Assert.ThrowsAsync<ViewsAdminException>(() => client.ListDefinitionsAsync());

        Assert.Equal(404, legacyError.Status);
        Assert.Equal("Not Found", legacyError.Message);
        Assert.Null(legacyError.Code);
        Assert.Null(legacyError.Detail);
        Assert.Equal(502, textError.Status);
        Assert.Equal("Bad Gateway", textError.Message);
    }

    private static HttpViewsAdminClient CreateClient(RecordingHandler handler) =>
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
