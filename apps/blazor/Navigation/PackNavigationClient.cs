using System.Net.Http.Json;
using Harborline.UIAdapters.Blazor.Components.Layout;

namespace Harborline.App.Blazor.ReferenceHost.Navigation;

public sealed record PackNavigationResponse(bool Configured, PackNavigationDeclaration? Pack);

public interface IPackNavigationClient
{
    Task<PackNavigationDeclaration?> ReadAsync(CancellationToken cancellationToken = default);
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
