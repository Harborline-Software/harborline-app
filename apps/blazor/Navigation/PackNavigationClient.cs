using System.Net.Http.Json;
using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Transport;
using Harborline.UIAdapters.Blazor.Components.Layout;

namespace Harborline.App.Blazor.ReferenceHost.Navigation;

public sealed record PackNavigationResponse(bool Configured, PackNavigationDeclaration? Pack);

public interface IPackNavigationClient
{
    Task<PackNavigationDeclaration?> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Navigation follows the browser's selected principal, never the server bootstrap client.</summary>
public sealed class BrowserPackNavigationClient(BrowserSelectedSessionTransport transport) : IPackNavigationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<PackNavigationDeclaration?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var receipt = await transport.SendAsync("/api/local-node/navigation/workspaces", cancellationToken: cancellationToken);
        if (receipt.Status is < 200 or >= 300) throw new InvalidOperationException("Unable to load application navigation. Retry the request.");
        var response = JsonSerializer.Deserialize<PackNavigationResponse>(receipt.Body, JsonOptions)
            ?? throw new InvalidOperationException("The navigation service returned no response.");
        if (response.Configured && response.Pack is null) throw new InvalidOperationException("The navigation service returned no declaration.");
        return response.Configured ? response.Pack : null;
    }
}

public sealed class HttpPackNavigationClient(HttpClient http) : IPackNavigationClient
{
    public async Task<PackNavigationDeclaration?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.GetFromJsonAsync<PackNavigationResponse>(
            "api/local-node/navigation/workspaces", cancellationToken)
            ?? throw new InvalidOperationException("The navigation service returned no response.");
        if (response.Configured && response.Pack is null)
            throw new InvalidOperationException("The navigation service returned no declaration.");
        return response.Configured ? response.Pack : null;
    }
}

// Explicit serverless mode has no installed packs. Never manufacture an Access declaration.
public sealed class FixturePackNavigationClient : IPackNavigationClient
{
    public Task<PackNavigationDeclaration?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<PackNavigationDeclaration?>(null);
}
