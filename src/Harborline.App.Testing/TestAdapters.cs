using Harborline.App.Abstractions;
using Harborline.App.Blazor.Hybrid;

namespace Harborline.App.Testing;

public sealed class StaticHarborlineUserSession(HarborlineUserSessionSnapshot snapshot) : IHarborlineUserSession
{
    public event EventHandler? Changed { add { } remove { } }
    public ValueTask<HarborlineUserSessionSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(snapshot);
}

public sealed class StaticHarborlineExtension(HarborlineExtensionContribution contribution) : IHarborlineAppExtension
{
    public ValueTask<HarborlineExtensionContribution> ContributeAsync(
        HarborlineUserSessionSnapshot session,
        HarborlineHostForm host,
        CancellationToken cancellationToken = default) => ValueTask.FromResult(contribution);
}

public sealed class MemoryHarborlineSecureStore : IHarborlineSecureStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_values.GetValueOrDefault(key));
    public ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _values[key] = value;
        return ValueTask.CompletedTask;
    }
    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _values.Remove(key);
        return ValueTask.CompletedTask;
    }
}
