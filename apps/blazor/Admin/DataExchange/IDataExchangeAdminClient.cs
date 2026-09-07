namespace Harborline.App.Blazor.ReferenceHost.Admin.DataExchange;

/// <summary>
/// Provides the read-only data exchange administration data seam used by the Blazor surface.
/// </summary>
public interface IDataExchangeAdminClient
{
    /// <summary>Lists one head revision per report-definition key.</summary>
    Task<IReadOnlyList<DataExchangeDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Gets the head revision's full detail.</summary>
    Task<DataExchangeDefinitionDetail> GetDefinitionAsync(string key, CancellationToken ct = default);

    /// <summary>Lists the full history newest first with the ordering rule that produced it.</summary>
    Task<DataExchangeVersionList> ListVersionsAsync(string key, CancellationToken ct = default);
}
