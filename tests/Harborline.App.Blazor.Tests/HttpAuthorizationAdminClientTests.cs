using System.Net;
using System.Text;
using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;

namespace Harborline.App.Blazor.Tests;

public sealed class HttpAuthorizationAdminClientTests
{
    [Fact]
    public async Task Exact_routes_and_dtos_preserve_qualified_roles_empty_warning_and_standing_rows()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse(HttpStatusCode.OK, """[{"roleDefinitionId":"11111111-1111-1111-1111-111111111111","role":{"vocabulary":"tax.roles","name":"author"},"displayName":"Tax author","owner":{"kind":"Package","ownerId":"harborline.tax"},"isSealed":false}]"""),
            JsonResponse(HttpStatusCode.OK, """[{"definitionId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","publisherPackageId":"harborline.tax","definitionRevision":3,"atom":{"operation":"tax.write","scopeType":"Tenant","scopeValue":"*"},"offeredRoles":[{"vocabulary":"tax.roles","name":"author"}],"binding":{"revision":4,"effectiveRoles":[],"warning":"EmptyBinding"}}]"""),
            JsonResponse(HttpStatusCode.OK, """{"revision":4,"effectiveRoles":[],"warning":"EmptyBinding"}"""),
            JsonResponse(HttpStatusCode.OK, """[{"ruleId":"standing-rule","ruleVersion":"1","standing":"Filed","declaredRecordType":"tax.return","fields":[{"field":"returnId","carryingRecordTypes":["tax.return","tax.amendment"]}]}]"""),
            JsonResponse(HttpStatusCode.OK, """{"definitionId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","revision":5,"effectiveRoles":[],"warning":"EmptyBinding","changedBy":"admin","changedAt":"2026-09-02T12:00:00Z","reason":"clear"}"""),
        ]);
        var handler = new RecordingHandler(_ => Task.FromResult(responses.Dequeue()));
        var client = CreateClient(handler, () => "request-key");
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var roles = await client.ListRoleVocabularyAsync();
        var definitions = await client.ListCapabilityDefinitionsAsync();
        var binding = await client.GetEffectiveBindingAsync(id);
        var standings = await client.ListStandingCatalogueAsync();
        var narrowed = await client.NarrowCapabilityBindingAsync(id, [], "clear");

        Assert.Equal(new RoleReference("tax.roles", "author"), Assert.Single(roles).Role);
        Assert.Equal("Tax author", roles[0].DisplayName);
        Assert.Equal(BindingWarningCode.EmptyBinding, Assert.Single(definitions).Binding.Warning);
        Assert.Equal(BindingWarningCode.EmptyBinding, binding.Warning);
        Assert.Equal(["tax.return", "tax.amendment"], Assert.Single(Assert.Single(standings).Fields).CarryingRecordTypes);
        Assert.Equal(BindingWarningCode.EmptyBinding, narrowed.Warning);
        Assert.Equal(
            [
                "http://localhost:5000/api/local-node/authorization/role-vocabulary",
                "http://localhost:5000/api/local-node/authorization/capability-definitions",
                $"http://localhost:5000/api/local-node/authorization/capability-definitions/{id:D}/binding",
                "http://localhost:5000/api/local-node/authorization/standing-catalogue",
                $"http://localhost:5000/api/local-node/authorization/capability-definitions/{id:D}/binding",
            ],
            handler.Requests.Select(request => request.Uri.AbsoluteUri));
        var post = handler.Requests[^1];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("request-key", post.IdempotencyKey);
        using var body = JsonDocument.Parse(post.Body!);
        Assert.Empty(body.RootElement.GetProperty("selectedRoles").EnumerateArray());
        Assert.Equal("clear", body.RootElement.GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData(400, "authorization.binding_invalid", "selected role binding is invalid")]
    [InlineData(404, "authorization.definition_not_found", "no longer available")]
    [InlineData(403, "authorization.permission_required", "do not have permission")]
    [InlineData(409, "authorization.idempotency_key_reused", "conflicts with an earlier request")]
    public async Task Machine_codes_map_to_lane_owned_copy_without_server_prose(int status, string code, string copy)
    {
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse((HttpStatusCode)status, $$"""{"code":"{{code}}","detail":"SERVER PROSE MUST NOT LEAK"}""")));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<AuthorizationAdminException>(
            () => client.GetEffectiveBindingAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));

        Assert.Equal(status, error.Status);
        Assert.Equal(code, error.Code);
        Assert.Contains(copy, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SERVER PROSE", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Crafted_unoffered_role_is_posted_and_widening_refusal_uses_lane_owned_copy()
    {
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.Conflict,
            """{"code":"authorization.binding_widening_refused","detail":"SERVER PROSE MUST NOT LEAK"}""")));
        var client = CreateClient(handler, () => "crafted-request");
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var error = await Assert.ThrowsAsync<AuthorizationAdminException>(() => client.NarrowCapabilityBindingAsync(
            id,
            [new("tax.roles", "reviewer")],
            "crafted widening"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("tax.roles", body.RootElement.GetProperty("selectedRoles")[0].GetProperty("vocabulary").GetString());
        Assert.Equal("reviewer", body.RootElement.GetProperty("selectedRoles")[0].GetProperty("name").GetString());
        Assert.Equal(409, error.Status);
        Assert.Equal("authorization.binding_widening_refused", error.Code);
        Assert.Equal("A removed role cannot be restored through this definition revision.", error.Message);
        Assert.DoesNotContain("SERVER PROSE", error.Message, StringComparison.Ordinal);
    }

    private static HttpAuthorizationAdminClient CreateClient(RecordingHandler handler, Func<string>? createKey = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") }, createKey);

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new(
                request.Method,
                request.RequestUri!,
                body,
                request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null));
            return await respond(request);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body, string? IdempotencyKey);
}
