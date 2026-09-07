namespace Harborline.App.Blazor.ReferenceHost.Admin.Forms;

/// <summary>
/// Provides the forms administration data seam used by the Blazor surface.
/// </summary>
public interface IFormsAdminClient
{
    /// <summary>Lists the published form-definition heads.</summary>
    Task<IReadOnlyList<FormDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Lists every retained version of a form definition.</summary>
    Task<IReadOnlyList<FormVersionSummary>> ListVersionsAsync(string formId, CancellationToken ct = default);

    /// <summary>Creates a draft derived from a retained form version.</summary>
    Task<RestoreResult> RestoreVersionAsync(string formId, string version, CancellationToken ct = default);
}
