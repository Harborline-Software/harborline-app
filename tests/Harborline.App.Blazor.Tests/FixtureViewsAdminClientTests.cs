using Harborline.App.Blazor.ReferenceHost.Admin.Views;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// Verifies the deterministic views administration fixture and its read-only history semantics.
/// </summary>
public sealed class FixtureViewsAdminClientTests
{
    [Fact]
    public async Task List_definitions_returns_the_deterministic_rows()
    {
        var rows = await new FixtureViewsAdminClient().ListDefinitionsAsync();

        Assert.Collection(
            rows,
            row => Assert.Equal(new ViewDefinitionSummary(
                "work-orders-table", "2.1.0", "Work orders table", "table", "Tenant"), row),
            row => Assert.Equal(new ViewDefinitionSummary(
                "tenant-directory", "1.4.2", "Tenant directory", "table", "Tenant"), row),
            row => Assert.Equal(new ViewDefinitionSummary(
                "occupancy-board", "0.3.0", "Occupancy board", "board", "Tenant"), row));
    }

    [Fact]
    public async Task Versions_are_ordered_semver_descending_for_semver_keys()
    {
        var client = new FixtureViewsAdminClient();

        var trialBalance = await client.ListVersionsAsync("work-orders-table");
        var arAging = await client.ListVersionsAsync("tenant-directory");

        Assert.Equal("semver", trialBalance.Ordering);
        Assert.Equal(["2.1.0", "2.0.0", "1.9.0"], trialBalance.Versions.Select(row => row.Version));
        Assert.Equal("semver", arAging.Ordering);
        Assert.Equal(["1.4.2", "1.4.1"], arAging.Versions.Select(row => row.Version));
    }

    [Fact]
    public async Task Versions_fall_back_to_ordinal_descending_when_any_version_is_not_semver()
    {
        var history = await new FixtureViewsAdminClient().ListVersionsAsync("occupancy-board");

        Assert.Equal("ordinal", history.Ordering);
        Assert.Equal(["2024-legacy", "0.3.0"], history.Versions.Select(row => row.Version));
        Assert.All(history.Versions, row =>
        {
            Assert.Equal("Occupancy board", row.Title);
            Assert.Equal("board", row.ViewKind);
            Assert.Equal("Tenant", row.CascadeLayer);
        });
    }

    [Fact]
    public async Task Detail_carries_schema_version_parameters_and_provenance()
    {
        var client = new FixtureViewsAdminClient();

        var trialBalance = await client.GetDefinitionAsync("work-orders-table");
        var occupancyBoard = await client.GetDefinitionAsync("occupancy-board");

        Assert.Equal(1, trialBalance.SchemaVersion);
        Assert.Equal("work-order", trialBalance.Parameters.GetProperty("entityType").GetString());
        Assert.Equal("harborline.core-views", trialBalance.Provenance.GetProperty("originPackKey").GetString());
        Assert.Equal("Vendor", trialBalance.Provenance.GetProperty("tier").GetString());
        Assert.Equal("portfolio", occupancyBoard.Parameters.GetProperty("scope").GetString());
        Assert.Equal("Community", occupancyBoard.Provenance.GetProperty("tier").GetString());
        Assert.Equal("demo.views", occupancyBoard.Provenance.GetProperty("originPackKey").GetString());
    }

    [Fact]
    public async Task Unknown_keys_throw_the_wire_not_found_error()
    {
        var client = new FixtureViewsAdminClient();

        var detailError = await Assert.ThrowsAsync<ViewsAdminException>(
            () => client.GetDefinitionAsync("unknown"));
        var versionsError = await Assert.ThrowsAsync<ViewsAdminException>(
            () => client.ListVersionsAsync("unknown"));

        Assert.Equal(404, detailError.Status);
        Assert.Equal("No view definition 'unknown' for this tenant.", detailError.Message);
        Assert.Equal(404, versionsError.Status);
        Assert.Equal("No view definition 'unknown' for this tenant.", versionsError.Message);
    }
}
