using System.Net.Http.Json;
using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Forms;

/// <summary>
/// Provides forms administration operations through the local-node HTTP API.
/// </summary>
public sealed class HttpFormsAdminClient(HttpClient http) : IFormsAdminClient
{
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
