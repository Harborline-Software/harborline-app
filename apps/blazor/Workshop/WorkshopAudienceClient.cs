using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Workshop;

/// <summary>Routes Workshop operations to the audience declared by the node API, never by response status.</summary>
public sealed class WorkshopAudienceClient(BrowserWorkshopCatalogueClient browser, HttpWorkshopCatalogueClient desktop) : IWorkshopCatalogueClient
{
    public Task<WorkshopCatalogueEntry> ReadViewAsync(string viewId, CancellationToken cancellationToken = default) => browser.ReadViewAsync(viewId, cancellationToken);
    public Task<WorkshopCatalogueEntry> ReadFormAsync(string id, string? version = null, CancellationToken cancellationToken = default) => browser.ReadFormAsync(id, version, cancellationToken);
    public Task<WorkshopCatalogueList> ListAsync(string kind, CancellationToken cancellationToken = default) => browser.ListAsync(kind, cancellationToken);
    // API 4750c148: HostedPackComposerApiEndpoint and HostedAuthorizationAdminApiEndpoint
    // require the positive desktop-plane feature for export/verify and audit traces. These
    // exact operations retain server authority; interactive catalogue/install/activate do not.
    public Task<JsonElement> ReadJsonAsync(string path, CancellationToken cancellationToken = default) =>
        IsAuditTrace(path) ? desktop.ReadJsonAsync(path, cancellationToken) : browser.ReadJsonAsync(path, cancellationToken);
    public Task<JsonElement> PostJsonAsync(string path, object body, CancellationToken cancellationToken = default) =>
        Path(path) is "api/local-node/packs/export" or "api/local-node/packs/export?validateOnly=true"
            ? desktop.PostJsonAsync(path, body, cancellationToken) : browser.PostJsonAsync(path, body, cancellationToken);
    public Task<JsonElement> PostArtifactAsync(string path, byte[] artifact, CancellationToken cancellationToken = default) =>
        Path(path) == "api/local-node/packs/verify"
            ? desktop.PostArtifactAsync(path, artifact, cancellationToken) : browser.PostArtifactAsync(path, artifact, cancellationToken);
    public Task<byte[]> ExportAsync(JsonElement candidate, CancellationToken cancellationToken = default) => desktop.ExportAsync(candidate, cancellationToken);

    private static string Path(string path) => path.StartsWith('/', StringComparison.Ordinal) ? path[1..] : path;
    private static bool IsAuditTrace(string path)
    {
        const string prefix = "api/local-node/authorization/traces/";
        var normalized = Path(path);
        if (!normalized.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var segment = normalized[prefix.Length..];
        if (string.IsNullOrEmpty(segment)) return false;
        var id = Uri.UnescapeDataString(segment);
        return id is not ("." or "..") && !id.Any(character => character is '/' or '\\' or '?' or '#')
            && string.Equals(Uri.EscapeDataString(id), segment, StringComparison.OrdinalIgnoreCase);
    }
}
