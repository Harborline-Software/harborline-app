using Harborline.App.Blazor.ReferenceHost.Transport;
using Microsoft.JSInterop;

namespace Harborline.App.Blazor.Tests;

public sealed class SelectedSessionCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelling_a_queued_install_revokes_its_browser_dispatch_permit(bool disposeTransport)
    {
        var browser = new QueuedBrowser();
        await using var transport = new BrowserSelectedSessionTransport(browser);
        using var cancellation = new CancellationTokenSource();
        var install = transport.SendAsync("/api/local-node/packs/install", "POST", new byte[] { 0, 255 }, "application/octet-stream", cancellation.Token);
        await browser.Queued.Task;
        if (disposeTransport) await transport.DisposeAsync();
        else await cancellation.CancelAsync();
        // A real JS invocation can continue after its .NET await ends. Release that independent
        // queued work and verify its final dispatch decision, not merely cancellation of await.
        if (!disposeTransport) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => install);
        browser.Release.SetResult();
        try { await browser.Work!; } catch (Exception exception) when (exception is JSException or ObjectDisposedException) { }
        Assert.Equal(0, browser.Mutations);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => install);
        Assert.NotNull(browser.Reference);
        Assert.Throws<ObjectDisposedException>(() => browser.Reference!.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispatch_reference_is_disposed_after_success_or_failure_without_retry(bool fail)
    {
        var browser = new QueuedBrowser { FailSend = fail };
        await using var transport = new BrowserSelectedSessionTransport(browser);
        var request = transport.SendAsync("/api/local-node/packs/install", "POST", new byte[] { 255 }, "application/octet-stream");
        await browser.Queued.Task;
        browser.Release.SetResult();
        if (fail) await Assert.ThrowsAsync<InvalidOperationException>(() => request);
        else Assert.Equal(200, (await request).Status);
        Assert.Equal(1, browser.Mutations);
        Assert.NotNull(browser.Reference);
        Assert.Throws<ObjectDisposedException>(() => browser.Reference!.Value);
    }

    [Fact]
    public async Task Interop_timeout_revokes_the_permit_even_without_caller_cancellation()
    {
        var browser = new QueuedBrowser { TimeoutSend = true };
        await using var transport = new BrowserSelectedSessionTransport(browser);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync("/api/local-node/packs/install", "POST", "{}"));
        Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerException);
        browser.Release.SetResult();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => browser.Work!);
        Assert.Equal(0, browser.Mutations);
        Assert.Throws<ObjectDisposedException>(() => browser.Reference!.Value);
    }

    [Fact]
    public async Task Binary_request_uses_the_same_revocable_dispatch_permit()
    {
        var browser = new QueuedBrowser();
        await using var transport = new BrowserSelectedSessionTransport(browser);
        using var cancellation = new CancellationTokenSource();
        var request = transport.SendBytesAsync("/api/local-node/packs/install", "POST", new byte[] { 255 }, "application/octet-stream", cancellation.Token);
        await browser.Queued.Task;
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.Equal("sendBytesForCaller", browser.Operation);
        browser.Release.SetResult();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => browser.Work!);
        Assert.Equal(0, browser.Mutations);
    }

    private sealed class QueuedBrowser : IJSRuntime, IJSObjectReference
    {
        public TaskCompletionSource Queued { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task? Work { get; private set; }
        public DotNetObjectReference<SelectedSessionDispatchGuard>? Reference { get; private set; }
        public bool FailSend { get; init; }
        public bool TimeoutSend { get; init; }
        public string? Operation { get; private set; }
        public int Mutations { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            try { return InvokeAsync<TValue>(identifier, CancellationToken.None, args); }
            catch (JSException) { throw; }
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            try
            {
                if (identifier == "import") return ValueTask.FromResult((TValue)(object)this);
                Operation = identifier;
                Reference = args![0] as DotNetObjectReference<SelectedSessionDispatchGuard>;
                var work = DispatchAsync<TValue>();
                Work = work;
                if (TimeoutSend) return ValueTask.FromCanceled<TValue>(new CancellationToken(true));
                return new ValueTask<TValue>(work.WaitAsync(cancellationToken));
            }
            catch (JSException) { throw; }
        }
        private async Task<TValue> DispatchAsync<TValue>()
        {
            Queued.SetResult();
            await Release.Task;
            if (Reference is not null && !Reference.Value.CanDispatch()) throw new JSException("Caller cancelled before dispatch.");
            Mutations++;
            if (FailSend) throw new JSException("Connection ended after dispatch.");
            return (TValue)(object)new SelectedSessionResponse(200, "{}", null);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
