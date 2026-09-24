using System.Net;
using System.Text;

using Bunit;

using Harborline.App.Blazor.ReferenceHost;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.App.Blazor.ReferenceHost.Admin.Configuration;
using Harborline.App.Blazor.ReferenceHost.Navigation;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;

using Microsoft.Extensions.DependencyInjection;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// T-657. Reachability, per lane — the Blazor twin of
/// apps/react/src/__tests__/configurationNavigation.test.tsx.
///
/// The api projects the <c>configuration</c> workspace only to a caller its configuration activation
/// routes would admit (the selected-session-product audience holding <c>packages:operate</c>
/// install-wide), so the two fixtures here are that one route's two answers: the projection a holder
/// is served, and the projection a non-holder is served. The shell mounts the existing T-460 surface
/// on the entry's id; nothing about how that page renders changed.
/// </summary>
public sealed class ConfigurationNavigationTests : BunitContext
{
    private const string Effective =
        """{"digest":"sha256:baseline-generation","algorithm":"sha256","references":[]}""";

    private static string Fixture(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Harborline.App.slnx"))) root = root.Parent;
        return File.ReadAllText(Path.Combine(root!.FullName, "tests/fixtures", name));
    }

    private (IRenderedComponent<Shell> Result, Handler Handler) Mount(string navigationFixture)
    {
        var handler = new Handler(Fixture(navigationFixture));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:7322/") };
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new HttpAuthorizationAdminClient(http));
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(http));
        Services.AddSingleton<IConfigurationActivationClient>(new HttpConfigurationActivationClient(http));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var result = Render<Shell>();
        result.WaitForAssertion(() => Assert.Contains("Access", result.Markup, StringComparison.Ordinal));
        return (result, handler);
    }

    [Fact]
    public void A_caller_holding_packages_operate_reaches_the_configuration_activation_surface()
    {
        var (result, handler) = Mount("configuration-navigation.json");

        result.Find("a[href='/workspaces/configuration']").Click();
        var item = result.Find("a[href='/workspaces/configuration.activation']");
        Assert.Equal("Activation", item.TextContent.Trim());
        item.Click();

        result.WaitForAssertion(() => Assert.Equal(
            "sha256:baseline-generation", result.Find("#effective-generation").TextContent.Trim()));
        Assert.Equal("Configuration activation", result.Find("h1").TextContent.Trim());
        Assert.Contains("/api/local-node/configuration/effective", handler.Paths);
    }

    [Fact]
    public void A_caller_without_packages_operate_is_not_shown_the_configuration_entry()
    {
        var (result, handler) = Mount("configuration-navigation-without-operate.json");

        Assert.Empty(result.FindAll("a[href='/workspaces/configuration']"));
        Assert.Empty(result.FindAll("a[href='/workspaces/configuration.activation']"));
        Assert.DoesNotContain("configuration.activation", result.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Effective generation:", result.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/local-node/configuration/effective", handler.Paths);
    }

    private sealed class Handler(string navigation) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            var body = path.EndsWith("/navigation/workspaces", StringComparison.Ordinal) ? navigation
                : path.EndsWith("/configuration/effective", StringComparison.Ordinal) ? Effective
                : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class Media : IMediaQueryObserver
    {
        public ValueTask<IMediaQuerySubscription> ObserveAsync(string query, Func<MediaQueryChange, ValueTask> changed, CancellationToken cancellationToken = default)
            => new(new Subscription(query));

        private sealed class Subscription(string query) : IMediaQuerySubscription
        {
            public string Query => query;
            public bool Matches => !query.Contains("max-width", StringComparison.Ordinal);
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
