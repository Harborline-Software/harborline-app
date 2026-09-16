using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Harborline.App.Blazor.Tests;

public sealed class PackActionHostTests : BunitContext
{
    [Fact]
    public async Task Disposal_revokes_the_browser_runtime_before_releasing_its_reference()
    {
        var browser = new BrowserRuntime();
        Services.AddSingleton<IJSRuntime>(browser);
        var component = Render<PackActionHost>(parameters => parameters.Add(view => view.ViewId, "example"));
        component.WaitForAssertion(() => Assert.Contains("load", browser.Calls));
        await component.Instance.DisposeAsync();
        await component.Instance.DisposeAsync();
        Assert.Equal(new[] { "import", "createPackActionRuntime", "load", "dispose", "release", "release" }, browser.Calls);
    }

    [Theory]
    [InlineData("import")]
    [InlineData("createPackActionRuntime")]
    public async Task Disposal_during_browser_initialization_never_loads_a_late_runtime(string delayedCall)
    {
        var browser = new BrowserRuntime { DelayedCall = delayedCall };
        Services.AddSingleton<IJSRuntime>(browser);
        var component = Render<PackActionHost>(parameters => parameters.Add(view => view.ViewId, "example"));
        component.WaitForAssertion(() => Assert.Contains(delayedCall, browser.Calls));
        await component.Instance.DisposeAsync();
        browser.Release.TrySetResult();
        component.WaitForAssertion(() => Assert.Equal(delayedCall == "import" ? 1 : 2, browser.Calls.Count(call => call == "release")));
        Assert.DoesNotContain("load", browser.Calls);
        Assert.Equal(delayedCall == "import" ? 0 : 1, browser.Calls.Count(call => call == "dispose"));
    }

    [Fact]
    public void Generic_browser_bridge_renders_the_same_refusal_receipt_without_a_server_http_client()
    {
        var browser = new BrowserRuntime();
        Services.AddSingleton<IJSRuntime>(browser);
        var component = Render<PackActionHost>(parameters => parameters.Add(view => view.ViewId, "example"));
        component.WaitForAssertion(() => Assert.Equal("Apply change", component.Find("button").TextContent));
        component.Find("button").Click();
        component.WaitForAssertion(() => Assert.Contains("Audit: native-audit", component.Markup, StringComparison.Ordinal));
        Assert.Contains("Correlation: native-correlation", component.Markup, StringComparison.Ordinal);
        Assert.Contains("authorization.permission_required", component.Markup, StringComparison.Ordinal);
        Assert.Equal(new[] { "import", "createPackActionRuntime", "load", "invoke" }, browser.Calls);
    }

    [Fact]
    public void Request_controls_forward_only_admitted_values_and_lock_until_explicit_reset()
    {
        var browser = new BrowserRuntime { Details = true };
        Services.AddSingleton<IJSRuntime>(browser);
        var component = Render<PackActionHost>(parameters => parameters.Add(view => view.ViewId, "example"));
        component.WaitForAssertion(() => Assert.Single(component.FindAll("input")));
        Assert.Equal("request.correlationId", component.Find("input").GetAttribute("name"));
        component.Find("input").Change("43300000-0000-4000-8000-000000000101");
        Assert.Equal("43300000-0000-4000-8000-000000000101", browser.Correlation);
        component.FindAll("button").Single(button => button.TextContent == "Apply change").Click();
        component.WaitForAssertion(() => Assert.True(component.Find("input").ParentElement!.ParentElement!.HasAttribute("disabled")));
        Assert.Contains("Correlation: 43300000-0000-4000-8000-000000000101", component.Markup, StringComparison.Ordinal);
        component.FindAll("button").Single(button => button.TextContent == "New request").Click();
        component.WaitForAssertion(() => Assert.False(component.Find("input").ParentElement!.ParentElement!.HasAttribute("disabled")));
        Assert.Contains("setRequestDetails", browser.Calls);
        Assert.Contains("newRequest", browser.Calls);
    }

    private sealed class BrowserRuntime : IJSRuntime, IJSObjectReference
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        public List<string> Calls { get; } = [];
        public bool Details { get; init; }
        public string? DelayedCall { get; init; }
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Correlation { get; private set; } = "43300000-0000-4000-8000-000000000010";
        private bool locked;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            try { return InvokeAsync<TValue>(identifier, CancellationToken.None, args); }
            catch (JSException) { throw; }
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);
            if (identifier == DelayedCall)
            {
                try { return new ValueTask<TValue>(ReleaseReferenceAsync<TValue>()); }
                catch (JSException) { throw; }
            }
            if (identifier == "dispose") return ValueTask.FromResult(default(TValue)!);
            if (identifier is "import" or "createPackActionRuntime") return ValueTask.FromResult((TValue)(object)this);
            if (identifier == "setRequestDetails") Correlation = ((string[][])args![0]!)[0][1];
            if (identifier == "newRequest") locked = false;
            if (identifier == "invoke") locked = true;
            var state = JsonSerializer.Deserialize<PackActionSnapshot>("""
                {"plan":null,"rows":[],"selectedId":null,"activeAction":{"id":"opaque","label":"Apply change"},
                 "inputPlan":null,"receipt":null,"error":null,"busy":false}
                """, JsonOptions)!;
            if (Details) state = state with { RequestDetails = new Dictionary<string, string> { ["correlationId"] = Correlation }, RequestLocked = locked };
            if (identifier == "invoke") state = state with { Receipt = new PackActionReceipt(403,
                "authorization.permission_required", JsonSerializer.SerializeToElement(new { code = "authorization.permission_required" }),
                "authorization.permission_required", "native-audit", Details ? Correlation : "native-correlation") };
            return ValueTask.FromResult((TValue)(object)state);
        }
        public ValueTask DisposeAsync() { Calls.Add("release"); return ValueTask.CompletedTask; }
        private async Task<TValue> ReleaseReferenceAsync<TValue>()
        {
            await Release.Task;
            return (TValue)(object)this;
        }
    }
}
