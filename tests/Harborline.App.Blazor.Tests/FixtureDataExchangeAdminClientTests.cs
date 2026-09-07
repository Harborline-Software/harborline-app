using Harborline.App.Blazor.ReferenceHost.Admin.DataExchange;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// Verifies the deterministic data exchange administration fixture and its read-only history semantics.
/// </summary>
public sealed class FixtureDataExchangeAdminClientTests
{
    [Fact]
    public async Task List_definitions_returns_the_deterministic_rows()
    {
        var rows = await new FixtureDataExchangeAdminClient().ListDefinitionsAsync();

        Assert.Collection(
            rows,
            row => Assert.Equal(new DataExchangeDefinitionSummary(
                "bank-feed-import", "2.1.0", "Bank feed import", "import", "Tenant"), row),
            row => Assert.Equal(new DataExchangeDefinitionSummary(
                "erpnext-sync", "1.4.2", "ERPNext sync", "import", "Tenant"), row),
            row => Assert.Equal(new DataExchangeDefinitionSummary(
                "legacy-ledger-export", "0.3.0", "Legacy ledger export", "export", "Tenant"), row));
    }

    [Fact]
    public async Task Versions_are_ordered_semver_descending_for_semver_keys()
    {
        var client = new FixtureDataExchangeAdminClient();

        var trialBalance = await client.ListVersionsAsync("bank-feed-import");
        var arAging = await client.ListVersionsAsync("erpnext-sync");

        Assert.Equal("semver", trialBalance.Ordering);
        Assert.Equal(["2.1.0", "2.0.0", "1.9.0"], trialBalance.Versions.Select(row => row.Version));
        Assert.Equal("semver", arAging.Ordering);
        Assert.Equal(["1.4.2", "1.4.1"], arAging.Versions.Select(row => row.Version));
    }

    [Fact]
    public async Task Versions_fall_back_to_ordinal_descending_when_any_version_is_not_semver()
    {
        var history = await new FixtureDataExchangeAdminClient().ListVersionsAsync("legacy-ledger-export");

        Assert.Equal("ordinal", history.Ordering);
        Assert.Equal(["2024-legacy", "0.3.0"], history.Versions.Select(row => row.Version));
        Assert.All(history.Versions, row =>
        {
            Assert.Equal("Legacy ledger export", row.Title);
            Assert.Equal("export", row.ExchangeKind);
            Assert.Equal("Tenant", row.CascadeLayer);
        });
    }

    [Fact]
    public async Task Detail_carries_schema_version_settings_and_provenance()
    {
        var client = new FixtureDataExchangeAdminClient();

        var trialBalance = await client.GetDefinitionAsync("bank-feed-import");
        var legacyLedgerExport = await client.GetDefinitionAsync("legacy-ledger-export");

        Assert.Equal(1, trialBalance.SchemaVersion);
        Assert.Equal("csv-standard", trialBalance.Settings.GetProperty("mapping").GetString());
        Assert.Equal("harborline.core-exchange", trialBalance.Provenance.GetProperty("originPackKey").GetString());
        Assert.Equal("Vendor", trialBalance.Provenance.GetProperty("tier").GetString());
        Assert.Equal("ofx", legacyLedgerExport.Settings.GetProperty("format").GetString());
        Assert.Equal("Community", legacyLedgerExport.Provenance.GetProperty("tier").GetString());
        Assert.Equal("demo.exchange", legacyLedgerExport.Provenance.GetProperty("originPackKey").GetString());
    }

    [Fact]
    public async Task Unknown_keys_throw_the_wire_not_found_error()
    {
        var client = new FixtureDataExchangeAdminClient();

        var detailError = await Assert.ThrowsAsync<DataExchangeAdminException>(
            () => client.GetDefinitionAsync("unknown"));
        var versionsError = await Assert.ThrowsAsync<DataExchangeAdminException>(
            () => client.ListVersionsAsync("unknown"));

        Assert.Equal(404, detailError.Status);
        Assert.Equal("No data exchange definition 'unknown' for this tenant.", detailError.Message);
        Assert.Equal(404, versionsError.Status);
        Assert.Equal("No data exchange definition 'unknown' for this tenant.", versionsError.Message);
    }
}
