using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Harborline.App.Blazor.Tests;

public sealed class PackActionHostTests : BunitContext
{
    private const string SafeFailure = "The selected-session request could not complete. No mutation was retried.";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Input_bound_grant_action_submits_visible_field_without_rows_and_preserves_refusal(bool rowsRefused)
    {
        var browser = new BrowserRuntime { GrantInput = true, RowsRefused = rowsRefused };
        Services.AddSingleton<IJSRuntime>(browser);
        var component = Render<PackActionHost>(parameters => parameters.Add(view => view.ViewId, "access.holders"));
        component.WaitForAssertion(() => Assert.Single(component.FindAll("input[name=targetGrant]")));
        Assert.Empty(component.FindAll("tbody tr"));
        if (rowsRefused) Assert.Contains("authorization.permission_required", component.Find("[role=alert]").TextContent, StringComparison.Ordinal);
        var field = component.Find("input[name=targetGrant]");
        Assert.True(field.HasAttribute("required"));
        field.Input("known-grant");
        component.Find("form").Submit();
        Assert.True(browser.Calls.Contains("invoke"), component.Markup);
        component.WaitForAssertion(() => Assert.True(component.Markup.Contains("Audit: native-audit", StringComparison.Ordinal), component.Markup));
        Assert.Contains("Correlation: native-correlation", component.Markup, StringComparison.Ordinal);
        Assert.Contains("authorization.permission_required", component.Markup, StringComparison.Ordinal);
        Assert.Equal("known-grant", browser.Submitted!["targetGrant"]);
        Assert.Single(browser.Calls, call => call == "invoke");
        Assert.DoesNotContain("select", browser.Calls);
    }

    [Fact]
    public void Configured_interop_deadline_is_not_disabled_by_owned_cancellation_tokens()
    {
        Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options => options.JSInteropDefaultCallTimeout = TimeSpan.Zero);
        var browser = new BrowserRuntime { CancellationBlockedCall = "import" };
        Services.AddSingleton<IJSRuntime>(browser);
        var component = Render<PackActionHost>(parameters => parameters.Add(view => view.ViewId, "example"));
        component.WaitForAssertion(() => Assert.Contains(SafeFailure, component.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.DoesNotContain("invoke", browser.Calls);
    }

    [Theory]
    [InlineData("import", false)]
    [InlineData("import", true)]
    [InlineData("invoke", false)]
    [InlineData("invoke", true)]
    public async Task Owned_cancellation_stays_cancelled_without_a_visible_timeout_or_replay(string blockedCall, bool dispose)
    {
        using var caller = new CancellationTokenSource();
        var browser = new BrowserRuntime { CancellationBlockedCall = blockedCall };
        Services.AddSingleton<IJSRuntime>(browser);
        var component = Render<PackActionHost>(parameters => parameters
            .Add(view => view.ViewId, "example").Add(view => view.CancellationToken, caller.Token));
        Task? click = null;
        if (blockedCall == "invoke")
        {
            component.WaitForAssertion(() => Assert.Equal("Apply change", component.Find("button").TextContent));
            click = component.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        }
        await browser.CancellationEntered.Task;
        if (dispose) await component.Instance.DisposeAsync();
        else await caller.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => browser.CancellationWork!);
        if (click is not null) await click;
        await component.InvokeAsync(() => Task.CompletedTask);
        Assert.True(browser.ObservedToken.IsCancellationRequested);
        Assert.Empty(component.FindAll("[role=alert]"));
        Assert.Equal(1, browser.Calls.Count(call => call == blockedCall));
    }

    [Theory]
    [InlineData("import")]
    [InlineData("createPackActionRuntime")]
    [InlineData("load")]
    public void Unowned_initialization_timeout_is_visible_and_explicitly_retryable(string failingCall)
    {
        var browser = new BrowserRuntime { FailingCall = failingCall, TimeoutFailure = true };
        Services.AddSingleton<IJSRuntime>(browser);
        var component = Render<PackActionHost>(parameters => parameters.Add(view => view.ViewId, "example"));
        component.WaitForAssertion(() => Assert.Contains(SafeFailure, component.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Equal(1, browser.Calls.Count(call => call == failingCall));
        component.Find("button").Click();
        component.WaitForAssertion(() => Assert.Equal("Apply change", component.Find("button").TextContent));
        Assert.Equal(2, browser.Calls.Count(call => call == failingCall));
        Assert.DoesNotContain("invoke", browser.Calls);
    }

    [Fact]
    public void Unowned_action_timeout_is_visible_and_reload_never_replays_the_mutation()
    {
        var browser = new BrowserRuntime { FailingCall = "invoke", TimeoutFailure = true };
        Services.AddSingleton<IJSRuntime>(browser);
        var component = Render<PackActionHost>(parameters => parameters.Add(view => view.ViewId, "example"));
        component.WaitForAssertion(() => Assert.Equal("Apply change", component.Find("button").TextContent));
        component.Find("button").Click();
        component.WaitForAssertion(() => Assert.Contains(SafeFailure, component.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        component.Find("[role=alert] button").Click();
        component.WaitForAssertion(() => Assert.Empty(component.FindAll("[role=alert]")));
        Assert.Single(browser.Calls, call => call == "invoke");
        Assert.Equal(2, browser.Calls.Count(call => call == "load"));
    }

    [Theory]
    [InlineData("import")]
    [InlineData("createPackActionRuntime")]
    public void Failed_initialization_recovers_only_after_explicit_reload_with_fresh_import(string failingCall)
    {
        var browser = new BrowserRuntime { FailingCall = failingCall };
        Services.AddSingleton<IJSRuntime>(browser);
        var component = Render<PackActionHost>(parameters => parameters.Add(view => view.ViewId, "example"));
        component.WaitForAssertion(() => Assert.Contains("Reload view", component.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Contains(SafeFailure, component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Private browser", component.Markup, StringComparison.Ordinal);
        Assert.Equal(1, browser.Calls.Count(call => call == failingCall));
        Assert.DoesNotContain("invoke", browser.Calls);
        component.Find("button").Click();
        component.WaitForAssertion(() => Assert.Equal("Apply change", component.Find("button").TextContent));
        Assert.Equal(2, browser.Calls.Count(call => call == "import"));
        Assert.Single(browser.Calls, call => call == "load");
        Assert.DoesNotContain("invoke", browser.Calls);
    }

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
        public bool GrantInput { get; init; }
        public bool RowsRefused { get; init; }
        public IReadOnlyDictionary<string, object?>? Submitted { get; private set; }
        public string? FailingCall { get; init; }
        public bool TimeoutFailure { get; init; }
        public string? CancellationBlockedCall { get; init; }
        public TaskCompletionSource CancellationEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task? CancellationWork { get; private set; }
        public CancellationToken ObservedToken { get; private set; }
        private bool failed;
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
            if (identifier == CancellationBlockedCall)
            {
                ObservedToken = cancellationToken;
                CancellationEntered.TrySetResult();
                try
                {
                    var work = AwaitCancellationAsync<TValue>(cancellationToken);
                    CancellationWork = work;
                    return new ValueTask<TValue>(work);
                }
                catch (JSException) { throw; }
            }
            if (identifier == FailingCall && !failed)
            {
                failed = true;
                if (TimeoutFailure) return ValueTask.FromCanceled<TValue>(new CancellationToken(true));
                throw new JSException("Private browser initialization detail");
            }
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
            if (identifier == "invoke" && GrantInput) Submitted = (IReadOnlyDictionary<string, object?>)args![0]!;
            var state = JsonSerializer.Deserialize<PackActionSnapshot>("""
                {"plan":null,"rows":[],"selectedId":null,"activeAction":{"id":"opaque","label":"Apply change"},
                 "inputPlan":null,"receipt":null,"error":null,"busy":false}
                """, JsonOptions)!;
            if (Details) state = state with { RequestDetails = new Dictionary<string, string> { ["correlationId"] = Correlation }, RequestLocked = locked };
            if (GrantInput) state = state with
            {
                ActiveAction = new PackActionDeclaration("revoke", "Revoke grant", null),
                Error = RowsRefused && identifier == "load" ? "authorization.permission_required" : null,
                InputPlan = JsonSerializer.SerializeToElement(new
                {
                    definitionId = "revoke", definitionVersion = "1", definitionKind = "FormDefinition",
                    bindings = new
                    {
                        fields = new { targetGrant = new { type = "text", required = true } },
                        overlay = new { fields = new { targetGrant = new { label = "Grant ID" } },
                            sections = new[] { new { id = "details", title = "Details", fields = new[] { "targetGrant" } } } },
                    },
                }),
            };
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
        private static async Task<TValue> AwaitCancellationAsync<TValue>(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return default!;
        }
    }
}
