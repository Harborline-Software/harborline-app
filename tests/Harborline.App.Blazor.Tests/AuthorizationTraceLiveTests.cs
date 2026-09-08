using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.App.Blazor.ReferenceHost.Authorization;
using Harborline.App.Blazor.ReferenceHost.Navigation;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Harborline.App.Blazor.Tests;
public sealed class AuthorizationTraceLiveTests(ITestOutputHelper output) : BunitContext
{
    private static readonly string[] Headings = ["Act kind", "Roles in force for the subject with scope and dates", "Standings", "Verdict"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record Fixture(Guid AuditId, AuthorizationTraceRead Read);
    private static Fixture ReadFixture()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Harborline.App.slnx"))) root = root.Parent;
        return JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(root!.FullName, "tests/fixtures/authorization-trace.json")), JsonOptions)!;
    }
    private void Register(HttpClient http)
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IAuthorizationAdminClient>(new HttpAuthorizationAdminClient(http));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }
    private static void AssertTrace(IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> result, AuthorizationTraceRead expected)
    {
        result.WaitForAssertion(() => Assert.Equal(Headings, result.FindAll("details h4").Select(node => node.TextContent)));
        var parts = result.FindAll("details li");
        for (var index = 0; index < 4; index++)
            foreach (var fact in expected.Steps[index].Facts) Assert.Contains(fact, parts[index].TextContent, StringComparison.Ordinal);
    }
    [Theory]
    [InlineData("settings", false)] [InlineData("settings", true)]
    [InlineData("holders", false)] [InlineData("holders", true)]
    [InlineData("binding", false)] [InlineData("binding", true)]
    public async Task Refusal_offers_a_live_reader_only_when_auditId_is_present(string surface, bool linked)
    {
        var fixture = ReadFixture(); var handler = new Replay(fixture, linked);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:7322") };
        Register(http);
        IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> result;
        if (surface == "settings") result = Render<AuthorizationAdminPage>();
        else if (surface == "holders") result = Render<AccessHoldersPage>();
        else
        {
            var definition = (await new FixtureAuthorizationAdminClient().ListCapabilityDefinitionsAsync())[0];
            result = Render<CapabilityBindingEditor>(p => p.Add(c => c.Definition, definition).Add(c => c.RoleDefinitions, FixtureAuthorizationAdminClient.RoleDefinitions));
            result.FindAll("button").Single(node => node.TextContent.Trim() == "Clear all roles…").Click();
            result.FindAll("[role=dialog] button").Single(node => node.TextContent.Trim() == "Clear all roles").Click();
        }
        result.WaitForAssertion(() => Assert.Contains("You do not have permission to administer authorization settings.", result.Markup, StringComparison.Ordinal));
        if (linked)
        {
            result.Find("details").TriggerEvent("ontoggle", EventArgs.Empty);
            AssertTrace(result, fixture.Read);
            Assert.Contains($"/api/local-node/authorization/traces/{fixture.AuditId:D}", handler.Paths);
        }
        else
        {
            Assert.Empty(result.FindAll("details"));
            Assert.DoesNotContain(handler.Paths, path => path.Contains("/traces/", StringComparison.Ordinal));
        }
    }
    [LiveHostFact]
    public void Live_shipping_host_Access_refusal_links_real_auditId_to_four_stored_parts()
    {
        using var handler = new Capture();
        using var http = new HttpClient(handler) { BaseAddress = new Uri(Environment.GetEnvironmentVariable("HARBORLINE_LIVE_API_ORIGIN")!) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("HARBORLINE_LIVE_API_TOKEN"));
        Register(http);
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(http));
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        var result = Render<Shell>();
        result.WaitForAssertion(() => Assert.Contains("Access", result.Markup, StringComparison.Ordinal));
        result.Find("a[href='/workspaces/access']").Click();
        result.FindAll("a").Single(node => node.TextContent.Trim() == "Access").Click();
        result.WaitForAssertion(() => Assert.Contains("access.holders", result.Markup, StringComparison.Ordinal));
        result.FindAll("button, a").Single(node => node.TextContent.Trim() == "access.holders").Click();
        result.WaitForAssertion(() => Assert.Single(result.FindAll("details")));
        result.Find("details").TriggerEvent("ontoggle", EventArgs.Empty);
        result.WaitForAssertion(() => Assert.NotNull(handler.Trace));
        Assert.NotNull(handler.AuditId); Assert.Equal(0, handler.Trace!.Availability); Assert.Equal(1, handler.Trace.Version);
        Assert.Equal(["1:act", "2:effective-roles", "3:standings", "4:verdict"], handler.Trace.Steps.Select(step => $"{step.Ordinal}:{step.Stage}"));
        AssertTrace(result, handler.Trace);
        Assert.Contains("verdict:denied", result.Find("details ol").TextContent, StringComparison.Ordinal);
        output.WriteLine($"LIVE Blazor: {http.BaseAddress}; Access → holders 403; auditId={handler.AuditId}; trace 200; act → effective-roles → standings → verdict");
    }
    private sealed class Replay(Fixture fixture, bool linked) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath; Paths.Add(path);
            if (path == $"/api/local-node/authorization/traces/{fixture.AuditId:D}")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(fixture.Read) });
            var body = new Dictionary<string, object> { ["code"] = "authorization.permission_required" };
            if (linked) body["auditId"] = fixture.AuditId;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = JsonContent.Create(body) });
        }
    }
    private sealed class Capture() : DelegatingHandler(new HttpClientHandler())
    {
        public Guid? AuditId { get; private set; }
        public AuthorizationTraceRead? Trace { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/authorization/holders", StringComparison.Ordinal))
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                AuditId = json.RootElement.GetProperty("auditId").GetGuid();
            }
            if (path.Contains("/authorization/traces/", StringComparison.Ordinal))
            {
                Assert.Equal($"/api/local-node/authorization/traces/{AuditId:D}", path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Trace = JsonSerializer.Deserialize<AuthorizationTraceRead>(await response.Content.ReadAsStringAsync(cancellationToken), JsonOptions);
            }
            return response;
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
