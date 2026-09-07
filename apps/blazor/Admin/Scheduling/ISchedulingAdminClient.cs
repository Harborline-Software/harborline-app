namespace Harborline.App.Blazor.ReferenceHost.Admin.Scheduling;

/// <summary>
/// Provides the scheduling administration data seam used by the Blazor surface.
/// </summary>
public interface ISchedulingAdminClient
{
    /// <summary>Lists the current scheduling-definition heads.</summary>
    Task<IReadOnlyList<SchedulingDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Reads the current head view of a scheduling definition.</summary>
    Task<SchedulingDefinitionView> GetDefinitionAsync(string definitionId, CancellationToken ct = default);

    /// <summary>Lists every retained revision of a scheduling definition, newest first.</summary>
    Task<IReadOnlyList<SchedulingDefinitionSummary>> ListVersionsAsync(string definitionId, CancellationToken ct = default);

    /// <summary>Restores revision N as the new head.</summary>
    Task<RestoreResult> RestoreRevisionAsync(string definitionId, int revision, CancellationToken ct = default);
}
