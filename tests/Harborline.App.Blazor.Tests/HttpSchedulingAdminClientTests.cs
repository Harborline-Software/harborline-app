using System.Net;
using System.Text;
using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Admin.Scheduling;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// Verifies the HTTP scheduling administration client's wire contract and error mapping.
/// </summary>
public sealed class HttpSchedulingAdminClientTests
{
    [Fact]
    public async Task List_definitions_maps_wire_rows()
    {
        const string json = """
            [{"id":"inspection-protocol","revision":3,"title":"Inspection protocol","updatedAt":"2026-08-18T09:00:00Z","updatedBy":"user:fixture-scheduler"}]
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));
        var client = CreateClient(handler);

        var rows = await client.ListDefinitionsAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/scheduling/definitions", request.Uri.AbsoluteUri);
        var row = Assert.Single(rows);
        Assert.Equal("inspection-protocol", row.Id);
        Assert.Equal(3, row.Revision);
        Assert.Equal("Inspection protocol", row.Title);
        Assert.Equal(DateTimeOffset.Parse("2026-08-18T09:00:00Z"), row.UpdatedAt);
        Assert.Equal("user:fixture-scheduler", row.UpdatedBy);
    }

    [Fact]
    public async Task Get_definition_escapes_the_id_and_maps_the_view()
    {
        const string json = """
            {"id":"a b/c","revision":3,"definition":{"title":"Inspection protocol","cadence":"weekly"},"updatedAt":"2026-08-18T09:00:00Z","updatedBy":"user:fixture-scheduler"}
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));
        var client = CreateClient(handler);

        var definition = await client.GetDefinitionAsync("a b/c");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/scheduling/definitions/a%20b%2Fc", request.Uri.AbsoluteUri);
        Assert.Equal(3, definition.Revision);
        Assert.Equal("Inspection protocol", definition.Definition.GetProperty("title").GetString());
    }

    [Fact]
    public async Task List_versions_maps_wire_rows()
    {
        const string json = """
            [{"id":"inspection-protocol","revision":2,"title":"Inspection protocol","updatedAt":"2026-08-12T10:00:00Z","updatedBy":"user:fixture-scheduler"}]
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));
        var client = CreateClient(handler);

        var rows = await client.ListVersionsAsync("inspection-protocol");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/scheduling/definitions/inspection-protocol/versions", request.Uri.AbsoluteUri);
        var row = Assert.Single(rows);
        Assert.Equal(2, row.Revision);
        Assert.Equal("Inspection protocol", row.Title);
        Assert.Equal("user:fixture-scheduler", row.UpdatedBy);
    }

    [Fact]
    public async Task Restore_posts_the_selected_revision_and_maps_the_result()
    {
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            "{\"definitionId\":\"inspection-protocol\",\"revision\":4,\"restoredFrom\":1}")));
        var client = CreateClient(handler);

        var result = await client.RestoreRevisionAsync("inspection-protocol", 1);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://localhost:5000/api/local-node/scheduling/definitions/inspection-protocol/restore", request.Uri.AbsoluteUri);
        using var body = JsonDocument.Parse(request.Body!);
        var property = Assert.Single(body.RootElement.EnumerateObject());
        Assert.Equal("revision", property.Name);
        Assert.Equal(JsonValueKind.Number, property.Value.ValueKind);
        Assert.Equal(1, property.Value.GetInt32());
        Assert.Equal(new RestoreResult("inspection-protocol", 4, 1), result);
    }

    [Fact]
    public async Task A_plain_code_envelope_maps_to_the_bare_code()
    {
        var handler = new RecordingHandler(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.NotFound, "{\"code\":\"scheduling.draft.revision_not_found\"}")));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<SchedulingAdminException>(
            () => client.RestoreRevisionAsync("inspection-protocol", 99));

        Assert.Equal(404, error.Status);
        Assert.Equal("scheduling.draft.revision_not_found", error.Message);
        Assert.Equal("scheduling.draft.revision_not_found", error.Code);
        Assert.Null(error.Detail);
    }

    [Fact]
    public async Task A_parameterized_diagnostic_carries_its_named_detail_fields()
    {
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.BadRequest,
            "{\"code\":\"scheduling.definition.invalid\",\"detail\":{\"key\":\"occupancy\",\"maxLength\":200}}")));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<SchedulingAdminException>(
            () => client.RestoreRevisionAsync("inspection-protocol", 99));

        Assert.Equal("scheduling.definition.invalid (key=occupancy, maxLength=200)", error.Message);
        Assert.Equal("scheduling.definition.invalid", error.Code);
        Assert.NotNull(error.Detail);
        Assert.Equal("occupancy", error.Detail!["key"]);
        Assert.Equal("200", error.Detail!["maxLength"]);
    }

    [Fact]
    public async Task Bodies_without_a_code_fall_back_to_the_reason_phrase()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse(HttpStatusCode.NotFound, "{\"error\":\"Scheduling definition not found.\"}"),
            new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("upstream unavailable", Encoding.UTF8, "text/plain"),
                ReasonPhrase = "Bad Gateway",
            },
        ]);
        var handler = new RecordingHandler(_ => Task.FromResult(responses.Dequeue()));
        var client = CreateClient(handler);

        var proseError = await Assert.ThrowsAsync<SchedulingAdminException>(
            () => client.RestoreRevisionAsync("inspection-protocol", 99));
        var textError = await Assert.ThrowsAsync<SchedulingAdminException>(
            () => client.GetDefinitionAsync("inspection-protocol"));

        Assert.Equal("Not Found", proseError.Message);
        Assert.Null(proseError.Code);
        Assert.Null(proseError.Detail);
        Assert.Equal(502, textError.Status);
        Assert.Equal("Bad Gateway", textError.Message);
    }

    [Fact]
    public async Task List_reports_an_unmapped_family_rather_than_an_anonymous_404()
    {
        // The node registers the scheduling family only behind LocalNode:SchedulingDogfood:Enabled,
        // false in shipped config — so this 404 means "flip the flag", not "no definitions". An
        // empty tenant answers 200 [], which is why the two are safely distinguishable.
        var handler = new RecordingHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<SchedulingAdminException>(
            () => client.ListDefinitionsAsync());

        Assert.Equal(404, error.Status);
        Assert.Equal("scheduling.family_not_enabled", error.Message);
    }

    [Fact]
    public async Task Detail_404_is_left_alone_only_the_list_route_means_unmapped()
    {
        var handler = new RecordingHandler(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.NotFound, "{\"code\":\"scheduling.draft.not_found\"}")));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<SchedulingAdminException>(
            () => client.GetDefinitionAsync("nope"));

        Assert.Equal("scheduling.draft.not_found", error.Message);
    }

    private static HttpSchedulingAdminClient CreateClient(RecordingHandler handler) =>
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
