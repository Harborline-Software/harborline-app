using System.Net.Http.Json;
using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Authorization;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Authorization;

public sealed class HttpAuthorizationAdminClient(HttpClient httpClient, Func<string>? createIdempotencyKey = null) : IAuthorizationAdminClient
{
    public Task<AuthorizationTraceRead> ReadTraceAsync(Guid auditId) =>
        GetAsync<AuthorizationTraceRead>($"{RouteBase}/traces/{auditId:D}", CancellationToken.None);
    public Task<AccessHoldersResponse> ListHoldersAsync(CancellationToken cancellationToken = default) =>
        GetAsync<AccessHoldersResponse>($"{RouteBase}/holders", cancellationToken);

    private const string RouteBase = "api/local-node/authorization";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Func<string> createKey = createIdempotencyKey ?? (() => Guid.NewGuid().ToString("N"));

    public Task<IReadOnlyList<RoleDefinition>> ListRoleVocabularyAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<RoleDefinition>>($"{RouteBase}/role-vocabulary", cancellationToken);

    public Task<IReadOnlyList<AuthorizationCapabilityDefinition>> ListCapabilityDefinitionsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<AuthorizationCapabilityDefinition>>($"{RouteBase}/capability-definitions", cancellationToken);

    public Task<AuthorizationBinding> GetEffectiveBindingAsync(Guid definitionId, CancellationToken cancellationToken = default) =>
        GetAsync<AuthorizationBinding>($"{RouteBase}/capability-definitions/{Uri.EscapeDataString(definitionId.ToString("D"))}/binding", cancellationToken);

    public Task<IReadOnlyList<StandingDefinition>> ListStandingCatalogueAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<StandingDefinition>>($"{RouteBase}/standing-catalogue", cancellationToken);

    public async Task<NarrowAuthorizationBindingResult> NarrowCapabilityBindingAsync(
        Guid definitionId,
        IReadOnlyList<RoleReference> selectedRoles,
        string reason,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{RouteBase}/capability-definitions/{Uri.EscapeDataString(definitionId.ToString("D"))}/binding")
        {
            Content = JsonContent.Create(new NarrowAuthorizationBindingRequest(selectedRoles, reason), options: JsonOptions),
        };
        request.Headers.Add("Idempotency-Key", createKey());
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw await ToExceptionAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<NarrowAuthorizationBindingResult>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The authorization service returned an empty response.");
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode) throw await ToExceptionAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The authorization service returned an empty response.");
    }

    private static async Task<AuthorizationAdminException> ToExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var code = body.RootElement.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : $"http.{(int)response.StatusCode}";
            Guid? auditId = body.RootElement.TryGetProperty("auditId", out var id) && id.ValueKind == JsonValueKind.String && id.TryGetGuid(out var parsed) ? parsed : null;
            return new AuthorizationAdminException((int)response.StatusCode, code, auditId);
        }
        catch (JsonException)
        {
            return new AuthorizationAdminException((int)response.StatusCode, $"http.{(int)response.StatusCode}");
        }
    }
}
