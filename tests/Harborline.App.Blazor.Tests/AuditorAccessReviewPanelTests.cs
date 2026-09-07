using Bunit;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Harborline.UIAdapters.Blazor;
using Microsoft.Extensions.DependencyInjection;

namespace Harborline.App.Blazor.Tests;

public sealed class AuditorAccessReviewPanelTests : BunitContext
{
    private static readonly RoleReference Auditor = new("sys.platform-roles", "auditor");

    private static async Task<IReadOnlyList<AuthorizationCapabilityDefinition>> SeededAsync() =>
        await new FixtureAuthorizationAdminClient().ListCapabilityDefinitionsAsync();

    [Fact]
    public async Task Exactly_one_row_is_derived_from_the_seeded_catalogue()
    {
        var seeded = await SeededAsync();
        var expected = Assert.Single(seeded, row => row.Binding.EffectiveRoles.Contains(Auditor));
        Services.AddSingleton<IAuthorizationAdminClient>(new FixtureAuthorizationAdminClient());
        Services.AddHarborlineUiAdapters();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<AuthorizationAdminPage>();

        cut.WaitForAssertion(() =>
        {
            var review = cut.Find("section[aria-labelledby='auditor-access-review-heading']");
            var row = Assert.Single(review.QuerySelectorAll("article"));
            Assert.Null(review.QuerySelector("[role='alert']"));
            Assert.Equal(
                ["Operation", "Publisher ceiling", "Effective roles", "Binding revision"],
                row.QuerySelectorAll("dt").Select(term => term.TextContent.Trim()));
            var values = row.QuerySelectorAll("dd").Select(value => value.TextContent.Trim()).ToArray();
            Assert.Equal($"{expected.Atom.Operation} · {expected.Atom.ScopeType}:{expected.Atom.ScopeValue}", values[0]);
            Assert.Equal($"{expected.PublisherPackageId} offers sys.platform-roles:auditor, sys.platform-roles:node-operator", values[1]);
            Assert.Equal("sys.platform-roles:auditor, sys.platform-roles:node-operator", values[2]);
            Assert.Equal(expected.Binding.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), values[3]);
        });
    }

    [Fact]
    public async Task No_auditor_capability_warns_rather_than_hiding()
    {
        var definitions = (await SeededAsync()).Where(row => !row.Binding.EffectiveRoles.Contains(Auditor)).ToArray();

        var cut = Render<AuditorAccessReviewPanel>(parameters => parameters.Add(panel => panel.Definitions, definitions));

        Assert.Contains(
            "the Auditor should hold exactly one authorization capability, but the catalogue derives 0",
            cut.Find("[role='alert']").TextContent,
            StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("article"));
    }

    [Fact]
    public async Task Two_auditor_capabilities_warn_and_both_rows_stay_visible()
    {
        var seeded = await SeededAsync();
        var held = Assert.Single(seeded, row => row.Binding.EffectiveRoles.Contains(Auditor));
        var second = held with
        {
            DefinitionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            Atom = held.Atom with { Operation = "contacts:read" },
        };

        var cut = Render<AuditorAccessReviewPanel>(parameters => parameters.Add(panel => panel.Definitions, [.. seeded, second]));

        Assert.Contains(
            "the Auditor should hold exactly one authorization capability, but the catalogue derives 2",
            cut.Find("[role='alert']").TextContent,
            StringComparison.Ordinal);
        Assert.Equal(2, cut.FindAll("article").Count);
        Assert.Single(cut.FindAll("h3"), heading => heading.TextContent.Trim() == "contacts:read");
    }
}
