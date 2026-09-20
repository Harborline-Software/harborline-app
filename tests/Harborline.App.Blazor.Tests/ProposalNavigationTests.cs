using System.Net;
using System.Text;
using System.Text.Json;

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
/// T-668. Reachability, per lane — the Blazor twin of
/// apps/react/src/__tests__/proposalNavigation.test.tsx.
///
/// The api projects <c>configuration.proposal</c> only to a caller its proposed-change routes would
/// admit (the selected-session-product audience holding <c>packages:author</c> install-wide), so the
/// two fixtures here are that one route's two answers: the projection an author is served, and the
/// projection an operator who cannot author is served — the SAME workspace, minus the one entry. The
/// shell mounts the existing T-461 surface on the entry's id; nothing about how that page renders
/// changed.
/// </summary>
public sealed class ProposalNavigationTests : BunitContext
{
    private const string ProposalRoute = "/api/local-node/configuration/proposals/configuration.proposal";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static DirectoryInfo RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "repository.yaml"))) root = root.Parent;
        return root ?? throw new InvalidOperationException("repository.yaml was not found above the test output directory.");
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot().FullName, "tests/fixtures", name));

    /// <summary>
    /// The api answer for the working proposed change, as bytes on the wire, built from the SAME
    /// released conformance example T-461's surface test drives. This test authors no payload of its
    /// own, so the answer it replays cannot drift from the one the surface is proved against.
    /// </summary>
    private static string Read()
    {
        using var cases = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepositoryRoot().FullName, "apps/blazor/.feed/platform/proposal-cases.json")));
        var entry = cases.RootElement.GetProperty("cases").EnumerateArray()
            .Single(candidate => candidate.GetProperty("step").GetString() == "proposed");
        var values = entry.GetProperty("values").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);
        return JsonSerializer.Serialize(
            new ProposedChange(entry.GetProperty("status").GetString()!, values["tenantKey"], values["proposalId"],
                values["baselineDigest"], values["effectiveDigest"], values["workingDigest"], [], 0, null, null, null,
                [], values),
            JsonOptions);
    }

    private (IRenderedComponent<Shell> Result, Handler Handler) Mount(string navigationFixture)
    {
        var handler = new Handler(Fixture(navigationFixture), Read());
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:7322/") };
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new Media());
        Services.AddSingleton<IAuthorizationAdminClient>(new HttpAuthorizationAdminClient(http));
        Services.AddSingleton<IPackNavigationClient>(new HttpPackNavigationClient(http));
        Services.AddSingleton<IConfigurationProposalClient>(new HttpConfigurationProposalClient(http));
        JSInterop.Mode = JSRuntimeMode.Loose;
        var result = Render<Shell>();
        result.WaitForAssertion(() => Assert.Contains("Access", result.Markup, StringComparison.Ordinal));
        return (result, handler);
    }

    [Fact]
    public void An_author_reaches_the_proposed_change_surface_from_the_projected_entry()
    {
        var (result, handler) = Mount("proposal-navigation.json");

        result.Find("a[href='/workspaces/configuration']").Click();
        var item = result.Find("a[href='/workspaces/configuration.proposal']");
        Assert.Equal("Proposed change", item.TextContent.Trim());
        item.Click();

        // The existing T-461 surface, rendering the released detail through the shared
        // HarborlineSchemaForm exactly as its own test proves. This asserts it is MOUNTED, not how it
        // renders: the heading is the platform's released vocabulary, which this lane authors none of.
        result.WaitForAssertion(() => Assert.Equal(
            ConfigurationProposalDefinitions.Label("proposed"),
            result.Find("#configuration-proposal-heading").TextContent.Trim()));
        // The entry addresses this install's working proposed change by the entry's own id.
        Assert.Equal([ProposalRoute], handler.Paths.Where(path => path.Contains("/proposals/", StringComparison.Ordinal)));
    }

    [Fact]
    public void An_operator_who_cannot_author_is_not_shown_the_entry_and_still_reaches_activation()
    {
        var (result, handler) = Mount("proposal-navigation-without-author.json");

        result.Find("a[href='/workspaces/configuration']").Click();

        Assert.Empty(result.FindAll("a[href='/workspaces/configuration.proposal']"));
        Assert.DoesNotContain("configuration.proposal", result.Markup, StringComparison.Ordinal);
        // The workspace itself survives: the api scopes the two entries separately, so an operator who
        // cannot author still reaches activation.
        Assert.Equal("Activation",
            result.Find("a[href='/workspaces/configuration.activation']").TextContent.Trim());
        Assert.DoesNotContain(handler.Paths, path => path.Contains("/proposals/", StringComparison.Ordinal));
    }

    private sealed class Handler(string navigation, string proposal) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            var body = path.EndsWith("/navigation/workspaces", StringComparison.Ordinal) ? navigation
                : path.Contains("/configuration/proposals/", StringComparison.Ordinal) ? proposal
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
