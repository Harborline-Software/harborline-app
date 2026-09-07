namespace Harborline.App.Blazor.ReferenceHost.Admin.Reports;

/// <summary>
/// Provides the read-only reports administration data seam used by the Blazor surface.
/// </summary>
public interface IReportsAdminClient
{
    /// <summary>Lists one head revision per report-definition key.</summary>
    Task<IReadOnlyList<ReportDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Gets the head revision's full detail.</summary>
    Task<ReportDefinitionDetail> GetDefinitionAsync(string key, CancellationToken ct = default);

    /// <summary>Lists the full history newest first with the ordering rule that produced it.</summary>
    Task<ReportVersionList> ListVersionsAsync(string key, CancellationToken ct = default);
}
