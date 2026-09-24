using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Configuration;

/// <summary>One edited definition inside a Proposed change, by public identity.</summary>
public sealed record ProposedEdit(string DefinitionKey, string PackageKey);

/// <summary>An immutable checkpoint with its authorship and rationale.</summary>
public sealed record SavedVersionSummary(int Ordinal, string Digest, string Author, string Rationale, DateTimeOffset SavedAt);

/// <summary>A recorded check and whether it still describes the state now being edited.</summary>
public sealed record RecordedCheck(string ReceiptId, string CheckedDigest, bool IsCurrent);

/// <summary>A Released package offered for activation. The digest is the artifact's own bytes.</summary>
public sealed record ReleasedPackageSummary(string Digest, string PackageKey, string Revision, string ProposalId,
    string SavedVersionDigest, string BaselineDigest, string ReleasedBy, DateTimeOffset ReleasedAt, string Signature);

/// <summary>
/// One Proposed change as the api reports it. <c>Detail</c> is the platform's RELEASED binding,
/// produced by <c>ConfigurationProposalDetail</c> in the api. Nothing in this lane derives, maps or
/// defaults a step label from it, and nothing here recomputes a digest.
/// </summary>
public sealed record ProposedChange(
    string Status,
    string TenantKey,
    string ProposalId,
    string BaselineDigest,
    string EffectiveDigest,
    string WorkingDigest,
    IReadOnlyList<ProposedEdit> Edits,
    int SavedVersionCount,
    SavedVersionSummary? SavedVersion,
    RecordedCheck? Check,
    ReleasedPackageSummary? ReleasedPackage,
    IReadOnlyList<ConfigurationRefusal> Refusals,
    IReadOnlyDictionary<string, string> Detail);

public sealed record AutosaveEdit(string DefinitionKey, string PackageKey, string BodyJson);

public sealed class ConfigurationProposalException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public interface IConfigurationProposalClient
{
    Task<ProposedChange> StartAsync(string proposalId, CancellationToken cancellationToken = default);
    Task<ProposedChange> ReadAsync(string proposalId, CancellationToken cancellationToken = default);
    Task<ProposedChange> AutosaveAsync(string proposalId, AutosaveEdit edit, CancellationToken cancellationToken = default);
    Task<ProposedChange> SaveVersionAsync(string proposalId, string rationale, CancellationToken cancellationToken = default);
    Task<ProposedChange> RecordCheckAsync(string proposalId, string receiptId, CancellationToken cancellationToken = default);
    Task<ProposedChange> ReleaseAsync(string proposalId, int ordinal, string packageKey, string revision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReleasedPackageSummary>> OfferedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// T-461. Transport over the api's propose, save and release routes, the Blazor twin of
/// <c>apps/react/src/admin/configuration/client/proposalClient.ts</c>.
/// </summary>
public sealed class HttpConfigurationProposalClient(HttpClient httpClient) : IConfigurationProposalClient
{
    private const string RouteBase = "api/local-node/configuration";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ProposedChange> StartAsync(string proposalId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{RouteBase}/proposals", new { proposalId }, cancellationToken);

    public Task<ProposedChange> ReadAsync(string proposalId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"{RouteBase}/proposals/{Uri.EscapeDataString(proposalId)}", null, cancellationToken);

    public Task<ProposedChange> AutosaveAsync(string proposalId, AutosaveEdit edit, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"{RouteBase}/proposals/{Uri.EscapeDataString(proposalId)}/edits", edit, cancellationToken);

    public Task<ProposedChange> SaveVersionAsync(string proposalId, string rationale, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{RouteBase}/proposals/{Uri.EscapeDataString(proposalId)}/versions", new { rationale }, cancellationToken);

    public Task<ProposedChange> RecordCheckAsync(string proposalId, string receiptId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{RouteBase}/proposals/{Uri.EscapeDataString(proposalId)}/checks", new { receiptId }, cancellationToken);

    public Task<ProposedChange> ReleaseAsync(string proposalId, int ordinal, string packageKey, string revision,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{RouteBase}/proposals/{Uri.EscapeDataString(proposalId)}/release",
            new { ordinal, packageKey, revision }, cancellationToken);

    public async Task<IReadOnlyList<ReleasedPackageSummary>> OfferedAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{RouteBase}/releases", cancellationToken);
        if (!response.IsSuccessStatusCode) throw await ToExceptionAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ReleasedPackageSummary[]>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The configuration service returned an empty response.");
    }

    // 422 is how the api reports a release refusal, and its body IS the released detail carrying the
    // refusal. Treating it as a transport failure would drop the one outcome the author most needs
    // to see, so an unprocessable response is read, not thrown.
    private async Task<ProposedChange> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.UnprocessableEntity)
            throw await ToExceptionAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProposedChange>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The configuration service returned an empty response.");
    }

    private static async Task<ConfigurationProposalException> ToExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var code = body.RootElement.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : $"http.{(int)response.StatusCode}";
            return new ConfigurationProposalException((int)response.StatusCode, code);
        }
        catch (JsonException)
        {
            return new ConfigurationProposalException((int)response.StatusCode, $"http.{(int)response.StatusCode}");
        }
    }
}
