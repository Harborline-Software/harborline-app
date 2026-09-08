using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.App.Blazor.ReferenceHost.Authorization;
using Harborline.UIAdapters.Blazor;
using Microsoft.Extensions.DependencyInjection;

namespace Harborline.App.Blazor.Tests;

public sealed class AuthorizationTraceTests : BunitContext
{
    private sealed record Fixture(Guid AuditId, AuthorizationTraceRead Read);
    private static Fixture ReadFixture()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Harborline.App.slnx"))) root = root.Parent;
        return JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(root!.FullName,
            "tests/fixtures/authorization-trace.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
    private IRenderedComponent<CapabilityBindingEditor> Mount(Fixture fixture, Func<Guid, Task<AuthorizationTraceRead>> read)
    {
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        JSInterop.Mode = JSRuntimeMode.Loose;
        var definition = new AuthorizationCapabilityDefinition(Guid.NewGuid(), "fixture", 1,
            new("tax.return.write", "Tenant", "tenant-163"), [], new(1, [], null));
        var view = Render<CapabilityBindingEditor>(parameters => parameters.Add(p => p.Definition, definition)
            .Add(p => p.RoleDefinitions, []).Add(p => p.DecisionTrace, new RecordedDecision(fixture.AuditId, new HttpAuthorizationAdminClient(new HttpClient(new TraceHandler(fixture.AuditId, read)) { BaseAddress = new Uri("http://127.0.0.1:7322") }).ReadTraceAsync)));
        Assert.Equal("Why can I do this?", view.Find("details summary").TextContent);
        view.Find("details").TriggerEvent("ontoggle", EventArgs.Empty);
        return view;
    }

    [Theory]
    [InlineData("allowed")]
    [InlineData("denied")]
    public void Four_parts_in_order_and_deciding_grant_from_shared_fixture(string verdict)
    {
        var fixture = ReadFixture();
        var result = fixture.Read;
        if (verdict == "denied") result = result with { Steps = result.Steps.Select(step => step.Ordinal switch
        {
            2 => step with { Facts = ["roles:none", "deciding:none"] },
            4 => step with { Facts = ["verdict:denied", "refusal:NoEffectiveRole", "version:1"] },
            _ => step,
        }).ToArray() };
        Guid? requested = null;
        var view = Mount(fixture, id => { requested = id; return Task.FromResult(result); });
        view.WaitForAssertion(() => Assert.Single(view.FindAll("ol[aria-label='Authorization trace']")));
        Assert.Equal(fixture.AuditId, requested);
        Assert.Equal(["Act kind", "Roles in force for the subject with scope and dates", "Standings", "Verdict"],
            view.FindAll("details h4").Select(heading => heading.TextContent));
        var parts = view.FindAll("details li");
        for (var index = 0; index < 4; index++)
            foreach (var fact in result.Steps[index].Facts) Assert.Contains(fact, parts[index].TextContent, StringComparison.Ordinal);
        Assert.Contains($"Deciding grant: {(verdict == "allowed" ? "grant-163@1" : "None recorded")}", parts[3].TextContent, StringComparison.Ordinal);
        Assert.Empty(view.FindAll("details [class]"));
    }

    [Theory]
    [InlineData(2, "You do not have permission to read this authorization trace.")]
    [InlineData(1, "No authorization trace was recorded for this decision.")]
    [InlineData(99, "The recorded authorization trace is incomplete or unsupported.")]
    [InlineData(-1, "Unable to read the authorization trace. Try again.")]
    public void Unavailable_reads_disclose_no_facts_and_refusal_or_error_retries(int availability, string message)
    {
        var fixture = ReadFixture();
        var calls = 0;
        var view = Mount(fixture, _ => ++calls > 1 ? Task.FromResult(fixture.Read)
            : availability == -1 ? Task.FromException<AuthorizationTraceRead>(new HttpRequestException("private transport detail"))
            : Task.FromResult(fixture.Read with { Availability = availability }));
        view.WaitForAssertion(() => Assert.Contains(message, view.Find("details").TextContent, StringComparison.Ordinal));
        Assert.Empty(view.FindAll("details ol"));
        Assert.DoesNotContain("deciding:grant:grant-163@1", view.Markup, StringComparison.Ordinal);
        if (availability is 2 or -1)
        {
            view.Find("details button").Click();
            view.WaitForAssertion(() => Assert.Single(view.FindAll("details ol")));
        }
    }
    private sealed class TraceHandler(Guid id, Func<Guid, Task<AuthorizationTraceRead>> read) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal($"/api/local-node/authorization/traces/{id:D}", request.RequestUri!.AbsolutePath);
            var result = await read(id);
            return new HttpResponseMessage(result.Availability == 2 ? HttpStatusCode.Forbidden : HttpStatusCode.OK)
            { Content = JsonContent.Create(result) };
        }
    }
}
