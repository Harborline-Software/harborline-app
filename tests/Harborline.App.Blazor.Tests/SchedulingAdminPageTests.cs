using Bunit;
using Harborline.App.Blazor.ReferenceHost.Admin.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// Verifies the scheduling administration page's list, detail, restore, and cancel interactions.
/// </summary>
public sealed class SchedulingAdminPageTests : BunitContext
{
    [Fact]
    public void List_renders_the_fixture_definition_rows()
    {
        var cut = RenderPage(new FixtureSchedulingAdminClient());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, cut.FindAll("div.hl-data-grid__row--leaf").Count);
            var inspection = cut.Find("div.hl-data-grid__row--leaf[data-row-id='inspection-protocol@3']");
            Assert.Equal("inspection-protocol", inspection.QuerySelector("div[role='gridcell'][data-column-id='id']")!.TextContent.Trim());
            Assert.Equal("Inspection protocol", inspection.QuerySelector("div[role='gridcell'][data-column-id='title']")!.TextContent.Trim());
            Assert.Equal("3", inspection.QuerySelector("div[role='gridcell'][data-column-id='revision']")!.TextContent.Trim());
            Assert.Equal("user:fixture-scheduler", inspection.QuerySelector("div[role='gridcell'][data-column-id='updatedBy']")!.TextContent.Trim());
        });
    }

    [Fact]
    public void Detail_renders_the_Scheduling_h1_with_an_h2_section_heading()
    {
        var cut = RenderPage(new FixtureSchedulingAdminClient());
        ClickDefinition(cut, "inspection-protocol");

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("aside[data-detail-panel]");
            Assert.Equal("Scheduling", cut.Find("h1").TextContent.Trim());
            Assert.Equal(
                ["Definition"],
                panel.QuerySelectorAll("h2").Select(heading => heading.TextContent.Trim()));
            Assert.Equal("Scheduling definition inspection-protocol", panel.GetAttribute("aria-label"));
            // The React lane pins the same five terms in the same order — the metadata list is the
            // part of the surface a reader compares between lanes first.
            Assert.Equal(
                ["Definition id", "Title", "Head revision", "Updated", "Updated by"],
                panel.QuerySelectorAll("dl dt").Select(term => term.TextContent.Trim()));
            AssertDefinitionValue(panel, "Definition id", "inspection-protocol");
            AssertDefinitionValue(panel, "Title", "Inspection protocol");
            AssertDefinitionValue(panel, "Head revision", "3");
            AssertDefinitionValue(panel, "Updated", "2026-08-18 09:00 UTC");
            AssertDefinitionValue(panel, "Updated by", "user:fixture-scheduler");
            Assert.Equal(
                "Revision history",
                panel.QuerySelector("table.hl-table caption")!.TextContent.Trim());
            var json = panel.QuerySelector("[data-testid='definition-json']")!.TextContent;
            Assert.Contains("Inspection protocol", json);
            Assert.Contains("weekly", json);
            var rows = panel.QuerySelectorAll("table.hl-table tbody tr");
            Assert.Equal(3, rows.Length);
            Assert.Equal("3", rows[0].QuerySelector("td")!.TextContent.Trim());
        });
    }

    [Fact]
    public void Restore_confirms_refreshes_list_and_history_and_reports_the_new_head()
    {
        var spy = new SpySchedulingAdminClient();
        var cut = RenderPage(spy);
        ClickDefinition(cut, "inspection-protocol");
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("table.hl-table tbody tr").Count));

        FindVersionRow(cut, "1").QuerySelector("button.happ-link")!.Click();
        // The confirm dialog composes the base dialog; the confirm copy renders as the dialog
        // description. The sentence matches the React lane's word for word.
        Assert.Equal(
            "Appends revision 1's content of 'inspection-protocol' as the new head revision, effective immediately. History is never modified.",
            cut.Find("p.hl-dialog__description").TextContent.Trim());
        cut.Find("button.hl-confirm-dialog__confirm").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, spy.RestoreCalls);
            Assert.Equal(("inspection-protocol", 1), spy.LastRestore);
            Assert.True(spy.ListDefinitionsCalls >= 2);
            Assert.True(spy.ListVersionsCalls >= 2);
            Assert.Equal(4, cut.FindAll("table.hl-table tbody tr").Count);
            Assert.Equal("4", FindVersionRow(cut, "4").QuerySelector("td")!.TextContent.Trim());
            Assert.NotNull(cut.Find("div.hl-data-grid__row--leaf[data-row-id='inspection-protocol@4']"));
            Assert.Equal("Revision 4 restored from 1.", cut.Find("[data-testid='restore-status']").TextContent.Trim());
        });
    }

    [Fact]
    public void Restore_cancel_does_not_call_the_client_or_show_status()
    {
        var spy = new SpySchedulingAdminClient();
        var cut = RenderPage(spy);
        ClickDefinition(cut, "inspection-protocol");
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("table.hl-table tbody tr").Count));

        FindVersionRow(cut, "1").QuerySelector("button.happ-link")!.Click();
        cut.Find("button.hl-confirm-dialog__cancel").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(0, spy.RestoreCalls);
            Assert.Empty(cut.FindAll("[data-testid='restore-status']"));
        });
    }

    private IRenderedComponent<SchedulingAdminPage> RenderPage(ISchedulingAdminClient client)
    {
        Services.AddSingleton(client);
        Services.AddHarborlineUiAdapters();
        // Registered after the adapters so it wins resolution: the real MediaQueryObserver needs the
        // browser's media-query JS module, which the test renderer does not load. The platform's own
        // component tests stub the observer the same way.
        Services.AddSingleton<IMediaQueryObserver>(new StubMediaQueryObserver());
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<SchedulingAdminPage>(parameters =>
            parameters.Add(page => page.PanelRailCapable, true));
    }

    private static void ClickDefinition(IRenderedComponent<SchedulingAdminPage> cut, string definitionId)
    {
        var button = Assert.Single(
            cut.FindAll("div[role='gridcell'][data-column-id='id'] button.happ-link"),
            candidate => candidate.TextContent.Trim() == definitionId);
        button.Click();
    }

    private static AngleSharp.Dom.IElement FindVersionRow(
        IRenderedComponent<SchedulingAdminPage> cut,
        string revision) =>
        Assert.Single(
            cut.FindAll("table.hl-table tbody tr"),
            row => row.QuerySelector("td")!.TextContent.Trim() == revision);

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

    private sealed class SpySchedulingAdminClient : ISchedulingAdminClient
    {
        private readonly FixtureSchedulingAdminClient inner = new();

        public int ListDefinitionsCalls { get; private set; }

        public int ListVersionsCalls { get; private set; }

        public int RestoreCalls { get; private set; }

        public (string Id, int Revision)? LastRestore { get; private set; }

        public Task<IReadOnlyList<SchedulingDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default)
        {
            ListDefinitionsCalls++;
            return inner.ListDefinitionsAsync(ct);
        }

        public Task<SchedulingDefinitionView> GetDefinitionAsync(
            string definitionId,
            CancellationToken ct = default) =>
            inner.GetDefinitionAsync(definitionId, ct);

        public Task<IReadOnlyList<SchedulingDefinitionSummary>> ListVersionsAsync(
            string definitionId,
            CancellationToken ct = default)
        {
            ListVersionsCalls++;
            return inner.ListVersionsAsync(definitionId, ct);
        }

        public Task<RestoreResult> RestoreRevisionAsync(
            string definitionId,
            int revision,
            CancellationToken ct = default)
        {
            RestoreCalls++;
            LastRestore = (definitionId, revision);
            return inner.RestoreRevisionAsync(definitionId, revision, ct);
        }
    }
}
