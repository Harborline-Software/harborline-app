using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Admin.DataExchange;

/// <summary>
/// Provides deterministic, in-memory data exchange administration data for the standalone reference host.
/// </summary>
public sealed class FixtureDataExchangeAdminClient : IDataExchangeAdminClient
{
    private const string StandardSettingsJson = """{"mapping":"csv-standard"}""";
    private const string StandardProvenanceJson = """{"originPackKey":"harborline.core-exchange","tier":"Vendor"}""";
    private const string OccupancySettingsJson = """{"format":"ofx"}""";
    private const string OccupancyProvenanceJson = """{"originPackKey":"demo.exchange","tier":"Community"}""";

    // The fixture is read-only, so its state never mutates and no lock is needed.
    private readonly IReadOnlyList<DataExchangeDefinitionSummary> definitions =
    [
        new("bank-feed-import", "2.1.0", "Bank feed import", "import", "Tenant"),
        new("erpnext-sync", "1.4.2", "ERPNext sync", "import", "Tenant"),
        new("legacy-ledger-export", "0.3.0", "Legacy ledger export", "export", "Tenant"),
    ];
    private readonly IReadOnlyDictionary<string, DataExchangeVersionList> versions =
        new Dictionary<string, DataExchangeVersionList>(StringComparer.Ordinal)
        {
            ["bank-feed-import"] = new(
                "semver",
                [
                    new("bank-feed-import", "2.1.0", "Bank feed import", "import", "Tenant"),
                    new("bank-feed-import", "2.0.0", "Bank feed import", "import", "Tenant"),
                    new("bank-feed-import", "1.9.0", "Bank feed import", "import", "Tenant"),
                ]),
            ["erpnext-sync"] = new(
                "semver",
                [
                    new("erpnext-sync", "1.4.2", "ERPNext sync", "import", "Tenant"),
                    new("erpnext-sync", "1.4.1", "ERPNext sync", "import", "Tenant"),
                ]),
            ["legacy-ledger-export"] = new(
                "ordinal",
                [
                    new("legacy-ledger-export", "2024-legacy", "Legacy ledger export", "export", "Tenant"),
                    new("legacy-ledger-export", "0.3.0", "Legacy ledger export", "export", "Tenant"),
                ]),
        };

    /// <inheritdoc />
    public Task<IReadOnlyList<DataExchangeDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<DataExchangeDefinitionSummary>>(definitions.ToArray());
    }

    /// <inheritdoc />
    public Task<DataExchangeDefinitionDetail> GetDefinitionAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var detail = key switch
        {
            "bank-feed-import" => Detail(
                "bank-feed-import", "2.1.0", "Bank feed import", "import",
                StandardSettingsJson, StandardProvenanceJson),
            "erpnext-sync" => Detail(
                "erpnext-sync", "1.4.2", "ERPNext sync", "import",
                StandardSettingsJson, StandardProvenanceJson),
            "legacy-ledger-export" => Detail(
                "legacy-ledger-export", "0.3.0", "Legacy ledger export", "export",
                OccupancySettingsJson, OccupancyProvenanceJson),
            _ => throw NotFound(key),
        };
        return Task.FromResult(detail);
    }

    /// <inheritdoc />
    public Task<DataExchangeVersionList> ListVersionsAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!versions.TryGetValue(key, out var history))
        {
            throw NotFound(key);
        }

        return Task.FromResult(new DataExchangeVersionList(history.Ordering, history.Versions.ToArray()));
    }

    private static DataExchangeDefinitionDetail Detail(
        string key,
        string version,
        string title,
        string exchangeKind,
        string settingsJson,
        string provenanceJson) =>
        new(
            key,
            version,
            title,
            exchangeKind,
            "Tenant",
            1,
            JsonSerializer.Deserialize<JsonElement>(settingsJson),
            JsonSerializer.Deserialize<JsonElement>(provenanceJson));

    private static DataExchangeAdminException NotFound(string key) =>
        new(404, $"No data exchange definition '{key}' for this tenant.");
}
