using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Configuration;

/// <summary>The effective generation. This read carries generation identity only, never a bound detail.</summary>
public sealed record EffectiveGeneration(string Digest, string Algorithm, JsonElement References);

/// <summary>A stable refusal code, its target, and a message.</summary>
public sealed record ConfigurationRefusal(string Code, string Target, string Message);

/// <summary>An immutable projection reference.</summary>
public sealed record ProjectionReference(string Key, string Revision, string Digest);

/// <summary>
/// A prepare or activate outcome. <c>Detail</c> is the platform's RELEASED binding, produced by
/// <c>ConfigurationActivationDetail.Bind</c> in the api. Nothing in this lane derives, maps or
/// defaults a status from it.
/// </summary>
public sealed record ConfigurationActivationOutcome(
    string Status,
    string TenantKey,
    string CandidateDigest,
    string ExpectedBaselineDigest,
    string EffectiveDigest,
    IReadOnlyList<ConfigurationRefusal> Refusals,
    ProjectionReference? Projection = null,
    IReadOnlyDictionary<string, string>? Detail = null,
    bool Acknowledged = false);

public sealed record ConfigurationOwnership(string DefinitionKey, string PackageKey);

public sealed record PrepareConfigurationRequest(
    string ExpectedBaselineDigest,
    IReadOnlyList<string> ActivePackageKeys,
    IReadOnlyList<ConfigurationOwnership>? Ownership = null);

public sealed record EvidenceIntent(string Id, string Reason);

public sealed record ActivateConfigurationRequest(string ExpectedBaselineDigest, string CandidateDigest, EvidenceIntent EvidenceIntent);

public sealed class ConfigurationActivationException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public interface IConfigurationActivationClient
{
    Task<EffectiveGeneration> ReadEffectiveAsync(CancellationToken cancellationToken = default);
    Task<ConfigurationActivationOutcome> PrepareAsync(PrepareConfigurationRequest request, CancellationToken cancellationToken = default);
    Task<ConfigurationActivationOutcome> ActivateAsync(ActivateConfigurationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// T-460. Transport over the api's atomic configuration activation routes, following this lane's
/// <c>HttpAuthorizationAdminClient</c>.
/// </summary>
public sealed class HttpConfigurationActivationClient(HttpClient httpClient) : IConfigurationActivationClient
{
    private const string RouteBase = "api/local-node/configuration";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EffectiveGeneration> ReadEffectiveAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{RouteBase}/effective", cancellationToken);
        if (!response.IsSuccessStatusCode) throw await ToExceptionAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<EffectiveGeneration>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The configuration service returned an empty response.");
    }

    public Task<ConfigurationActivationOutcome> PrepareAsync(PrepareConfigurationRequest request, CancellationToken cancellationToken = default) =>
        PostAsync($"{RouteBase}/prepare", request, cancellationToken);

    public Task<ConfigurationActivationOutcome> ActivateAsync(ActivateConfigurationRequest request, CancellationToken cancellationToken = default) =>
        PostAsync($"{RouteBase}/activate", request, cancellationToken);

    // 422 is how the api reports a refusal, and its body IS the released detail. Treating it as a
    // transport failure would discard the one outcome this surface exists to distinguish, so an
    // unprocessable response is read, not thrown.
    private async Task<ConfigurationActivationOutcome> PostAsync(string path, object request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(path, request, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.UnprocessableEntity)
            throw await ToExceptionAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ConfigurationActivationOutcome>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The configuration service returned an empty response.");
    }

    private static async Task<ConfigurationActivationException> ToExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var code = body.RootElement.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : $"http.{(int)response.StatusCode}";
            return new ConfigurationActivationException((int)response.StatusCode, code);
        }
        catch (JsonException)
        {
            return new ConfigurationActivationException((int)response.StatusCode, $"http.{(int)response.StatusCode}");
        }
    }
}
