using System.Net;
using System.Text;
using Harborline.App.Blazor.ReferenceHost.Admin.DataExchange;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// Verifies the HTTP data exchange administration client's wire contract and error mapping.
/// </summary>
public sealed class HttpDataExchangeAdminClientTests
{
    [Fact]
    public async Task List_definitions_maps_the_wire_rows()
    {
        const string json = """
            [
              {"key":"bank-feed-import","version":"2.1.0","title":"Bank feed import","exchangeKind":"import","cascadeLayer":"Tenant"},
              {"key":"legacy-ledger-export","version":"0.3.0","title":"Legacy ledger export","exchangeKind":"export","cascadeLayer":"Tenant"}
            ]
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));
        var client = CreateClient(handler);

        var rows = await client.ListDefinitionsAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/data-exchange/definitions", request.Uri.AbsoluteUri);
        Assert.Equal(
            [
                new DataExchangeDefinitionSummary("bank-feed-import", "2.1.0", "Bank feed import", "import", "Tenant"),
                new DataExchangeDefinitionSummary("legacy-ledger-export", "0.3.0", "Legacy ledger export", "export", "Tenant"),
            ],
            rows);
    }

    [Fact]
    public async Task Get_definition_escapes_the_key_and_maps_the_detail()
    {
        const string json = """
            {"key":"a b/c","version":"3.0.0","title":"Escaped report","exchangeKind":"import","cascadeLayer":"Tenant","schemaVersion":3,"settings":{"mapping":"csv-standard"},"provenance":{"tier":"Vendor"}}
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));
        var client = CreateClient(handler);

        var detail = await client.GetDefinitionAsync("a b/c");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/data-exchange/definitions/a%20b%2Fc", request.Uri.AbsoluteUri);
        Assert.Equal("a b/c", detail.Key);
        Assert.Equal("3.0.0", detail.Version);
        Assert.Equal("Escaped report", detail.Title);
        Assert.Equal("import", detail.ExchangeKind);
        Assert.Equal("Tenant", detail.CascadeLayer);
        Assert.Equal(3, detail.SchemaVersion);
        Assert.Equal("csv-standard", detail.Settings.GetProperty("mapping").GetString());
        Assert.Equal("Vendor", detail.Provenance.GetProperty("tier").GetString());
    }

    [Fact]
    public async Task List_versions_maps_the_ordering_and_rows()
    {
        const string json = """
            {"ordering":"ordinal","versions":[
              {"key":"legacy-ledger-export","version":"2024-legacy","title":"Legacy ledger export","exchangeKind":"export","cascadeLayer":"Tenant"},
              {"key":"legacy-ledger-export","version":"0.3.0","title":"Legacy ledger export","exchangeKind":"export","cascadeLayer":"Tenant"}
            ]}
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));
        var client = CreateClient(handler);

        var history = await client.ListVersionsAsync("legacy-ledger-export");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/data-exchange/definitions/legacy-ledger-export/versions", request.Uri.AbsoluteUri);
        Assert.Equal("ordinal", history.Ordering);
        Assert.Equal(["2024-legacy", "0.3.0"], history.Versions.Select(row => row.Version));
    }

    [Fact]
    public async Task A_plain_code_envelope_maps_to_the_bare_code()
    {
        // Ticket 094: `{ code }` with no detail — the message is the code itself.
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.NotFound, "{\"code\":\"data_exchange_definition.not_found\"}")));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<DataExchangeAdminException>(() => client.GetDefinitionAsync("nope"));

        Assert.Equal(404, error.Status);
        Assert.Equal("data_exchange_definition.not_found", error.Message);
        Assert.Equal("data_exchange_definition.not_found", error.Code);
        Assert.Null(error.Detail);
    }

    [Fact]
    public async Task A_parameterized_diagnostic_carries_its_named_detail_fields()
    {
        // Ticket 094: a `detail` object of named fields renders after the code.
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.BadRequest,
            "{\"code\":\"data_exchange_definition.version_not_found\",\"detail\":{\"key\":\"occupancy\",\"maxLength\":200}}")));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<DataExchangeAdminException>(() => client.ListDefinitionsAsync());

        Assert.Equal(400, error.Status);
        Assert.Equal("data_exchange_definition.version_not_found (key=occupancy, maxLength=200)", error.Message);
        Assert.Equal("data_exchange_definition.version_not_found", error.Code);
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

        var legacyError = await Assert.ThrowsAsync<DataExchangeAdminException>(() => client.GetDefinitionAsync("nope"));
        var textError = await Assert.ThrowsAsync<DataExchangeAdminException>(() => client.ListDefinitionsAsync());

        Assert.Equal(404, legacyError.Status);
        Assert.Equal("Not Found", legacyError.Message);
        Assert.Null(legacyError.Code);
        Assert.Null(legacyError.Detail);
        Assert.Equal(502, textError.Status);
        Assert.Equal("Bad Gateway", textError.Message);
    }

    private static HttpDataExchangeAdminClient CreateClient(RecordingHandler handler) =>
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
