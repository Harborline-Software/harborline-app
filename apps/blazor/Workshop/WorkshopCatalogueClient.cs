using System.Net.Http.Json;
using System.Text.Json;
using Harborline.UIAdapters.Blazor.Components.DataDisplay;

namespace Harborline.App.Blazor.ReferenceHost.Workshop;

public interface IWorkshopCatalogueClient
{
    Task<WorkshopCatalogueEntry> ReadViewAsync(string itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkshopCatalogueEntry>> ListAsync(string kind, CancellationToken cancellationToken = default);
}

public sealed record WorkshopLocalizedText(string DefaultLocale, IReadOnlyDictionary<string, string> Values);

public sealed record WorkshopCatalogueEntry(
    string Id,
    string Version,
    string Status,
    WorkshopLocalizedText? Title,
    JsonElement Body,
    ViewRenderPlan? RenderPlan);

public sealed class HttpWorkshopCatalogueClient(HttpClient httpClient) : IWorkshopCatalogueClient
{
    public async Task<WorkshopCatalogueEntry> ReadViewAsync(string itemId, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<WorkshopCatalogueEntry>(
            $"api/local-node/catalogue/definitions/ViewDefinition/platform.list.{Uri.EscapeDataString(itemId)}", cancellationToken)
        ?? throw new InvalidOperationException("The seeded Workshop view returned no definition.");

    public async Task<IReadOnlyList<WorkshopCatalogueEntry>> ListAsync(string kind, CancellationToken cancellationToken = default) =>
        (await httpClient.GetFromJsonAsync<WorkshopCatalogueList>(
            $"api/local-node/catalogue/definitions?kind={Uri.EscapeDataString(kind)}", cancellationToken))?.Entries
        ?? throw new InvalidOperationException("The Workshop catalogue returned no list.");

    private sealed record WorkshopCatalogueList(IReadOnlyList<WorkshopCatalogueEntry> Entries);
}
