using Microsoft.JSInterop;

namespace Harborline.App.Blazor.ReferenceHost.Transport;

/// <summary>A per-request browser dispatch permit; it contains no credentials or session identity.</summary>
public sealed class SelectedSessionDispatchGuard(CancellationToken cancellationToken) : IDisposable
{
    private int completed;
    [JSInvokable]
    public bool CanDispatch() => Volatile.Read(ref completed) == 0 && !cancellationToken.IsCancellationRequested;
    public void Dispose() => Interlocked.Exchange(ref completed, 1);
}
