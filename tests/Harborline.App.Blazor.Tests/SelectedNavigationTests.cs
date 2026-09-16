using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.App.Blazor.ReferenceHost.Navigation;
using Harborline.App.Blazor.ReferenceHost.Transport;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;
using Harborline.UIAdapters.Blazor.Components.Layout;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace Harborline.App.Blazor.Tests;

public sealed class SelectedNavigationTests : BunitContext
{
    [Fact]
    public async Task Concurrent_browser_navigation_uses_each_selected_tenant_without_server_http_or_shared_state()
    {
        var first = new Browser("tenant-a");
        var second = new Browser("tenant-b");
        await using var firstTransport = new BrowserSelectedSessionTransport(first);
        await using var secondTransport = new BrowserSelectedSessionTransport(second);
        var results = await Task.WhenAll(new BrowserPackNavigationClient(firstTransport).ReadAsync(),
            new BrowserPackNavigationClient(secondTransport).ReadAsync());
        Assert.Equal("tenant-a", Assert.Single(results[0]!.SeedWorkspaces).Id);
        Assert.Equal("tenant-b", Assert.Single(results[1]!.SeedWorkspaces).Id);
        Assert.Equal(new[] { "import", "sendForCaller" }, first.Calls);
        Assert.Equal(new[] { "import", "sendForCaller" }, second.Calls);
    }

    [Fact]
    public async Task Static_server_prerender_never_loads_navigation_under_bootstrap_authority()
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver, StaticMedia>();
        Services.AddSingleton<IAuthorizationAdminClient, FixtureAuthorizationAdminClient>();
        var navigation = new NeverReadNavigation();
        Services.AddSingleton<IPackNavigationClient>(navigation);
        await using var renderer = new HtmlRenderer(Services, NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<Shell>()).ToHtmlString());
        Assert.DoesNotContain("tenant-a", html, StringComparison.Ordinal);
        Assert.Equal(0, navigation.Requests);
    }

    private sealed class NeverReadNavigation : IPackNavigationClient
    {
        public int Requests { get; private set; }
        public Task<PackNavigationDeclaration?> ReadAsync(CancellationToken cancellationToken = default)
        {
            Requests++;
            throw new InvalidOperationException("Navigation must wait for the browser's first interactive render.");
        }
    }

    private sealed class StaticMedia : IMediaQueryObserver
    {
        public ValueTask<IMediaQuerySubscription> ObserveAsync(string query, Func<MediaQueryChange, ValueTask> changed,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Static prerender has no browser media observer.");
    }

    private sealed class Browser(string tenant) : IJSRuntime, IJSObjectReference
    {
        public List<string> Calls { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            try { return InvokeAsync<TValue>(identifier, CancellationToken.None, args); }
            catch (JSException) { throw; }
        }
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);
            if (identifier == "import") return (TValue)(object)this;
            Assert.Equal("sendForCaller", identifier);
            Assert.True(Assert.IsType<DotNetObjectReference<SelectedSessionDispatchGuard>>(args![0]).Value.CanDispatch());
            Assert.Equal("/api/local-node/navigation/workspaces", args[1]);
            Assert.Equal("GET", args[2]);
            Assert.Null(args[3]);
            await Task.Delay(tenant == "tenant-a" ? 10 : 1, cancellationToken);
            return (TValue)(object)new SelectedSessionResponse(200, JsonSerializer.Serialize(new
            {
                configured = true, pack = new { packId = tenant,
                    seedWorkspaces = new[] { new { id = tenant, labelKey = tenant, groups = Array.Empty<object>() } }, modeSwitch = new { } },
            }), null);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
