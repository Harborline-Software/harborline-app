namespace Harborline.App.Blazor.ReferenceHost.Admin.Views;

/// <summary>
/// Provides the read-only views administration data seam used by the Blazor surface.
/// </summary>
public interface IViewsAdminClient
{
    /// <summary>Lists one head revision per report-definition key.</summary>
    Task<IReadOnlyList<ViewDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Gets the head revision's full detail.</summary>
    Task<ViewDefinitionDetail> GetDefinitionAsync(string key, CancellationToken ct = default);

    /// <summary>Lists the full history newest first with the ordering rule that produced it.</summary>
    Task<ViewVersionList> ListVersionsAsync(string key, CancellationToken ct = default);
}
