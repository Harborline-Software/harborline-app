using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Scheduling;

/// <summary>
/// Represents the current head of a scheduling definition.
/// </summary>
/// <param name="Id">The stable scheduling-definition identifier.</param>
/// <param name="Revision">The current integer revision.</param>
/// <param name="Title">The definition title.</param>
/// <param name="UpdatedAt">The time the revision was last updated.</param>
/// <param name="UpdatedBy">The identity that last updated the revision.</param>
public sealed record SchedulingDefinitionSummary(
    string Id, int Revision, string Title, DateTimeOffset UpdatedAt, string UpdatedBy);

/// <summary>
/// Represents the current scheduling-definition view.
/// </summary>
/// <param name="Id">The stable scheduling-definition identifier.</param>
/// <param name="Revision">The current integer revision.</param>
/// <param name="Definition">The scheduling-definition body.</param>
/// <param name="UpdatedAt">The time the revision was last updated.</param>
/// <param name="UpdatedBy">The identity that last updated the revision.</param>
public sealed record SchedulingDefinitionView(
    string Id, int Revision, JsonElement Definition, DateTimeOffset UpdatedAt, string UpdatedBy);

/// <summary>
/// Identifies the new head created by a restore operation.
/// </summary>
/// <param name="DefinitionId">The stable scheduling-definition identifier.</param>
/// <param name="Revision">The newly minted head revision.</param>
/// <param name="RestoredFrom">The source revision that was restored.</param>
public sealed record RestoreResult(string DefinitionId, int Revision, int RestoredFrom);

/// <summary>
/// Represents a scheduling administration request failure.
/// </summary>
/// <param name="status">The HTTP-compatible status code.</param>
/// <param name="message">The failure message.</param>
/// <param name="code">The machine-readable code the api sent, when any (ticket 094).</param>
/// <param name="detail">The named detail fields the code carries, when any (ticket 094).</param>
public sealed class SchedulingAdminException(
    int status,
    string message,
    string? code = null,
    IReadOnlyDictionary<string, string>? detail = null) : Exception(message)
{
    /// <summary>
    /// Gets the HTTP-compatible status code.
    /// </summary>
    public int Status { get; } = status;

    /// <summary>
    /// Gets the machine-readable code, or <see langword="null"/> when the body had none.
    /// </summary>
    public string? Code { get; } = code;

    /// <summary>
    /// Gets the named detail fields the code carries, when any.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Detail { get; } = detail;
}
