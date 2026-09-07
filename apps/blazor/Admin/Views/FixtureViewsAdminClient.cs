using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Views;

/// <summary>
/// Provides deterministic, in-memory views administration data for the standalone reference host.
/// </summary>
public sealed class FixtureViewsAdminClient : IViewsAdminClient
{
    private const string StandardParametersJson = """{"entityType":"work-order"}""";
    private const string StandardProvenanceJson = """{"originPackKey":"harborline.core-views","tier":"Vendor"}""";
    private const string OccupancyParametersJson = """{"scope":"portfolio"}""";
    private const string OccupancyProvenanceJson = """{"originPackKey":"demo.views","tier":"Community"}""";

    // The fixture is read-only, so its state never mutates and no lock is needed.
    private readonly IReadOnlyList<ViewDefinitionSummary> definitions =
    [
        new("work-orders-table", "2.1.0", "Work orders table", "table", "Tenant"),
        new("tenant-directory", "1.4.2", "Tenant directory", "table", "Tenant"),
        new("occupancy-board", "0.3.0", "Occupancy board", "board", "Tenant"),
    ];
    private readonly IReadOnlyDictionary<string, ViewVersionList> versions =
        new Dictionary<string, ViewVersionList>(StringComparer.Ordinal)
        {
            ["work-orders-table"] = new(
                "semver",
                [
                    new("work-orders-table", "2.1.0", "Work orders table", "table", "Tenant"),
                    new("work-orders-table", "2.0.0", "Work orders table", "table", "Tenant"),
                    new("work-orders-table", "1.9.0", "Work orders table", "table", "Tenant"),
                ]),
            ["tenant-directory"] = new(
                "semver",
                [
                    new("tenant-directory", "1.4.2", "Tenant directory", "table", "Tenant"),
                    new("tenant-directory", "1.4.1", "Tenant directory", "table", "Tenant"),
                ]),
            ["occupancy-board"] = new(
                "ordinal",
                [
                    new("occupancy-board", "2024-legacy", "Occupancy board", "board", "Tenant"),
                    new("occupancy-board", "0.3.0", "Occupancy board", "board", "Tenant"),
                ]),
        };

    /// <inheritdoc />
    public Task<IReadOnlyList<ViewDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ViewDefinitionSummary>>(definitions.ToArray());
    }

    /// <inheritdoc />
    public Task<ViewDefinitionDetail> GetDefinitionAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var detail = key switch
        {
            "work-orders-table" => Detail(
                "work-orders-table", "2.1.0", "Work orders table", "table",
                StandardParametersJson, StandardProvenanceJson),
            "tenant-directory" => Detail(
                "tenant-directory", "1.4.2", "Tenant directory", "table",
                StandardParametersJson, StandardProvenanceJson),
            "occupancy-board" => Detail(
                "occupancy-board", "0.3.0", "Occupancy board", "board",
                OccupancyParametersJson, OccupancyProvenanceJson),
            _ => throw NotFound(key),
        };
        return Task.FromResult(detail);
    }

    /// <inheritdoc />
    public Task<ViewVersionList> ListVersionsAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!versions.TryGetValue(key, out var history))
        {
            throw NotFound(key);
        }

        return Task.FromResult(new ViewVersionList(history.Ordering, history.Versions.ToArray()));
    }

    private static ViewDefinitionDetail Detail(
        string key,
        string version,
        string title,
        string viewKind,
        string parametersJson,
        string provenanceJson) =>
        new(
            key,
            version,
            title,
            viewKind,
            "Tenant",
            1,
            JsonSerializer.Deserialize<JsonElement>(parametersJson),
            JsonSerializer.Deserialize<JsonElement>(provenanceJson));

    private static ViewsAdminException NotFound(string key) =>
        new(404, $"No view definition '{key}' for this tenant.");
}
