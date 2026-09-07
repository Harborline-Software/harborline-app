using Harborline.App.Blazor.ReferenceHost.Admin.Scheduling;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// Verifies the deterministic scheduling administration fixture and its restore semantics.
/// </summary>
public sealed class FixtureSchedulingAdminClientTests
{
    [Fact]
    public async Task List_definitions_returns_the_deterministic_heads()
    {
        var rows = await new FixtureSchedulingAdminClient().ListDefinitionsAsync();

        Assert.Collection(
            rows,
            row => AssertDefinition(row, "inspection-protocol", 3, "Inspection protocol", "2026-08-18T09:00:00Z", "user:fixture-scheduler"),
            row => AssertDefinition(row, "move-in-checklist", 1, "Move-in checklist", "2026-08-10T14:30:00Z", "user:fixture-scheduler"),
            row => AssertDefinition(row, "turnover-schedule", 2, "Turnover schedule", "2026-08-15T11:15:00Z", "user:fixture-admin"));
    }

    [Fact]
    public async Task List_versions_returns_the_retained_history_newest_first()
    {
        var client = new FixtureSchedulingAdminClient();

        var inspection = await client.ListVersionsAsync("inspection-protocol");
        var turnover = await client.ListVersionsAsync("turnover-schedule");

        Assert.Collection(
            inspection,
            row => AssertVersion(row, 3, "2026-08-18T09:00:00Z", "user:fixture-scheduler"),
            row => AssertVersion(row, 2, "2026-08-12T10:00:00Z", "user:fixture-scheduler"),
            row => AssertVersion(row, 1, "2026-08-05T08:00:00Z", "user:fixture-scheduler"));
        Assert.Collection(
            turnover,
            row => AssertVersion(row, 2, "2026-08-15T11:15:00Z", "user:fixture-admin"),
            row => AssertVersion(row, 1, "2026-08-09T16:45:00Z", "user:fixture-scheduler"));
    }

    [Fact]
    public async Task Get_definition_returns_the_head_view()
    {
        var view = await new FixtureSchedulingAdminClient().GetDefinitionAsync("inspection-protocol");

        Assert.Equal(3, view.Revision);
        Assert.Equal("Inspection protocol", view.Definition.GetProperty("title").GetString());
        Assert.Equal("weekly", view.Definition.GetProperty("cadence").GetString());
        Assert.Equal("user:fixture-scheduler", view.UpdatedBy);
    }

    [Fact]
    public async Task Restore_mints_the_next_revision_as_the_new_head()
    {
        var client = new FixtureSchedulingAdminClient();

        var result = await client.RestoreRevisionAsync("inspection-protocol", 1);
        var versions = await client.ListVersionsAsync("inspection-protocol");
        var definitions = await client.ListDefinitionsAsync();
        var view = await client.GetDefinitionAsync("inspection-protocol");

        Assert.Equal(new RestoreResult("inspection-protocol", 4, 1), result);
        Assert.Equal(4, versions.Count);
        AssertVersion(versions[0], 4, "2026-08-20T00:00:00Z", "user:fixture-operator");
        Assert.Equal(4, Assert.Single(definitions, row => row.Id == "inspection-protocol").Revision);
        Assert.Equal(4, view.Revision);
        Assert.Equal("Inspection protocol", view.Definition.GetProperty("title").GetString());
        Assert.Equal("weekly", view.Definition.GetProperty("cadence").GetString());
        var source = Assert.Single(versions, row => row.Revision == 1);
        AssertVersion(source, 1, "2026-08-05T08:00:00Z", "user:fixture-scheduler");

        var second = await client.RestoreRevisionAsync("turnover-schedule", 1);
        var turnover = await client.ListVersionsAsync("turnover-schedule");
        Assert.Equal(new RestoreResult("turnover-schedule", 3, 1), second);
        AssertVersion(turnover[0], 3, "2026-08-20T00:01:00Z", "user:fixture-operator");
    }

    [Fact]
    public async Task Restore_rejects_unknown_definitions_and_revisions()
    {
        var client = new FixtureSchedulingAdminClient();

        var unknownDefinition = await Assert.ThrowsAsync<SchedulingAdminException>(
            () => client.RestoreRevisionAsync("unknown-definition", 1));
        var unknownRevision = await Assert.ThrowsAsync<SchedulingAdminException>(
            () => client.RestoreRevisionAsync("inspection-protocol", 99));

        // Restore answers revision_not_found for BOTH, because the real route looks for the
        // revision first and never distinguishes them. Verified against a live node 2026-08-22;
        // this lane used to answer not_found for the unknown definition and disagree with both
        // the API and the React lane.
        Assert.Equal(404, unknownDefinition.Status);
        Assert.Equal("scheduling.draft.revision_not_found", unknownDefinition.Message);
        Assert.Equal(404, unknownRevision.Status);
        Assert.Equal("scheduling.draft.revision_not_found", unknownRevision.Message);
    }

    private static void AssertDefinition(
        SchedulingDefinitionSummary row,
        string id,
        int revision,
        string title,
        string updatedAt,
        string updatedBy)
    {
        Assert.Equal(id, row.Id);
        Assert.Equal(revision, row.Revision);
        Assert.Equal(title, row.Title);
        Assert.Equal(DateTimeOffset.Parse(updatedAt), row.UpdatedAt);
        Assert.Equal(updatedBy, row.UpdatedBy);
    }

    private static void AssertVersion(
        SchedulingDefinitionSummary row,
        int revision,
        string updatedAt,
        string updatedBy)
    {
        Assert.Equal(revision, row.Revision);
        Assert.Equal(DateTimeOffset.Parse(updatedAt), row.UpdatedAt);
        Assert.Equal(updatedBy, row.UpdatedBy);
    }
}
