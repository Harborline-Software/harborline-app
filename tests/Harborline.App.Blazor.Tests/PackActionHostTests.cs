using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Harborline.App.Blazor.Tests;

public sealed class PackActionHostTests : BunitContext
{
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

    private sealed class BrowserRuntime : IJSRuntime, IJSObjectReference
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        public List<string> Calls { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            try { return InvokeAsync<TValue>(identifier, CancellationToken.None, args); }
            catch (JSException) { throw; }
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);
            if (identifier is "import" or "createPackActionRuntime") return ValueTask.FromResult((TValue)(object)this);
            var state = JsonSerializer.Deserialize<PackActionSnapshot>("""
                {"plan":null,"rows":[],"selectedId":null,"activeAction":{"id":"opaque","label":"Apply change"},
                 "inputPlan":null,"receipt":null,"error":null,"busy":false}
                """, JsonOptions)!;
            if (identifier == "invoke") state = state with { Receipt = new PackActionReceipt(403,
                "authorization.permission_required", JsonSerializer.SerializeToElement(new { code = "authorization.permission_required" }),
                "authorization.permission_required", "native-audit", "native-correlation") };
            return ValueTask.FromResult((TValue)(object)state);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
