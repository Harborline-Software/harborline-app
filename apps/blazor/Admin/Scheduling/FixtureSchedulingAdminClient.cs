using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Scheduling;

/// <summary>
/// Provides deterministic, in-memory scheduling administration data for the standalone reference host.
/// </summary>
public sealed class FixtureSchedulingAdminClient : ISchedulingAdminClient
{
    private static readonly DateTimeOffset RestoreEpoch = DateTimeOffset.Parse("2026-08-20T00:00:00Z");
    private readonly object gate = new();
    private readonly Dictionary<string, List<RevisionRow>> histories = new(StringComparer.Ordinal)
    {
        ["inspection-protocol"] =
        [
            Row(3, "Inspection protocol", "2026-08-18T09:00:00Z", "user:fixture-scheduler"),
            Row(2, "Inspection protocol", "2026-08-12T10:00:00Z", "user:fixture-scheduler"),
            Row(1, "Inspection protocol", "2026-08-05T08:00:00Z", "user:fixture-scheduler"),
        ],
        ["move-in-checklist"] =
        [
            Row(1, "Move-in checklist", "2026-08-10T14:30:00Z", "user:fixture-scheduler"),
        ],
        ["turnover-schedule"] =
        [
            Row(2, "Turnover schedule", "2026-08-15T11:15:00Z", "user:fixture-admin"),
            Row(1, "Turnover schedule", "2026-08-09T16:45:00Z", "user:fixture-scheduler"),
        ],
    };
    private readonly string[] definitionOrder = ["inspection-protocol", "move-in-checklist", "turnover-schedule"];
    private int restoreCounter;

    /// <inheritdoc />
    public Task<IReadOnlyList<SchedulingDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<SchedulingDefinitionSummary>>(
                definitionOrder.Select(id => ToSummary(id, histories[id][0])).ToArray());
        }
    }

    /// <inheritdoc />
    public Task<SchedulingDefinitionView> GetDefinitionAsync(string definitionId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!histories.TryGetValue(definitionId, out var rows))
            {
                throw new SchedulingAdminException(404, "scheduling.draft.not_found");
            }

            var head = rows[0];
            return Task.FromResult(new SchedulingDefinitionView(
                definitionId, head.Revision, ParseDefinition(head.DefinitionJson), head.UpdatedAt, head.UpdatedBy));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SchedulingDefinitionSummary>> ListVersionsAsync(
        string definitionId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!histories.TryGetValue(definitionId, out var rows))
            {
                throw new SchedulingAdminException(404, "scheduling.draft.not_found");
            }

            return Task.FromResult<IReadOnlyList<SchedulingDefinitionSummary>>(
                rows.Select(row => ToSummary(definitionId, row)).ToArray());
        }
    }

    /// <inheritdoc />
    public Task<RestoreResult> RestoreRevisionAsync(
        string definitionId,
        int revision,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            // An unknown definition and an unknown revision BOTH answer revision_not_found here,
            // because that is what the real route does: it looks for the revision first, so it
            // never gets far enough to distinguish the two. Verified against a live node
            // 2026-08-22. The React lane already matched; this lane said not_found and nothing
            // caught it — the fixture-parity fence compares seeded DATA, not behaviour.
            var source = histories.TryGetValue(definitionId, out var rows)
                ? rows.FirstOrDefault(row => row.Revision == revision)
                : null;
            if (rows is null || source is null)
            {
                throw new SchedulingAdminException(404, "scheduling.draft.revision_not_found");
            }

            var minted = rows[0].Revision + 1;
            rows.Insert(0, new RevisionRow(
                minted, source.DefinitionJson, RestoreEpoch.AddMinutes(restoreCounter++), "user:fixture-operator"));
            return Task.FromResult(new RestoreResult(definitionId, minted, revision));
        }
    }

    private static RevisionRow Row(int revision, string title, string timestamp, string updatedBy) =>
        new(revision, $"{{\"title\":\"{title}\",\"cadence\":\"weekly\"}}", DateTimeOffset.Parse(timestamp), updatedBy);

    private static SchedulingDefinitionSummary ToSummary(string id, RevisionRow row) =>
        new(id, row.Revision, ParseDefinition(row.DefinitionJson).GetProperty("title").GetString()!, row.UpdatedAt, row.UpdatedBy);

    private static JsonElement ParseDefinition(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed record RevisionRow(
        int Revision, string DefinitionJson, DateTimeOffset UpdatedAt, string UpdatedBy);
}
