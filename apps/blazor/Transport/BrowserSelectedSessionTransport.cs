using Microsoft.JSInterop;

namespace Harborline.App.Blazor.ReferenceHost.Transport;

public sealed record SelectedSessionResponse(int Status, string Body, string? AuditId, string? CorrelationId = null);
public sealed record SelectedSessionBytesResponse(int Status, string Body, string? AuditId, string? CorrelationId, byte[]? Bytes);

/// <summary>Calls the same browser-owned session transport as React, without a server cookie jar.</summary>
public sealed class BrowserSelectedSessionTransport(IJSRuntime javascript) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;
    private readonly CancellationTokenSource lifetime = new();
    private int disposed;

    public Task<SelectedSessionResponse> SendAsync(
        string path, string method = "GET", object? body = null, string contentType = "application/json",
        CancellationToken cancellationToken = default) => SendCoreAsync<SelectedSessionResponse>("send", path, method, body, contentType, cancellationToken);

    public Task<SelectedSessionBytesResponse> SendBytesAsync(
        string path, string method = "GET", object? body = null, string contentType = "application/json",
        CancellationToken cancellationToken = default) => SendCoreAsync<SelectedSessionBytesResponse>("sendBytes", path, method, body, contentType, cancellationToken);

    private async Task<T> SendCoreAsync<T>(string operation, string path, string method, object? body, string contentType, CancellationToken cancellationToken)
    {
        Task<IJSObjectReference>? import = null;
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        var dispatchToken = linked.Token;
        try
        {
            dispatchToken.ThrowIfCancellationRequested();
            if (_module is { IsFaulted: true } or { IsCanceled: true }) _module = null;
            import = _module ??= javascript.InvokeAsync<IJSObjectReference>("import", "./selected-session-transport.mjs").AsTask();
            var module = await import.WaitAsync(dispatchToken);
            dispatchToken.ThrowIfCancellationRequested();
            // Cancelling an interop await does not cancel JavaScript queued behind another request.
            // The browser checks this short-lived permit before CSRF and before dispatch; disposal
            // also revokes it when interop times out. A dispatched mutation is never retried.
            using var guard = new SelectedSessionDispatchGuard(dispatchToken);
            using var reference = DotNetObjectReference.Create(guard);
            return await module.InvokeAsync<T>(operation + "ForCaller", dispatchToken, reference, path, method, body, contentType);
        }
        catch (JSException exception)
        {
            throw new InvalidOperationException("The selected-session request could not complete. No mutation was retried.", exception);
        }
        catch (OperationCanceledException exception) when (!dispatchToken.IsCancellationRequested)
        {
            // JS interop's own timeout is a visible request failure, not caller cancellation.
            // Leave genuine caller/circuit cancellation unchanged and never retry here.
            throw new InvalidOperationException("The selected-session request could not complete. No mutation was retried.", exception);
        }
        finally
        {
            // Import timeout uses cancellation, not JSException. Never retain a failed import,
            // and never clear a newer concurrent import or retry a dispatched mutation.
            if (ReferenceEquals(_module, import) && import is ({ IsFaulted: true } or { IsCanceled: true })) _module = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await lifetime.CancelAsync();
        if (_module is { IsCompletedSuccessfully: true })
        {
            try { await _module.Result.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
        lifetime.Dispose();
    }
}
