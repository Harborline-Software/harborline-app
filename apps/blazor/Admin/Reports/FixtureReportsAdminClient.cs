using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Reports;

/// <summary>
/// Provides deterministic, in-memory reports administration data for the standalone reference host.
/// </summary>
public sealed class FixtureReportsAdminClient : IReportsAdminClient
{
    private const string StandardParametersJson = """{"chartId":"chart-7"}""";
    private const string StandardProvenanceJson = """{"originPackKey":"harborline.core-reports","tier":"Vendor"}""";
    private const string OccupancyParametersJson = """{"scope":"portfolio"}""";
    private const string OccupancyProvenanceJson = """{"originPackKey":"demo.occupancy","tier":"Community"}""";

    // The fixture is read-only, so its state never mutates and no lock is needed.
    private readonly IReadOnlyList<ReportDefinitionSummary> definitions =
    [
        new("trial-balance", "2.1.0", "Trial balance", "standard", "Tenant"),
        new("ar-aging", "1.4.2", "AR aging summary", "standard", "Tenant"),
        new("occupancy", "0.3.0", "Occupancy snapshot", "snapshot", "Tenant"),
    ];
    private readonly IReadOnlyDictionary<string, ReportVersionList> versions =
        new Dictionary<string, ReportVersionList>(StringComparer.Ordinal)
        {
            ["trial-balance"] = new(
                "semver",
                [
                    new("trial-balance", "2.1.0", "Trial balance", "standard", "Tenant"),
                    new("trial-balance", "2.0.0", "Trial balance", "standard", "Tenant"),
                    new("trial-balance", "1.9.0", "Trial balance", "standard", "Tenant"),
                ]),
            ["ar-aging"] = new(
                "semver",
                [
                    new("ar-aging", "1.4.2", "AR aging summary", "standard", "Tenant"),
                    new("ar-aging", "1.4.1", "AR aging summary", "standard", "Tenant"),
                ]),
            ["occupancy"] = new(
                "ordinal",
                [
                    new("occupancy", "2024-legacy", "Occupancy snapshot", "snapshot", "Tenant"),
                    new("occupancy", "0.3.0", "Occupancy snapshot", "snapshot", "Tenant"),
                ]),
        };

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ReportDefinitionSummary>>(definitions.ToArray());
    }

    /// <inheritdoc />
    public Task<ReportDefinitionDetail> GetDefinitionAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var detail = key switch
        {
            "trial-balance" => Detail(
                "trial-balance", "2.1.0", "Trial balance", "standard",
                StandardParametersJson, StandardProvenanceJson),
            "ar-aging" => Detail(
                "ar-aging", "1.4.2", "AR aging summary", "standard",
                StandardParametersJson, StandardProvenanceJson),
            "occupancy" => Detail(
                "occupancy", "0.3.0", "Occupancy snapshot", "snapshot",
                OccupancyParametersJson, OccupancyProvenanceJson),
            _ => throw NotFound(key),
        };
        return Task.FromResult(detail);
    }

    /// <inheritdoc />
    public Task<ReportVersionList> ListVersionsAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!versions.TryGetValue(key, out var history))
        {
            throw NotFound(key);
        }

        return Task.FromResult(new ReportVersionList(history.Ordering, history.Versions.ToArray()));
    }

    private static ReportDefinitionDetail Detail(
        string key,
        string version,
        string title,
        string reportKind,
        string parametersJson,
        string provenanceJson) =>
        new(
            key,
            version,
            title,
            reportKind,
            "Tenant",
            1,
            JsonSerializer.Deserialize<JsonElement>(parametersJson),
            JsonSerializer.Deserialize<JsonElement>(provenanceJson));

    private static ReportsAdminException NotFound(string key) =>
        new(404, $"No report definition '{key}' for this tenant.");
}
