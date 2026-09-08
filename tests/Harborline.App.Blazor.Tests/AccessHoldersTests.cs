using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.App.Blazor.ReferenceHost.Navigation;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;
using Harborline.UIAdapters.Blazor.Components.Layout;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Harborline.App.Blazor.Tests;
public sealed class AccessHoldersTests(ITestOutputHelper output) : BunitContext
{
    private static string Fixture(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Harborline.App.slnx"))) root = root.Parent;
        return File.ReadAllText(Path.Combine(root!.FullName, "tests/fixtures", name));
    }
    private IRenderedComponent<Shell> Mount(HttpClient http)
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new HttpAuthorizationAdminClient(http));
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(http));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var result = Render<Shell>();
        result.WaitForAssertion(() => Assert.Contains("Access", result.Markup, StringComparison.Ordinal));
        result.Find("a[href='/workspaces/access']").Click();
        result.FindAll("a").Single(link => link.TextContent.Trim() == "Access").Click();
        result.WaitForAssertion(() => Assert.Contains("access.holders", result.Markup, StringComparison.Ordinal));
        result.FindAll("button, a").Single(button => button.TextContent.Trim() == "access.holders").Click();
        return result;
    }
    [Fact]
    public void Captured_rows_render_every_field_in_wire_order_including_unattributed_failure_and_dates()
    {
        var handler = new Handler(Fixture("access-holders.json"));
        var result = Mount(new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:7322/") });
        var rows = JsonSerializer.Deserialize<AccessHoldersResponse>(handler.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Holders;
        result.WaitForAssertion(() => Assert.Equal(rows.Count, result.FindAll("article").Count));
        foreach (var (row, index) in rows.Select((row, index) => (row, index)))
        {
            var expected = new List<string> { row.PartyId, row.Source };
            if (row.AttributionFailure is not null) expected.Add(row.AttributionFailure);
            expected.AddRange([$"{row.Role!.Vocabulary} / {row.Role.Name}", row.GrantId, row.Granter, row.Scope, row.EffectiveFrom, row.EffectiveTo ?? "No end date"]);
            Assert.Equal(expected, result.FindAll("article")[index].QuerySelectorAll("dd").Select(cell => cell.TextContent));
        }
        Assert.Contains(rows, row => row.PartyId == "UNATTRIBUTED" && row.AttributionFailure is not null);
        Assert.Contains(rows, row => row.PartyId != "UNATTRIBUTED");
        Assert.Contains("/api/local-node/authorization/holders", handler.Paths);
    }
    [Fact]
    public void Refusal_is_visible_never_an_empty_list_and_retry_reads_the_same_route()
    {
        var handler = new Handler(Fixture("access-holders-refused.json")) { Status = HttpStatusCode.Forbidden };
        var result = Mount(new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:7322/") });
        result.WaitForAssertion(() => Assert.Contains("authorization.permission_required", result.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.Empty(result.FindAll("article"));
        Assert.DoesNotContain("No active holders.", result.Markup, StringComparison.Ordinal);
        handler.Status = HttpStatusCode.OK; handler.Body = Fixture("access-holders.json");
        result.Find("[role=alert] button").Click();
        result.WaitForAssertion(() => Assert.Equal(JsonSerializer.Deserialize<AccessHoldersResponse>(handler.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Holders.Count, result.FindAll("article").Count));
        Assert.Empty(result.FindAll("[role=alert]"));
    }
    [LiveHostFact]
    public void Live_shipping_declaration_renders_Access_and_holders_item_then_reads_live_holders()
    {
        using var http = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("HARBORLINE_LIVE_API_ORIGIN")!) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("HARBORLINE_LIVE_API_TOKEN"));
        var result = Mount(http);
        result.WaitForAssertion(() => Assert.NotEmpty(result.FindAll("article")));
        Assert.Equal("Holders", result.Find("h1").TextContent);
        output.WriteLine($"LIVE Blazor: {http.BaseAddress}; Access workspace + access.holders item; {result.FindAll("article").Count} holder rows");
    }
    private sealed class Handler(string body) : HttpMessageHandler
    {
        public string Body = body;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath; Paths.Add(path);
            var holders = path.EndsWith("/authorization/holders", StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(holders ? Status : HttpStatusCode.OK) { Content = new StringContent(
                path.EndsWith("/navigation/workspaces", StringComparison.Ordinal) ? Fixture("access-navigation.json") : holders ? Body : "[]", Encoding.UTF8, "application/json") });
        }
    }
    private sealed class Media : IMediaQueryObserver
    {
        public ValueTask<IMediaQuerySubscription> ObserveAsync(string query, Func<MediaQueryChange, ValueTask> changed, CancellationToken cancellationToken = default) => new(new Subscription(query));
        private sealed class Subscription(string query) : IMediaQuerySubscription
        {
            public string Query => query;
            public bool Matches => !query.Contains("max-width", StringComparison.Ordinal);
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
public sealed class LiveHostFactAttribute : FactAttribute
{
    public LiveHostFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HARBORLINE_LIVE_API_ORIGIN"))) Skip = "Set HARBORLINE_LIVE_API_ORIGIN and HARBORLINE_LIVE_API_TOKEN for the explicit live-host proof.";
    }
}
