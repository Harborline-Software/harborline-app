namespace Harborline.App.Blazor.ReferenceHost.Admin.Forms;

/// <summary>
/// Represents localized text with a preferred locale and its available translations.
/// </summary>
/// <param name="DefaultLocale">The preferred locale key.</param>
/// <param name="Values">The translations keyed by locale.</param>
public sealed record InternationalizedText(string DefaultLocale, IReadOnlyDictionary<string, string> Values);

/// <summary>
/// Represents the published head of a form definition.
/// </summary>
/// <param name="FormId">The stable form identifier.</param>
/// <param name="Version">The published version.</param>
/// <param name="Title">The localized title, when present.</param>
/// <param name="UpdatedAt">The time the published head was last updated.</param>
/// <param name="CascadeLayer">The cascade layer, when known.</param>
public sealed record FormDefinitionSummary(
    string FormId, string Version, InternationalizedText? Title,
    DateTimeOffset UpdatedAt, string? CascadeLayer);

/// <summary>
/// Represents one retained version of a form definition.
/// </summary>
/// <param name="FormId">The stable form identifier.</param>
/// <param name="Version">The retained version.</param>
/// <param name="Status">The lifecycle status.</param>
/// <param name="Owner">The owner identity, when present.</param>
/// <param name="CreatedAt">The creation time.</param>
/// <param name="UpdatedAt">The last update time.</param>
/// <param name="DerivedFrom">The source version, when this revision was derived.</param>
/// <param name="SyncsToPeers">Whether the revision synchronizes to peers.</param>
/// <param name="SafeForStaging">Whether the revision is isolated for staging.</param>
public sealed record FormVersionSummary(
    string FormId, string Version, string Status, string? Owner,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? DerivedFrom,
    bool SyncsToPeers, bool SafeForStaging);

/// <summary>
/// Identifies the draft created by a restore operation.
/// </summary>
/// <param name="FormId">The stable form identifier.</param>
/// <param name="Version">The newly minted draft version.</param>
public sealed record RestoreResult(string FormId, string Version);

/// <summary>
/// Represents a forms administration request failure.
/// </summary>
/// <param name="status">The HTTP-compatible status code.</param>
/// <param name="message">The failure message.</param>
/// <param name="code">The machine-readable error code, when the api sent one.</param>
/// <param name="detail">The named detail fields the code carries, when any.</param>
public sealed class FormsAdminException(
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
    /// Gets the machine-readable error code the api sent, or <see langword="null"/> when the body
    /// carried none (ticket 094).
    /// </summary>
    public string? Code { get; } = code;

    /// <summary>
    /// Gets the named detail fields the api sent with the code, or <see langword="null"/> when the
    /// code carries no detail.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Detail { get; } = detail;
}
