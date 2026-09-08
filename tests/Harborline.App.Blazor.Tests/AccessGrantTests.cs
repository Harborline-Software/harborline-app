using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.App.Blazor.ReferenceHost.Admin.Forms;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Harborline.App.Blazor.Tests;
public sealed class AccessGrantTests(ITestOutputHelper output) : BunitContext
{
    private static string Fixture(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Harborline.App.slnx"))) root = root.Parent;
        return File.ReadAllText(Path.Combine(root!.FullName, "tests/fixtures", name));
    }
    private IRenderedComponent<AccessHoldersPage> Mount(HttpClient http)
    {
        Services.AddSingleton<IAuthorizationAdminClient>(new HttpAuthorizationAdminClient(http));
        Services.AddSingleton<IFormsAdminClient>(new HttpFormsAdminClient(http));
        return Render<AccessHoldersPage>();
    }
    private static void Fill(IRenderedComponent<AccessHoldersPage> result)
    {
        result.FindAll("button").Single(b => b.TextContent == "Grant a role").Click();
        result.WaitForAssertion(() => Assert.Single(result.FindAll("form")));
        Assert.Equal(new[] { "person", "role", "scope", "residency", "effectiveFrom", "effectiveTo", "reason" }, result.FindAll("input").Select(i => i.GetAttribute("name")));
        using var values = JsonDocument.Parse(Fixture("access-grant-body.json"));
        foreach (var value in values.RootElement.EnumerateObject()) result.Find($"input[name={value.Name}]").Change(value.Value.GetString());
        result.Find("form").Submit();
    }
    [Fact]
    public void Pack_field_order_and_UTF8_body_match_other_lane_contract_and_acceptance_refreshes_holders()
    {
        var handler = new Handler(HttpStatusCode.Created);
        var result = Mount(new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:7324/") });
        result.WaitForAssertion(() => Assert.Contains("No active holders.", result.Markup, StringComparison.Ordinal));
        Fill(result);
        result.WaitForAssertion(() => Assert.Contains("Submission saved.", result.Markup, StringComparison.Ordinal));
        result.WaitForAssertion(() => Assert.Single(result.FindAll("article")));
        Assert.Contains("party-s301s2", result.Find("article").TextContent, StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetBytes(Fixture("access-grant-body.json")), Encoding.UTF8.GetBytes(Assert.Single(handler.Bodies)));
        Assert.Equal(2, handler.Reads);
        output.WriteLine($"PARITY Blazor body: {handler.Bodies[0]}");
    }
    [Fact]
    public void Refused_submission_keeps_values_exposes_audit_and_never_refreshes_or_reports_success()
    {
        var handler = new Handler(HttpStatusCode.Forbidden);
        var result = Mount(new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:7324/") });
        result.WaitForAssertion(() => Assert.Contains("No active holders.", result.Markup, StringComparison.Ordinal));
        Fill(result);
        result.WaitForAssertion(() => Assert.Contains("permission", result.Find("[role=alert]").TextContent, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Why can I do this?", result.Find("summary").TextContent);
        Assert.DoesNotContain("Submission saved.", result.Markup, StringComparison.Ordinal);
        Assert.Equal("party-s301s2", result.Find("input[name=person]").GetAttribute("value"));
        Assert.Equal(1, handler.Reads);
        Assert.Equal(Fixture("access-grant-body.json"), Assert.Single(handler.Bodies));
    }
    [LiveHostFact]
    public void Live_grant_through_surface_then_find_submitted_party_in_holders()
    {
        using var http = new HttpClient(new LiveHandler(output)) { BaseAddress = new Uri(Environment.GetEnvironmentVariable("HARBORLINE_LIVE_API_ORIGIN")!) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("HARBORLINE_LIVE_API_TOKEN"));
        var result = Mount(http);
        Fill(result);
        result.WaitForAssertion(() => Assert.Contains(result.FindAll("article"), row => row.TextContent.Contains("party-s301s2", StringComparison.Ordinal)));
    }
    private sealed class LiveHandler(ITestOutputHelper output) : DelegatingHandler(new HttpClientHandler())
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            output.WriteLine($"LIVE Blazor {request.Method} {request.RequestUri}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
            return response;
        }
    }
    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        public int Reads { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var resultStatus = HttpStatusCode.OK;
            string body;
            if (path.EndsWith("/submit", StringComparison.Ordinal))
            {
                Bodies.Add(await request.Content!.ReadAsStringAsync(ct)); resultStatus = status;
                body = status == HttpStatusCode.Created ? "{\"instanceId\":\"submission-301\"}" : "{\"code\":\"authorization.permission_required\",\"auditId\":\"30100000-0000-4000-8000-000000000003\"}";
            }
            else if (path.EndsWith("/forms/access.grant-a-role", StringComparison.Ordinal)) body = Fixture("access-grant-form.json");
            else if (path.EndsWith("/holders", StringComparison.Ordinal))
            {
                Reads++;
                using var holders = JsonDocument.Parse(Fixture("access-holders.json"));
                var row = JsonNode.Parse(holders.RootElement.GetProperty("holders")[0].GetRawText())!;
                row["partyId"] = "party-s301s2";
                body = Bodies.Count > 0 && status == HttpStatusCode.Created ? "{\"holders\":[" + row.ToJsonString() + "]}" : "{\"holders\":[]}";
            }
            else throw new InvalidOperationException($"Unexpected request {path}");
            return new HttpResponseMessage(resultStatus) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
