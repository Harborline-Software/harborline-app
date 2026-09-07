using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Admin.DataExchange;

/// <summary>
/// Represents the head revision of a data exchange definition.
/// </summary>
/// <param name="Key">The stable report-definition key.</param>
/// <param name="Version">The head version.</param>
/// <param name="Title">The report title.</param>
/// <param name="ExchangeKind">The report kind.</param>
/// <param name="CascadeLayer">The cascade layer.</param>
public sealed record DataExchangeDefinitionSummary(
    string Key, string Version, string Title, string ExchangeKind, string CascadeLayer);

/// <summary>
/// Represents the full detail of a data exchange definition's head revision.
/// </summary>
/// <param name="Key">The stable report-definition key.</param>
/// <param name="Version">The head version.</param>
/// <param name="Title">The report title.</param>
/// <param name="ExchangeKind">The report kind.</param>
/// <param name="CascadeLayer">The cascade layer.</param>
/// <param name="SchemaVersion">The parameter schema version.</param>
/// <param name="Settings">The report settings.</param>
/// <param name="Provenance">The pack-projection provenance.</param>
public sealed record DataExchangeDefinitionDetail(
    string Key, string Version, string Title, string ExchangeKind, string CascadeLayer,
    int SchemaVersion, JsonElement Settings, JsonElement Provenance);

/// <summary>
/// Represents a data exchange definition's retained versions and their ordering rule.
/// </summary>
/// <param name="Ordering">The ordering rule applied to the versions.</param>
/// <param name="Versions">The retained versions, newest first.</param>
public sealed record DataExchangeVersionList(string Ordering, IReadOnlyList<DataExchangeDefinitionSummary> Versions);

/// <summary>
/// Represents a data exchange administration request failure.
/// </summary>
/// <param name="status">The HTTP-compatible status code.</param>
/// <param name="message">The failure message.</param>
/// <param name="code">The machine-readable error code, when the api sent one.</param>
/// <param name="detail">The named detail fields the code carries, when any.</param>
public sealed class DataExchangeAdminException(
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
