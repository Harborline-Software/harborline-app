using Harborline.App.Blazor.ReferenceHost.Admin.Forms;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// Verifies the deterministic forms administration fixture and its restore semantics.
/// </summary>
public sealed class FixtureFormsAdminClientTests
{
    [Fact]
    public async Task List_definitions_returns_the_deterministic_rows()
    {
        var rows = await new FixtureFormsAdminClient().ListDefinitionsAsync();

        Assert.Collection(
            rows,
            row => AssertDefinition(row, "incident-intake", "1.0.3", "Incident intake", "2026-08-05T12:00:00Z"),
            row => AssertDefinition(row, "vessel-registration", "2.1.4", "Vessel registration", "2026-08-10T15:45:00Z"),
            row =>
            {
                Assert.Equal("crew-manifest", row.FormId);
                Assert.Equal("1.0.0", row.Version);
                Assert.Null(row.Title);
                Assert.Equal(DateTimeOffset.Parse("2026-08-03T08:00:00Z"), row.UpdatedAt);
                // Deliberately non-'Tenant' (review P2-12) so fixture-backed screens exercise
                // the 'Pack' cascade-layer render path.
                Assert.Equal("Pack", row.CascadeLayer);
            });
    }

    [Fact]
    public async Task Restore_mints_the_next_patch_and_preserves_the_source()
    {
        var client = new FixtureFormsAdminClient();

        var result = await client.RestoreVersionAsync("incident-intake", "1.0.0");
        var rows = await client.ListVersionsAsync("incident-intake");

        Assert.Equal(new RestoreResult("incident-intake", "1.0.4"), result);
        Assert.Equal(5, rows.Count);
        var draft = rows[0];
        Assert.Equal("Draft", draft.Status);
        Assert.Equal("1.0.4", draft.Version);
        Assert.Equal("1.0.0", draft.DerivedFrom);
        Assert.Equal("user:fixture-operator", draft.Owner);
        Assert.Equal(DateTimeOffset.Parse("2026-08-15T00:00:00Z"), draft.CreatedAt);
        Assert.True(draft.SyncsToPeers);
        Assert.False(draft.SafeForStaging);

        var source = Assert.Single(rows, row => row.Version == "1.0.0");
        Assert.Equal("Published", source.Status);
        Assert.Equal("user:fixture-operator", source.Owner);
        Assert.Equal(DateTimeOffset.Parse("2026-08-01T08:00:00Z"), source.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-08-01T08:00:00Z"), source.UpdatedAt);
        Assert.Null(source.DerivedFrom);
        Assert.True(source.SyncsToPeers);
        Assert.False(source.SafeForStaging);
    }

    [Fact]
    public async Task Restore_rejects_unknown_versions_and_forms()
    {
        var client = new FixtureFormsAdminClient();

        var unknownVersion = await Assert.ThrowsAsync<FormsAdminException>(
            () => client.RestoreVersionAsync("incident-intake", "9.9.9"));
        var unknownForm = await Assert.ThrowsAsync<FormsAdminException>(
            () => client.RestoreVersionAsync("unknown-form", "1.0.0"));

        Assert.Equal(404, unknownVersion.Status);
        Assert.Equal("No revision '9.9.9' of form 'incident-intake' to restore.", unknownVersion.Message);
        Assert.Equal(404, unknownForm.Status);
    }

    private static void AssertDefinition(
        FormDefinitionSummary row,
        string formId,
        string version,
        string title,
        string updatedAt)
    {
        Assert.Equal(formId, row.FormId);
        Assert.Equal(version, row.Version);
        Assert.Equal(title, row.Title!.Values["en"]);
        Assert.Equal(DateTimeOffset.Parse(updatedAt), row.UpdatedAt);
        Assert.Equal("Tenant", row.CascadeLayer);
    }
}
