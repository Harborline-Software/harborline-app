using Harborline.App.Blazor.ReferenceHost.Admin.Reports;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// Verifies the deterministic reports administration fixture and its read-only history semantics.
/// </summary>
public sealed class FixtureReportsAdminClientTests
{
    [Fact]
    public async Task List_definitions_returns_the_deterministic_rows()
    {
        var rows = await new FixtureReportsAdminClient().ListDefinitionsAsync();

        Assert.Collection(
            rows,
            row => Assert.Equal(new ReportDefinitionSummary(
                "trial-balance", "2.1.0", "Trial balance", "standard", "Tenant"), row),
            row => Assert.Equal(new ReportDefinitionSummary(
                "ar-aging", "1.4.2", "AR aging summary", "standard", "Tenant"), row),
            row => Assert.Equal(new ReportDefinitionSummary(
                "occupancy", "0.3.0", "Occupancy snapshot", "snapshot", "Tenant"), row));
    }

    [Fact]
    public async Task Versions_are_ordered_semver_descending_for_semver_keys()
    {
        var client = new FixtureReportsAdminClient();

        var trialBalance = await client.ListVersionsAsync("trial-balance");
        var arAging = await client.ListVersionsAsync("ar-aging");

        Assert.Equal("semver", trialBalance.Ordering);
        Assert.Equal(["2.1.0", "2.0.0", "1.9.0"], trialBalance.Versions.Select(row => row.Version));
        Assert.Equal("semver", arAging.Ordering);
        Assert.Equal(["1.4.2", "1.4.1"], arAging.Versions.Select(row => row.Version));
    }

    [Fact]
    public async Task Versions_fall_back_to_ordinal_descending_when_any_version_is_not_semver()
    {
        var history = await new FixtureReportsAdminClient().ListVersionsAsync("occupancy");

        Assert.Equal("ordinal", history.Ordering);
        Assert.Equal(["2024-legacy", "0.3.0"], history.Versions.Select(row => row.Version));
        Assert.All(history.Versions, row =>
        {
            Assert.Equal("Occupancy snapshot", row.Title);
            Assert.Equal("snapshot", row.ReportKind);
            Assert.Equal("Tenant", row.CascadeLayer);
        });
    }

    [Fact]
    public async Task Detail_carries_schema_version_parameters_and_provenance()
    {
        var client = new FixtureReportsAdminClient();

        var trialBalance = await client.GetDefinitionAsync("trial-balance");
        var occupancy = await client.GetDefinitionAsync("occupancy");

        Assert.Equal(1, trialBalance.SchemaVersion);
        Assert.Equal("chart-7", trialBalance.Parameters.GetProperty("chartId").GetString());
        Assert.Equal("harborline.core-reports", trialBalance.Provenance.GetProperty("originPackKey").GetString());
        Assert.Equal("Vendor", trialBalance.Provenance.GetProperty("tier").GetString());
        Assert.Equal("portfolio", occupancy.Parameters.GetProperty("scope").GetString());
        Assert.Equal("Community", occupancy.Provenance.GetProperty("tier").GetString());
        Assert.Equal("demo.occupancy", occupancy.Provenance.GetProperty("originPackKey").GetString());
    }

    [Fact]
    public async Task Unknown_keys_throw_the_wire_not_found_error()
    {
        var client = new FixtureReportsAdminClient();

        var detailError = await Assert.ThrowsAsync<ReportsAdminException>(
            () => client.GetDefinitionAsync("unknown"));
        var versionsError = await Assert.ThrowsAsync<ReportsAdminException>(
            () => client.ListVersionsAsync("unknown"));

        Assert.Equal(404, detailError.Status);
        Assert.Equal("No report definition 'unknown' for this tenant.", detailError.Message);
        Assert.Equal(404, versionsError.Status);
        Assert.Equal("No report definition 'unknown' for this tenant.", versionsError.Message);
    }
}
