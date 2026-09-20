using System.Text.Json;

using Bunit;

using Harborline.App.Blazor.ReferenceHost.Admin.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// T-461. The Blazor twin of apps/react/src/admin/__tests__/configurationProposalPage.test.tsx,
/// walking the SAME Records-and-Forms example step for step, from the SAME released definition and
/// the SAME conformance fixture the React lane and the platform's own two renderers drive:
/// apps/blazor/.feed/platform/, written out of the pinned platform checkout by
/// scripts/build-local-feed.mjs. The two lanes complete one example, not two similar ones.
/// </summary>
public sealed class ConfigurationProposalPageTests : BunitContext
{
    private static DirectoryInfo RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "repository.yaml"))) root = root.Parent;
        return root ?? throw new InvalidOperationException("repository.yaml was not found above the test output directory.");
    }

    private static JsonDocument Feed(string name) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot().FullName, "apps/blazor/.feed/platform", name)));

    private static JsonElement Example()
    {
        using var cases = Feed("proposal-cases.json");
        return cases.RootElement.Clone();
    }

    private static JsonElement Step(string step) => Example().GetProperty("cases").EnumerateArray()
        .Single(entry => entry.GetProperty("step").GetString() == step);

    private static string Label(string status)
    {
        using var definitions = Feed("configuration-proposal.json");
        return definitions.RootElement.GetProperty("statuses").GetProperty(status).GetProperty("values").GetProperty("en").GetString()!;
    }

    /// <summary>Turns one fixture case into the api answer the surface renders.</summary>
    private static ProposedChange Answer(string step)
    {
        var entry = Step(step);
        var values = entry.GetProperty("values").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);
        var edits = values["editedDefinitions"].Length == 0
            ? []
            : values["editedDefinitions"].Split('\n')
                .Select(line => new ProposedEdit(line[..line.IndexOf(" (", StringComparison.Ordinal)],
                    line[(line.IndexOf('(', StringComparison.Ordinal) + 1)..^1])).ToArray();
        var check = values["checkState"] == "No check recorded."
            ? null
            : new RecordedCheck("receipt-1", values["workingDigest"],
                values["checkState"].StartsWith("Current", StringComparison.Ordinal));
        return new ProposedChange(entry.GetProperty("status").GetString()!, values["tenantKey"], values["proposalId"],
            values["baselineDigest"], values["effectiveDigest"], values["workingDigest"], edits,
            values["savedVersion"].Length == 0 ? 0 : int.Parse(values["savedVersion"].Split(':')[0], System.Globalization.CultureInfo.InvariantCulture),
            null, check, null, [], values);
    }

    private StubClient Register(params string[] script)
    {
        var client = new StubClient(script);
        Services.AddSingleton<IConfigurationProposalClient>(client);
        return client;
    }

    [Theory]
    [InlineData("autosaved-one-of-two")]
    [InlineData("proposed")]
    [InlineData("saved")]
    [InlineData("released")]
    [InlineData("check-invalidated-by-a-later-edit")]
    [InlineData("released-a-superseded-saved-version")]
    [InlineData("released-against-a-stale-baseline")]
    public void Renders_every_step_of_the_example_through_the_released_platform_definition(string step)
    {
        Register(step);
        var entry = Step(step);
        var values = entry.GetProperty("values").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);
        var cut = Render<ConfigurationProposalPage>(parameters => parameters.Add(page => page.ProposalId, "proposal-1"));

        // Acceptance 6: every label on the surface is the platform's released vocabulary.
        Assert.Contains("Proposed change", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(Label(entry.GetProperty("status").GetString()!), cut.Find("#status").TextContent);
        foreach (var pair in values) Assert.Equal(pair.Value, cut.Find($"#{pair.Key}").TextContent);

        // Acceptance 1: the recorded baseline is on the surface, and the effective generation matches
        // it except in the one case where the tenant moved on underneath the author.
        Assert.Equal(Example().GetProperty("baselineDigest").GetString(), cut.Find("#baselineDigest").TextContent);
        if (step == "released-against-a-stale-baseline")
        {
            Assert.NotEqual(cut.Find("#baselineDigest").TextContent, cut.Find("#effectiveDigest").TextContent);
            Assert.Contains("configuration-baseline-stale", cut.Find("#refusals").TextContent, StringComparison.Ordinal);
        }
        else Assert.Equal(cut.Find("#baselineDigest").TextContent, cut.Find("#effectiveDigest").TextContent);
    }

    [Fact]
    public void Walks_the_example_from_proposed_through_saved_to_released()
    {
        var client = Register("proposed", "saved", "released");
        var cut = Render<ConfigurationProposalPage>(parameters => parameters.Add(page => page.ProposalId, "proposal-1"));

        // Acceptance 2: the two autosaved edits are on the surface before any version is saved, and no
        // Saved version, author or rationale is shown yet.
        Assert.Equal(Step("proposed").GetProperty("values").GetProperty("editedDefinitions").GetString(),
            cut.Find("#editedDefinitions").TextContent);
        Assert.Equal(string.Empty, cut.Find("#savedVersion").TextContent);
        Assert.Equal(string.Empty, cut.Find("#savedBy").TextContent);
        // Nothing may be released before a version is saved and checked.
        Assert.True(cut.Find("#release").HasAttribute("disabled"));

        cut.Find("#save-version").Click();
        Assert.Equal("Saved version", cut.Find("#status").TextContent);
        Assert.Equal("dana.okafor", cut.Find("#savedBy").TextContent);
        Assert.Equal(Step("saved").GetProperty("values").GetProperty("rationale").GetString(), cut.Find("#rationale").TextContent);
        Assert.Equal("Current (receipt-1)", cut.Find("#checkState").TextContent);

        cut.Find("#release").Click();
        Assert.Equal("Released package", cut.Find("#status").TextContent);
        // Acceptance 5: the digest on the surface is the exported artifact's own digest.
        Assert.Contains(Example().GetProperty("releasedPackageDigest").GetString()!,
            cut.Find("#releasedPackage").TextContent, StringComparison.Ordinal);
        Assert.Equal(string.Empty, cut.Find("#refusals").TextContent);
        Assert.Equal(1, client.SaveVersions);
        Assert.Equal(1, client.Releases);
    }

    [Fact]
    public void Does_not_offer_a_release_once_a_later_edit_has_invalidated_the_check()
    {
        Register("check-invalidated-by-a-later-edit");
        var cut = Render<ConfigurationProposalPage>(parameters => parameters.Add(page => page.ProposalId, "proposal-1"));

        // Acceptance 3: the surface reports the invalidation the api bound, and the act is not offered.
        Assert.Contains("Invalidated by a later edit", cut.Find("#checkState").TextContent, StringComparison.Ordinal);
        Assert.NotEqual("Released package", cut.Find("#status").TextContent);
        Assert.Equal(string.Empty, cut.Find("#releasedPackage").TextContent);
        Assert.True(cut.Find("#release").HasAttribute("disabled"));
    }

    /// <summary>Replays the example's steps in order; any act outside the script is the failure.</summary>
    private sealed class StubClient(params string[] script) : IConfigurationProposalClient
    {
        private int _position;
        public int SaveVersions { get; private set; }
        public int Releases { get; private set; }

        private Task<ProposedChange> Next() => Task.FromResult(Answer(script[Math.Min(_position++, script.Length - 1)]));

        public Task<ProposedChange> ReadAsync(string proposalId, CancellationToken cancellationToken = default) => Next();
        public Task<ProposedChange> SaveVersionAsync(string proposalId, string rationale, CancellationToken cancellationToken = default)
        {
            SaveVersions++;
            return Next();
        }
        public Task<ProposedChange> ReleaseAsync(string proposalId, int ordinal, string packageKey, string revision,
            CancellationToken cancellationToken = default)
        {
            Releases++;
            return Next();
        }
        public Task<ProposedChange> StartAsync(string proposalId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("not used");
        public Task<ProposedChange> AutosaveAsync(string proposalId, AutosaveEdit edit, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("not used");
        public Task<ProposedChange> RecordCheckAsync(string proposalId, string receiptId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("not used");
        public Task<IReadOnlyList<ReleasedPackageSummary>> OfferedAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("not used");
    }
}
