using System.Net.Http.Json;
using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Scheduling;

/// <summary>
/// Provides scheduling administration operations through the local-node HTTP API.
/// </summary>
public sealed class HttpSchedulingAdminClient(HttpClient http) : ISchedulingAdminClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<SchedulingDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/local-node/scheduling/definitions", ct);
        // A 404 on the LIST route does not mean "no definitions" — an empty tenant answers 200 [].
        // It means the route is not mapped at all, because the node registers the scheduling family
        // only when LocalNode:SchedulingDogfood:Enabled is true (false in shipped config). Saying so
        // is the difference between an operator flipping a flag and an operator filing a bug.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new SchedulingAdminException(404, "scheduling.family_not_enabled");
        }

        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<SummaryWire[]>(JsonOptions, ct) ?? [];
        return wire.Select(item => new SchedulingDefinitionSummary(
            item.Id, item.Revision, item.Title, item.UpdatedAt, item.UpdatedBy)).ToArray();
    }

    /// <inheritdoc />
    public async Task<SchedulingDefinitionView> GetDefinitionAsync(
        string definitionId,
        CancellationToken ct = default)
    {
        var escapedDefinitionId = Uri.EscapeDataString(definitionId);
        using var response = await http.GetAsync(
            $"api/local-node/scheduling/definitions/{escapedDefinitionId}", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<ViewWire>(JsonOptions, ct)
            ?? throw new JsonException("The definition response body was empty.");
        return new SchedulingDefinitionView(
            wire.Id, wire.Revision, wire.Definition, wire.UpdatedAt, wire.UpdatedBy);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SchedulingDefinitionSummary>> ListVersionsAsync(
        string definitionId,
        CancellationToken ct = default)
    {
        var escapedDefinitionId = Uri.EscapeDataString(definitionId);
        using var response = await http.GetAsync(
            $"api/local-node/scheduling/definitions/{escapedDefinitionId}/versions", ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<SummaryWire[]>(JsonOptions, ct) ?? [];
        return wire.Select(item => new SchedulingDefinitionSummary(
            item.Id, item.Revision, item.Title, item.UpdatedAt, item.UpdatedBy)).ToArray();
    }

    /// <inheritdoc />
    public async Task<RestoreResult> RestoreRevisionAsync(
        string definitionId,
        int revision,
        CancellationToken ct = default)
    {
        var escapedDefinitionId = Uri.EscapeDataString(definitionId);
        using var response = await http.PostAsJsonAsync(
            $"api/local-node/scheduling/definitions/{escapedDefinitionId}/restore",
            new { revision }, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        var wire = await response.Content.ReadFromJsonAsync<RestoreWire>(JsonOptions, ct)
            ?? throw new JsonException("The restore response body was empty.");
        return new RestoreResult(wire.DefinitionId, wire.Revision, wire.RestoredFrom);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await AdminError.ReadAsync(response, ct);
        throw new SchedulingAdminException(
            (int)response.StatusCode, error.Message, error.Code, error.Detail);
    }

    private sealed record SummaryWire(
        string Id, int Revision, string Title, DateTimeOffset UpdatedAt, string UpdatedBy);

    private sealed record ViewWire(
        string Id, int Revision, JsonElement Definition, DateTimeOffset UpdatedAt, string UpdatedBy);

    private sealed record RestoreWire(string DefinitionId, int Revision, int RestoredFrom);
}
