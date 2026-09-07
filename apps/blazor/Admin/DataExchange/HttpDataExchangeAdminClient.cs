using System.Net.Http.Json;
using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Admin;

namespace Harborline.App.Blazor.ReferenceHost.Admin.DataExchange;

/// <summary>
/// Provides data exchange administration operations through the local-node HTTP API.
/// </summary>
public sealed class HttpDataExchangeAdminClient(HttpClient http) : IDataExchangeAdminClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<DataExchangeDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/local-node/data-exchange/definitions", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<SummaryWire[]>(JsonOptions, ct) ?? [];
        return wire.Select(MapSummary).ToArray();
    }

    /// <inheritdoc />
    public async Task<DataExchangeDefinitionDetail> GetDefinitionAsync(string key, CancellationToken ct = default)
    {
        var escapedKey = Uri.EscapeDataString(key);
        using var response = await http.GetAsync($"api/local-node/data-exchange/definitions/{escapedKey}", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<DetailWire>(JsonOptions, ct)
            ?? throw new JsonException("The data exchange definition response body was empty.");
        return new DataExchangeDefinitionDetail(
            wire.Key, wire.Version, wire.Title, wire.ExchangeKind, wire.CascadeLayer,
            wire.SchemaVersion, wire.Settings, wire.Provenance);
    }

    /// <inheritdoc />
    public async Task<DataExchangeVersionList> ListVersionsAsync(string key, CancellationToken ct = default)
    {
        var escapedKey = Uri.EscapeDataString(key);
        using var response = await http.GetAsync(
            $"api/local-node/data-exchange/definitions/{escapedKey}/versions", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<VersionListWire>(JsonOptions, ct)
            ?? throw new JsonException("The report versions response body was empty.");
        return new DataExchangeVersionList(wire.Ordering, wire.Versions.Select(MapSummary).ToArray());
    }

    private static DataExchangeDefinitionSummary MapSummary(SummaryWire item) =>
        new(item.Key, item.Version, item.Title, item.ExchangeKind, item.CascadeLayer);

    /// <summary>Ticket 094: the shared <c>{ code, detail? }</c> parse (see <see cref="AdminError"/>).</summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await AdminError.ReadAsync(response, ct);
        throw new DataExchangeAdminException((int)response.StatusCode, error.Message, error.Code, error.Detail);
    }

    private sealed record SummaryWire(
        string Key, string Version, string Title, string ExchangeKind, string CascadeLayer);

    private sealed record DetailWire(
        string Key, string Version, string Title, string ExchangeKind, string CascadeLayer,
        int SchemaVersion, JsonElement Settings, JsonElement Provenance);

    private sealed record VersionListWire(string Ordering, IReadOnlyList<SummaryWire> Versions);
}
