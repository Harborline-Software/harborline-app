using System.Net;
using System.Text;
using System.Text.Json;

using Bunit;

using Harborline.App.Blazor.ReferenceHost.Admin.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// T-460. The Blazor twin of apps/react/src/admin/__tests__/configurationActivationPage.test.tsx,
/// driven by the SAME conformance cases and the SAME released definition the React lane and the
/// platform's own two renderers drive: apps/blazor/.feed/platform/, written out of the pinned
/// platform checkout by scripts/build-local-feed.mjs. Neither app lane can drift from the other.
/// </summary>
public sealed class ConfigurationActivationPageTests : BunitContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static DirectoryInfo RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "repository.yaml"))) root = root.Parent;
        return root ?? throw new InvalidOperationException("repository.yaml was not found above the test output directory.");
    }

    private static JsonDocument Feed(string name) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot().FullName, "apps/blazor/.feed/platform", name)));

    private static JsonElement Case(string status)
    {
        using var cases = Feed("activation-cases.json");
        return cases.RootElement.GetProperty("cases").EnumerateArray()
            .Single(entry => entry.GetProperty("status").GetString() == status).Clone();
    }

    private static string Label(string status)
    {
        using var definitions = Feed("configuration-activation.json");
        return definitions.RootElement.GetProperty("statuses").GetProperty(status).GetProperty("values").GetProperty("en").GetString()!;
    }

    private static ConfigurationActivationOutcome Outcome(JsonElement fixtureCase)
    {
        var values = fixtureCase.GetProperty("values").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);
        return new ConfigurationActivationOutcome(fixtureCase.GetProperty("status").GetString()!, values["tenantKey"],
            values["candidateDigest"], values["expectedBaselineDigest"], values["effectiveDigest"], [], Detail: values);
    }

    private IConfigurationActivationClient Register(string digest = "baseline-digest")
    {
        var client = new StubClient(digest);
        Services.AddSingleton<IConfigurationActivationClient>(client);
        return client;
    }

    [Theory]
    [InlineData("released")]
    [InlineData("preparing")]
    [InlineData("refused")]
    [InlineData("effective")]
    public void Renders_the_released_status_through_the_platform_schema_form(string status)
    {
        Register();
        var fixtureCase = Case(status);
        var values = fixtureCase.GetProperty("values").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);

        var cut = Render<ConfigurationActivationPage>(parameters => parameters.Add(page => page.Outcome, Outcome(fixtureCase)));

        Assert.Contains("Configuration activation", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(Label(status), cut.Find("#status").TextContent);
        foreach (var pair in values) Assert.Equal(pair.Value, cut.Find($"#{pair.Key}").TextContent);
        // Read-only: the surface reports, it never offers a way to change what it reports.
        Assert.Empty(cut.FindAll("input,select,textarea,button"));

        if (status == "effective")
        {
            Assert.Equal(values["candidateDigest"], values["effectiveDigest"]);
            Assert.Equal("", values["refusals"]);
        }
        else
        {
            Assert.Equal(values["expectedBaselineDigest"], values["effectiveDigest"]);
            Assert.NotEqual(values["candidateDigest"], values["effectiveDigest"]);
        }
    }

    [Fact]
    public void Never_renders_a_refused_projection_as_effective()
    {
        Register();
        var cut = Render<ConfigurationActivationPage>(parameters => parameters.Add(page => page.Outcome, Outcome(Case("refused"))));

        var rendered = cut.Find("#status").TextContent;
        Assert.Equal(Label("refused"), rendered);
        Assert.NotEqual(Label("effective"), rendered);
        Assert.Contains("projection-failed (forms/invoice)", cut.Find("#refusals").TextContent, StringComparison.Ordinal);
        // The effective generation stayed the prior baseline, and the candidate never became it.
        Assert.NotEqual(cut.Find("#candidateDigest").TextContent, cut.Find("#effectiveDigest").TextContent);
    }

    [Fact]
    public void Reports_the_effective_generation_the_api_read_and_nothing_more()
    {
        Register("only-baseline");
        var cut = Render<ConfigurationActivationPage>();

        Assert.Equal("only-baseline", cut.Find("#effective-generation").TextContent);
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public async Task Reads_a_refusal_out_of_the_api_422_rather_than_throwing_it_away()
    {
        var values = Case("refused").GetProperty("values").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);
        var body = JsonSerializer.Serialize(new
        {
            status = "refused", tenantKey = values["tenantKey"], candidateDigest = values["candidateDigest"],
            expectedBaselineDigest = values["expectedBaselineDigest"], effectiveDigest = values["effectiveDigest"],
            refusals = new[] { new { code = "projection-failed", target = "forms/invoice", message = "Invoice form projection failed." } },
            detail = values,
        }, JsonOptions);
        using var http = new HttpClient(new StubHandler(HttpStatusCode.UnprocessableEntity, body)) { BaseAddress = new Uri("http://localhost/") };

        var outcome = await new HttpConfigurationActivationClient(http).ActivateAsync(
            new(values["expectedBaselineDigest"], values["candidateDigest"], new("intent-1", "T-460 proof")));

        Assert.Equal("refused", outcome.Status);
        Assert.Equal(Label("refused"), outcome.Detail!["status"]);
    }

    [Fact]
    public async Task Throws_when_the_api_refuses_the_effective_read()
    {
        using var http = new HttpClient(new StubHandler(HttpStatusCode.NotFound, """{"code":"configuration-generation-missing"}"""))
        { BaseAddress = new Uri("http://localhost/") };

        var exception = await Assert.ThrowsAsync<ConfigurationActivationException>(
            () => new HttpConfigurationActivationClient(http).ReadEffectiveAsync());
        Assert.Equal("configuration-generation-missing", exception.Code);
    }

    private sealed class StubClient(string digest) : IConfigurationActivationClient
    {
        public Task<EffectiveGeneration> ReadEffectiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new EffectiveGeneration(digest, "sha256", JsonDocument.Parse("{}").RootElement.Clone()));

        public Task<ConfigurationActivationOutcome> PrepareAsync(PrepareConfigurationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ConfigurationActivationOutcome> ActivateAsync(ActivateConfigurationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
