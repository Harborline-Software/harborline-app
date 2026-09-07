using System.Net.Http.Json;
using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Admin;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Reports;

/// <summary>
/// Provides reports administration operations through the local-node HTTP API.
/// </summary>
public sealed class HttpReportsAdminClient(HttpClient http) : IReportsAdminClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/local-node/reports/definitions", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<SummaryWire[]>(JsonOptions, ct) ?? [];
        return wire.Select(MapSummary).ToArray();
    }

    /// <inheritdoc />
    public async Task<ReportDefinitionDetail> GetDefinitionAsync(string key, CancellationToken ct = default)
    {
        var escapedKey = Uri.EscapeDataString(key);
        using var response = await http.GetAsync($"api/local-node/reports/definitions/{escapedKey}", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<DetailWire>(JsonOptions, ct)
            ?? throw new JsonException("The report definition response body was empty.");
        return new ReportDefinitionDetail(
            wire.Key, wire.Version, wire.Title, wire.ReportKind, wire.CascadeLayer,
            wire.SchemaVersion, wire.Parameters, wire.Provenance);
    }

    /// <inheritdoc />
    public async Task<ReportVersionList> ListVersionsAsync(string key, CancellationToken ct = default)
    {
        var escapedKey = Uri.EscapeDataString(key);
        using var response = await http.GetAsync(
            $"api/local-node/reports/definitions/{escapedKey}/versions", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<VersionListWire>(JsonOptions, ct)
            ?? throw new JsonException("The report versions response body was empty.");
        return new ReportVersionList(wire.Ordering, wire.Versions.Select(MapSummary).ToArray());
    }

    private static ReportDefinitionSummary MapSummary(SummaryWire item) =>
        new(item.Key, item.Version, item.Title, item.ReportKind, item.CascadeLayer);

    /// <summary>Ticket 094: the shared <c>{ code, detail? }</c> parse (see <see cref="AdminError"/>).</summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await AdminError.ReadAsync(response, ct);
        throw new ReportsAdminException((int)response.StatusCode, error.Message, error.Code, error.Detail);
    }

    private sealed record SummaryWire(
        string Key, string Version, string Title, string ReportKind, string CascadeLayer);

    private sealed record DetailWire(
        string Key, string Version, string Title, string ReportKind, string CascadeLayer,
        int SchemaVersion, JsonElement Parameters, JsonElement Provenance);

    private sealed record VersionListWire(string Ordering, IReadOnlyList<SummaryWire> Versions);
}
