using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harborline.UIAdapters.Blazor.Components.DataDisplay;

namespace Harborline.App.Blazor.ReferenceHost.Workshop;

public interface IWorkshopCatalogueClient
{
    Task<WorkshopCatalogueEntry> ReadViewAsync(string viewId, CancellationToken cancellationToken = default);
    Task<WorkshopCatalogueList> ListAsync(string kind, CancellationToken cancellationToken = default);
    Task<WorkshopCatalogueEntry> ReadFormAsync(string id, string? version = null, CancellationToken cancellationToken = default);
    Task<JsonElement> ReadJsonAsync(string path, CancellationToken cancellationToken = default);
    Task<JsonElement> PostJsonAsync(string path, object body, CancellationToken cancellationToken = default);
    Task<JsonElement> PostArtifactAsync(string path, byte[] artifact, CancellationToken cancellationToken = default);
    Task<byte[]> ExportAsync(JsonElement candidate, CancellationToken cancellationToken = default);
}

public sealed record WorkshopLocalizedText(string DefaultLocale, IReadOnlyDictionary<string, string> Values);
public sealed record WorkshopCatalogueList(IReadOnlyList<WorkshopCatalogueEntry> Entries, IReadOnlyList<JsonElement> KindsUnavailable);
public sealed record WorkshopCatalogueProvenance(string? PackKey, string? PackVersion, string Kind);

public sealed record WorkshopCatalogueEntry(
    string Id,
    string Version,
    string Status,
    WorkshopLocalizedText? Title,
    JsonElement Body,
    ViewRenderPlan? RenderPlan)
{
    public JsonElement? Kind { get; init; }
    public WorkshopCatalogueProvenance? Provenance { get; init; }
    public bool Sealed { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string? DefinitionHash { get; init; }
    [JsonIgnore] public JsonElement CompiledBindings { get; init; }
}

public sealed class WorkshopRequestException(int statusCode, string responseBody)
    : HttpRequestException($"Request failed ({statusCode}): {responseBody}", null, (System.Net.HttpStatusCode)statusCode)
{
    public string ResponseBody { get; } = responseBody;
}

public sealed class HttpWorkshopCatalogueClient(HttpClient httpClient) : IWorkshopCatalogueClient
{
    public async Task<WorkshopCatalogueEntry> ReadViewAsync(string viewId, CancellationToken cancellationToken = default) =>
        ReadEntry(await ReadCatalogueJsonAsync($"api/local-node/catalogue/definitions/ViewDefinition/{Uri.EscapeDataString(viewId)}", cancellationToken));

    public Task<WorkshopCatalogueEntry> ReadFormAsync(string id, string? version = null, CancellationToken cancellationToken = default) =>
        ReadEntryAsync($"api/local-node/catalogue/definitions/FormDefinition/{Uri.EscapeDataString(id)}"
            + (version is null ? string.Empty : $"?version={Uri.EscapeDataString(version)}"), cancellationToken);

    private async Task<WorkshopCatalogueEntry> ReadEntryAsync(string path, CancellationToken cancellationToken)
        => ReadEntry(await ReadJsonAsync(path, cancellationToken));

    private static WorkshopCatalogueEntry ReadEntry(JsonElement json)
    {
        var entry = json.Deserialize<WorkshopCatalogueEntry>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new JsonException("The catalogue returned no definition.");
        return entry with { CompiledBindings = json.TryGetProperty("renderPlan", out var plan)
            && plan.ValueKind == JsonValueKind.Object && plan.TryGetProperty("bindings", out var bindings)
                ? bindings.Clone() : default };
    }

    public async Task<JsonElement> ReadJsonAsync(string path, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        return await ReadResponseAsync(response, cancellationToken);
    }

    public async Task<JsonElement> PostJsonAsync(string path, object body, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(path, body, cancellationToken);
        return await ReadResponseAsync(response, cancellationToken);
    }

    public async Task<JsonElement> PostArtifactAsync(string path, byte[] artifact, CancellationToken cancellationToken = default)
    {
        using var content = new ByteArrayContent(artifact);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await httpClient.PostAsync(path, content, cancellationToken);
        return await ReadResponseAsync(response, cancellationToken);
    }

    public async Task<byte[]> ExportAsync(JsonElement candidate, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/local-node/packs/export", candidate, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static async Task<JsonElement> ReadResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            throw new WorkshopRequestException((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<WorkshopCatalogueList> ListAsync(string kind, CancellationToken cancellationToken = default) =>
        (await ReadCatalogueJsonAsync($"api/local-node/catalogue/definitions?kind={Uri.EscapeDataString(kind)}", cancellationToken))
        .Deserialize<WorkshopCatalogueList>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidOperationException("The Workshop catalogue returned no list.");

    private async Task<JsonElement> ReadCatalogueJsonAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var message = $"Workshop catalogue request failed ({(int)response.StatusCode}).";
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            message = CatalogueReadMessage((int)response.StatusCode, document.RootElement, message);
        }
        catch (JsonException) { }
        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static string CatalogueReadMessage(int status, JsonElement body, string fallback)
    {
        if (body.ValueKind != JsonValueKind.Object || Text(body, "code") is not { } code) return fallback;
        if ((status == 400 && code == "catalogue.kind_unknown") || (status == 404 && code == "catalogue.definition_not_found"))
            return $"{fallback} {code}";
        // The public authorization refusal is rendered by the server; classified Diagnostic and unknown members stay out.
        if (status != 403 || code.Trim() != code || !System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z][a-z0-9]*(?:[._-][a-z0-9]+)+$")
            || Text(body, "permission") != "catalogue:read" || Text(body, "title") is not { } title
            || Text(body, "detail") is not { } detail || Text(body, "remediation") is not { } remediation)
            return fallback;
        var auditId = Text(body, "auditId");
        var correlationId = Text(body, "correlationId");
        if ((body.TryGetProperty("auditId", out _) && (auditId is null || auditId.Length != 36 || !Guid.TryParseExact(auditId, "D", out _)))
            || (body.TryGetProperty("correlationId", out _) && (correlationId is null || correlationId.Length != 36 || !Guid.TryParseExact(correlationId, "D", out _))))
            return fallback;
        var parts = new List<string> { fallback, code, "catalogue:read", title, detail, remediation };
        if (auditId is not null) parts.Add($"Audit ID: {auditId}");
        if (correlationId is not null) parts.Add($"Correlation ID: {correlationId}");
        return string.Join(" ", parts);
    }

    private static string? Text(JsonElement body, string name) => body.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
}
