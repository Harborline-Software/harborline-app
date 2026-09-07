using System.Net.Http.Json;
using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Admin;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Views;

/// <summary>
/// Provides views administration operations through the local-node HTTP API.
/// </summary>
public sealed class HttpViewsAdminClient(HttpClient http) : IViewsAdminClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<ViewDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/local-node/views/definitions", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<SummaryWire[]>(JsonOptions, ct) ?? [];
        return wire.Select(MapSummary).ToArray();
    }

    /// <inheritdoc />
    public async Task<ViewDefinitionDetail> GetDefinitionAsync(string key, CancellationToken ct = default)
    {
        var escapedKey = Uri.EscapeDataString(key);
        using var response = await http.GetAsync($"api/local-node/views/definitions/{escapedKey}", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<DetailWire>(JsonOptions, ct)
            ?? throw new JsonException("The view definition response body was empty.");
        return new ViewDefinitionDetail(
            wire.Key, wire.Version, wire.Title, wire.ViewKind, wire.CascadeLayer,
            wire.SchemaVersion, wire.Parameters, wire.Provenance);
    }

    /// <inheritdoc />
    public async Task<ViewVersionList> ListVersionsAsync(string key, CancellationToken ct = default)
    {
        var escapedKey = Uri.EscapeDataString(key);
        using var response = await http.GetAsync(
            $"api/local-node/views/definitions/{escapedKey}/versions", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<VersionListWire>(JsonOptions, ct)
            ?? throw new JsonException("The report versions response body was empty.");
        return new ViewVersionList(wire.Ordering, wire.Versions.Select(MapSummary).ToArray());
    }

    private static ViewDefinitionSummary MapSummary(SummaryWire item) =>
        new(item.Key, item.Version, item.Title, item.ViewKind, item.CascadeLayer);

    /// <summary>Ticket 094: the shared <c>{ code, detail? }</c> parse (see <see cref="AdminError"/>).</summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await AdminError.ReadAsync(response, ct);
        throw new ViewsAdminException((int)response.StatusCode, error.Message, error.Code, error.Detail);
    }

    private sealed record SummaryWire(
        string Key, string Version, string Title, string ViewKind, string CascadeLayer);

    private sealed record DetailWire(
        string Key, string Version, string Title, string ViewKind, string CascadeLayer,
        int SchemaVersion, JsonElement Parameters, JsonElement Provenance);

    private sealed record VersionListWire(string Ordering, IReadOnlyList<SummaryWire> Versions);
}
