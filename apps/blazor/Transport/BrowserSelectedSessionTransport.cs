using Microsoft.JSInterop;

namespace Harborline.App.Blazor.ReferenceHost.Transport;

public sealed record SelectedSessionResponse(int Status, string Body, string? AuditId, string? CorrelationId = null);

/// <summary>Calls the same browser-owned session transport as React, without a server cookie jar.</summary>
public sealed class BrowserSelectedSessionTransport(IJSRuntime javascript) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;

    public async Task<SelectedSessionResponse> SendAsync(
        string path, string method = "GET", object? body = null, string contentType = "application/json",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _module ??= javascript.InvokeAsync<IJSObjectReference>("import", "./selected-session-transport.mjs").AsTask();
            var module = await _module;
            return await module.InvokeAsync<SelectedSessionResponse>("send", cancellationToken, path, method, body, contentType);
        }
        catch (JSException exception)
        {
            throw new InvalidOperationException("The selected-session request could not complete. No mutation was retried.", exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is { IsCompletedSuccessfully: true })
        {
            try { await _module.Result.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}
