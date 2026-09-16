using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Transport;

namespace Harborline.App.Blazor.ReferenceHost.Workshop;

/// <summary>Interactive catalogue requests use only the browser's selected session.</summary>
public sealed class BrowserWorkshopCatalogueClient(BrowserSelectedSessionTransport transport) : IWorkshopCatalogueClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<WorkshopCatalogueEntry> ReadViewAsync(string viewId, CancellationToken cancellationToken = default) =>
        ReadEntryAsync($"/api/local-node/catalogue/definitions/ViewDefinition/{Uri.EscapeDataString(viewId)}", cancellationToken);

    public Task<WorkshopCatalogueEntry> ReadFormAsync(string id, string? version = null, CancellationToken cancellationToken = default) =>
        ReadEntryAsync($"/api/local-node/catalogue/definitions/FormDefinition/{Uri.EscapeDataString(id)}"
            + (version is null ? string.Empty : $"?version={Uri.EscapeDataString(version)}"), cancellationToken);

    private async Task<WorkshopCatalogueEntry> ReadEntryAsync(string path, CancellationToken cancellationToken)
    {
        var json = await ReadJsonAsync(path, cancellationToken);
        var entry = json.Deserialize<WorkshopCatalogueEntry>(JsonOptions) ?? throw new JsonException("The catalogue returned no definition.");
        return entry with { CompiledBindings = json.TryGetProperty("renderPlan", out var plan)
            && plan.ValueKind == JsonValueKind.Object && plan.TryGetProperty("bindings", out var bindings) ? bindings.Clone() : default };
    }

    public async Task<WorkshopCatalogueList> ListAsync(string kind, CancellationToken cancellationToken = default) =>
        (await ReadJsonAsync($"/api/local-node/catalogue/definitions?kind={Uri.EscapeDataString(kind)}", cancellationToken))
        .Deserialize<WorkshopCatalogueList>(JsonOptions) ?? throw new JsonException("The Workshop catalogue returned no list.");

    public async Task<JsonElement> ReadJsonAsync(string path, CancellationToken cancellationToken = default) =>
        Parse(await transport.SendAsync(Path(path), cancellationToken: cancellationToken));

    public async Task<JsonElement> PostJsonAsync(string path, object body, CancellationToken cancellationToken = default) =>
        Parse(await transport.SendAsync(Path(path), "POST", JsonSerializer.Serialize(body, JsonOptions), cancellationToken: cancellationToken));
    public async Task<JsonElement> PostArtifactAsync(string path, byte[] artifact, CancellationToken cancellationToken = default) =>
        Parse(await transport.SendAsync(Path(path), "POST", artifact, "application/octet-stream", cancellationToken));
    public async Task<byte[]> ExportAsync(JsonElement candidate, CancellationToken cancellationToken = default)
    {
        var response = await transport.SendBytesAsync("/api/local-node/packs/export", "POST", JsonSerializer.Serialize(candidate, JsonOptions), cancellationToken: cancellationToken);
        if (response.Status is < 200 or >= 300) throw new WorkshopRequestException(response.Status, response.Body);
        return response.Bytes ?? throw new InvalidOperationException("The selected-session export returned no artifact bytes.");
    }

    private static string Path(string path) => path.StartsWith("api/", StringComparison.Ordinal) ? "/" + path : path;
    private static JsonElement Parse(SelectedSessionResponse response)
    {
        if (response.Status is < 200 or >= 300) throw new WorkshopRequestException(response.Status, response.Body);
        using var json = JsonDocument.Parse(response.Body);
        return json.RootElement.Clone();
    }
}
