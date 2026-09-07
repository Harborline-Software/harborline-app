namespace Harborline.App.Blazor.ReferenceHost.Admin.Forms;

/// <summary>
/// Provides deterministic, in-memory forms administration data for the standalone reference host.
/// </summary>
public sealed class FixtureFormsAdminClient : IFormsAdminClient
{
    private static readonly DateTimeOffset RestoreEpoch = DateTimeOffset.Parse("2026-08-15T00:00:00Z");
    private readonly object gate = new();
    private readonly IReadOnlyList<FormDefinitionSummary> definitions =
    [
        new("incident-intake", "1.0.3", new InternationalizedText("en", new Dictionary<string, string> { ["en"] = "Incident intake" }), DateTimeOffset.Parse("2026-08-05T12:00:00Z"), "Tenant"),
        new("vessel-registration", "2.1.4", new InternationalizedText("en", new Dictionary<string, string> { ["en"] = "Vessel registration" }), DateTimeOffset.Parse("2026-08-10T15:45:00Z"), "Tenant"),
        // 'Pack' on purpose (review P2-12): fixture-backed screens must exercise the non-'Tenant'
        // cascade-layer render path too, not fabricate 'Tenant' on every row.
        new("crew-manifest", "1.0.0", null, DateTimeOffset.Parse("2026-08-03T08:00:00Z"), "Pack"),
    ];
    private readonly Dictionary<string, List<FormVersionSummary>> versions = new(StringComparer.Ordinal)
    {
        ["incident-intake"] =
        [
            Row("incident-intake", "1.0.3", "Published", "user:fixture-admin", "2026-08-05T12:00:00Z", "1.0.2"),
            Row("incident-intake", "1.0.2", "Draft", "user:fixture-admin", "2026-08-04T09:30:00Z", "1.0.0"),
            Row("incident-intake", "1.0.1", "Published", "user:fixture-operator", "2026-08-02T10:00:00Z", null),
            Row("incident-intake", "1.0.0", "Published", "user:fixture-operator", "2026-08-01T08:00:00Z", null),
        ],
        ["vessel-registration"] =
        [
            Row("vessel-registration", "2.1.4", "Published", null, "2026-08-10T15:45:00Z", null),
            Row("vessel-registration", "2.1.3", "Deprecated", null, "2026-08-09T15:45:00Z", null),
        ],
        ["crew-manifest"] =
        [
            Row("crew-manifest", "1.0.0", "Published", null, "2026-08-03T08:00:00Z", null),
        ],
    };
    private int restoreCounter;

    /// <inheritdoc />
    public Task<IReadOnlyList<FormDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<FormDefinitionSummary>>(definitions.ToArray());
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FormVersionSummary>> ListVersionsAsync(string formId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            // The UI only navigates from the definitions list.
            var snapshot = versions.TryGetValue(formId, out var rows) ? rows.ToArray() : [];
            return Task.FromResult<IReadOnlyList<FormVersionSummary>>(snapshot);
        }
    }

    /// <inheritdoc />
    public Task<RestoreResult> RestoreVersionAsync(string formId, string version, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!versions.TryGetValue(formId, out var rows) || !rows.Any(row => row.Version == version))
            {
                throw new FormsAdminException(404, $"No revision '{version}' of form '{formId}' to restore.");
            }

            var maximum = rows.Select(row => System.Version.Parse(row.Version)).Max()!;
            var minted = $"{maximum.Major}.{maximum.Minor}.{maximum.Build + 1}";
            var timestamp = RestoreEpoch.AddMinutes(restoreCounter++);
            rows.Insert(0, new FormVersionSummary(
                formId, minted, "Draft", "user:fixture-operator", timestamp, timestamp, version, true, false));
            return Task.FromResult(new RestoreResult(formId, minted));
        }
    }

    private static FormVersionSummary Row(
        string formId, string version, string status, string? owner, string timestamp, string? derivedFrom)
    {
        var at = DateTimeOffset.Parse(timestamp);
        return new FormVersionSummary(formId, version, status, owner, at, at, derivedFrom, true, false);
    }
}
