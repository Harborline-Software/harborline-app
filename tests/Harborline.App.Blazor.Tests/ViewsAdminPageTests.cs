using Bunit;
using Harborline.App.Blazor.ReferenceHost.Admin.Views;
using Microsoft.Extensions.DependencyInjection;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// Verifies the views administration page's list and read-only detail interactions.
/// </summary>
public sealed class ViewsAdminPageTests : BunitContext
{
    [Fact]
    public void List_renders_the_fixture_definition_rows()
    {
        var cut = RenderPage(new FixtureViewsAdminClient());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, cut.FindAll("div.hl-data-grid__row--leaf").Count);
            var trialBalance = cut.Find("div.hl-data-grid__row--leaf[data-row-id='work-orders-table@2.1.0']");
            Assert.Equal("work-orders-table", trialBalance.QuerySelector("div[role='gridcell'][data-column-id='key']")!.TextContent.Trim());
            Assert.Equal("Work orders table", trialBalance.QuerySelector("div[role='gridcell'][data-column-id='title']")!.TextContent.Trim());
            Assert.Equal("2.1.0", trialBalance.QuerySelector("div[role='gridcell'][data-column-id='version']")!.TextContent.Trim());
            Assert.Equal("table", trialBalance.QuerySelector("div[role='gridcell'][data-column-id='kind']")!.TextContent.Trim());
            Assert.Equal("Tenant", trialBalance.QuerySelector("div[role='gridcell'][data-column-id='cascadeLayer']")!.TextContent.Trim());
            var occupancyBoard = cut.Find("div.hl-data-grid__row--leaf[data-row-id='occupancy-board@0.3.0']");
            Assert.Equal("board", occupancyBoard.QuerySelector("div[role='gridcell'][data-column-id='kind']")!.TextContent.Trim());
        });
    }

    [Fact]
    public void Detail_renders_the_Views_h1_with_h2_section_headings()
    {
        var cut = RenderPage(new FixtureViewsAdminClient());
        ClickDefinition(cut, "work-orders-table");

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("aside[data-detail-panel]");
            Assert.Equal("Views", cut.Find("h1").TextContent.Trim());
            Assert.Equal(
                ["Parameters", "Provenance"],
                panel.QuerySelectorAll("h2").Select(heading => heading.TextContent.Trim()));
            Assert.Equal("View definition work-orders-table", panel.GetAttribute("aria-label"));
            AssertDefinitionValue(panel, "Key", "work-orders-table");
            AssertDefinitionValue(panel, "Version", "2.1.0");
            AssertDefinitionValue(panel, "Kind", "table");
            AssertDefinitionValue(panel, "Cascade layer", "Tenant");
            AssertDefinitionValue(panel, "Schema version", "1");
            Assert.Contains("work-order", panel.QuerySelector("pre[data-testid='parameters-json']")!.TextContent);
            Assert.Contains("harborline.core-views", panel.QuerySelector("pre[data-testid='provenance-json']")!.TextContent);
            Assert.Equal("Ordered by semver", panel.QuerySelector("caption.hl-table__caption")!.TextContent.Trim());
            var rows = FindVersionRows(panel);
            Assert.Equal(3, rows.Count);
            Assert.All(rows, row =>
            {
                Assert.Empty(row.QuerySelectorAll("button"));
                Assert.Equal(3, row.QuerySelectorAll("td").Length);
            });
        });
    }

    [Fact]
    public void Detail_shows_the_ordinal_caption_for_ordinal_histories()
    {
        var cut = RenderPage(new FixtureViewsAdminClient());
        ClickDefinition(cut, "occupancy-board");

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("aside[data-detail-panel]");
            Assert.Equal("Ordered ordinally", panel.QuerySelector("caption.hl-table__caption")!.TextContent.Trim());
            Assert.Equal(
                ["2024-legacy", "0.3.0"],
                FindVersionRows(panel).Select(row => row.QuerySelector("td")!.TextContent.Trim()));
        });
    }

    private IRenderedComponent<ViewsAdminPage> RenderPage(IViewsAdminClient client)
    {
        Services.AddSingleton(client);
        Services.AddHarborlineUiAdapters();
        // Registered after the adapters so it wins resolution: the real MediaQueryObserver needs the
        // browser's media-query JS module, which the test renderer does not load. The platform's own
        // component tests stub the observer the same way.
        Services.AddSingleton<IMediaQueryObserver>(new StubMediaQueryObserver());
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<ViewsAdminPage>(parameters =>
            parameters.Add(page => page.PanelRailCapable, true));
    }

    private static void ClickDefinition(IRenderedComponent<ViewsAdminPage> cut, string key)
    {
        var button = Assert.Single(
            cut.FindAll("div[role='gridcell'][data-column-id='key'] button.happ-link"),
            candidate => candidate.TextContent.Trim() == key);
        button.Click();
    }

    private static IReadOnlyList<AngleSharp.Dom.IElement> FindVersionRows(AngleSharp.Dom.IElement panel) =>
        panel.QuerySelectorAll("table.hl-table tbody tr").ToArray();

    private static void AssertDefinitionValue(AngleSharp.Dom.IElement panel, string term, string expected)
    {
        var definitionTerm = Assert.Single(
            panel.QuerySelectorAll("dl dt"),
            item => item.TextContent.Trim() == term);
        Assert.Equal(expected, definitionTerm.NextElementSibling!.TextContent.Trim());
    }

    private sealed class StubMediaQueryObserver : IMediaQueryObserver
    {
        public ValueTask<IMediaQuerySubscription> ObserveAsync(
            string query,
            Func<MediaQueryChange, ValueTask> onChanged,
            CancellationToken cancellationToken = default) =>
            new(new StubSubscription(query));

        private sealed class StubSubscription(string query) : IMediaQuerySubscription
        {
            public string Query { get; } = query;

            public bool Matches => true;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
