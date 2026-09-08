using System.Net.Http.Json;
using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Forms;

/// <summary>
/// Provides forms administration operations through the local-node HTTP API.
/// </summary>
public sealed class HttpFormsAdminClient(HttpClient http) : IFormsAdminClient
{
    public async Task<RuntimeForm> RenderFormAsync(string formId, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/local-node/forms/{Uri.EscapeDataString(formId)}", ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<RuntimeForm>(JsonOptions, ct) ?? throw new JsonException("The form response was empty.");
    }
    public async Task<RuntimeReceipt> SubmitFormAsync(string formId, string body, CancellationToken ct = default)
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await http.PostAsync($"api/local-node/forms/{Uri.EscapeDataString(formId)}/submit", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            JsonElement error = default;
            try { error = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct); } catch (JsonException) { }
            var code = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("code", out var c) ? c.GetString() : null;
            Guid? auditId = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("auditId", out var id) && id.ValueKind == JsonValueKind.String && id.TryGetGuid(out var parsed) ? parsed : null;
            throw new Authorization.AuthorizationAdminException((int)response.StatusCode, code ?? $"http.{(int)response.StatusCode}", auditId);
        }
        return await response.Content.ReadFromJsonAsync<RuntimeReceipt>(JsonOptions, ct) ?? throw new JsonException("The submission response was empty.");
    }
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<FormDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/local-node/forms/definitions", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<DefinitionWire[]>(JsonOptions, ct) ?? [];
        return wire.Select(item => new FormDefinitionSummary(
            item.FormId,
            item.Version,
            MapTitle(item.Title),
            item.UpdatedAt,
            item.CascadeLayer)).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FormVersionSummary>> ListVersionsAsync(string formId, CancellationToken ct = default)
    {
        var escapedFormId = Uri.EscapeDataString(formId);
        using var response = await http.GetAsync($"api/local-node/forms/definitions/{escapedFormId}/versions", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<VersionWire[]>(JsonOptions, ct) ?? [];
        return wire.Select(item => new FormVersionSummary(
            item.FormId, item.Version, item.Status, item.Owner, item.CreatedAt, item.UpdatedAt,
            item.DerivedFrom, item.SyncsToPeers, item.SafeForStaging)).ToArray();
    }

    /// <inheritdoc />
    public async Task<RestoreResult> RestoreVersionAsync(string formId, string version, CancellationToken ct = default)
    {
        var escapedFormId = Uri.EscapeDataString(formId);
        using var response = await http.PostAsJsonAsync(
            $"api/local-node/forms/definitions/{escapedFormId}/restore", new { version }, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<RestoreWire>(JsonOptions, ct)
            ?? throw new JsonException("The restore response body was empty.");
        return new RestoreResult(wire.FormId, wire.Version);
    }

    private static InternationalizedText? MapTitle(TitleWire? title) =>
        title is null ? null : new InternationalizedText(title.DefaultLocale, title.Values);

    /// <summary>Ticket 094: the shared <c>{ code, detail? }</c> parse (see <see cref="AdminError"/>).</summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await AdminError.ReadAsync(response, ct);
        throw new FormsAdminException((int)response.StatusCode, error.Message, error.Code, error.Detail);
    }

    private sealed record DefinitionWire(
        string FormId, string Version, TitleWire? Title, DateTimeOffset UpdatedAt, string? CascadeLayer);

    private sealed record VersionWire(
        string FormId, string Version, string Status, string? Owner,
        DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? DerivedFrom,
        bool SyncsToPeers, bool SafeForStaging);

    private sealed record RestoreWire(string FormId, string Version);

    private sealed record TitleWire(string DefaultLocale, IReadOnlyDictionary<string, string> Values);
}
