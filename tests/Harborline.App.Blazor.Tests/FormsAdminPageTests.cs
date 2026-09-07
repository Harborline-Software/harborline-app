using Bunit;
using Harborline.App.Blazor.ReferenceHost.Admin.Forms;
using Microsoft.Extensions.DependencyInjection;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// Verifies the forms administration page's list, detail, restore, and cancel interactions.
/// </summary>
public sealed class FormsAdminPageTests : BunitContext
{
    [Fact]
    public void List_renders_the_fixture_definition_rows()
    {
        var cut = RenderPage(new FixtureFormsAdminClient());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, cut.FindAll("div.hl-data-grid__row--leaf").Count);
            var incident = cut.Find("div.hl-data-grid__row--leaf[data-row-id='incident-intake@1.0.3']");
            Assert.Equal("incident-intake", incident.QuerySelector("div[role='gridcell'][data-column-id='key']")!.TextContent.Trim());
            Assert.Equal("Incident intake", incident.QuerySelector("div[role='gridcell'][data-column-id='title']")!.TextContent.Trim());
            Assert.Equal("1.0.3", incident.QuerySelector("div[role='gridcell'][data-column-id='version']")!.TextContent.Trim());
            Assert.Equal("Tenant", incident.QuerySelector("div[role='gridcell'][data-column-id='cascadeLayer']")!.TextContent.Trim());
            var crew = cut.Find("div.hl-data-grid__row--leaf[data-row-id='crew-manifest@1.0.0']");
            Assert.Equal("—", crew.QuerySelector("div[role='gridcell'][data-column-id='title']")!.TextContent.Trim());
        });
    }

    [Fact]
    public void Detail_renders_the_Forms_pillar_name_as_the_page_h1()
    {
        var cut = RenderPage(new FixtureFormsAdminClient());
        ClickDefinition(cut, "incident-intake");

        cut.WaitForAssertion(() =>
        {
            var panel = cut.Find("aside[data-detail-panel]");
            Assert.Equal("Forms", cut.Find("h1").TextContent.Trim());
            Assert.Equal("Form definition incident-intake", panel.GetAttribute("aria-label"));
            AssertDefinitionValue(panel, "Published version", "1.0.3");
            AssertDefinitionValue(panel, "Cascade layer", "Tenant");
            AssertDefinitionValue(panel, "Owner", "user:fixture-admin");
            AssertDefinitionValue(panel, "Syncs to peers", "Yes");
            AssertDefinitionValue(panel, "Safe for staging", "No");

            var rows = panel.QuerySelectorAll("table.hl-table tbody tr");
            Assert.Equal(4, rows.Length);
            var draft = Assert.Single(rows, row => row.QuerySelector("td")!.TextContent.Trim() == "1.0.2");
            var cells = draft.QuerySelectorAll("td");
            Assert.Equal("Draft", cells[1].TextContent.Trim());
            Assert.Equal("1.0.0", cells[4].TextContent.Trim());
        });
    }

    [Fact]
    public void Restore_confirms_once_refreshes_history_and_reports_the_draft()
    {
        var spy = new SpyFormsAdminClient();
        var cut = RenderPage(spy);
        ClickDefinition(cut, "incident-intake");
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("table.hl-table tbody tr").Count));

        FindVersionRow(cut, "1.0.1").QuerySelector("button.happ-link")!.Click();
        cut.Find("button.hl-confirm-dialog__confirm").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, spy.RestoreCalls);
            Assert.Equal(("incident-intake", "1.0.1"), spy.LastRestore);
            Assert.True(spy.ListVersionsCalls >= 2);
            Assert.Equal(5, cut.FindAll("table.hl-table tbody tr").Count);
            var draft = FindVersionRow(cut, "1.0.4").QuerySelectorAll("td");
            Assert.Equal("Draft", draft[1].TextContent.Trim());
            Assert.Equal("Draft 1.0.4 created from 1.0.1.", cut.Find("[data-testid='restore-status']").TextContent.Trim());
        });
    }

    [Fact]
    public void Restore_cancel_does_not_call_the_client_or_show_status()
    {
        var spy = new SpyFormsAdminClient();
        var cut = RenderPage(spy);
        ClickDefinition(cut, "incident-intake");
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("table.hl-table tbody tr").Count));

        FindVersionRow(cut, "1.0.1").QuerySelector("button.happ-link")!.Click();
        cut.Find("button.hl-confirm-dialog__cancel").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(0, spy.RestoreCalls);
            Assert.Empty(cut.FindAll("[data-testid='restore-status']"));
        });
    }

    private IRenderedComponent<FormsAdminPage> RenderPage(IFormsAdminClient client)
    {
        Services.AddSingleton(client);
        Services.AddHarborlineUiAdapters();
        // Registered after the adapters so it wins resolution: the real MediaQueryObserver needs the
        // browser's media-query JS module, which the test renderer does not load. The platform's own
        // component tests stub the observer the same way.
        Services.AddSingleton<IMediaQueryObserver>(new StubMediaQueryObserver());
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<FormsAdminPage>(parameters =>
            parameters.Add(page => page.PanelRailCapable, true));
    }

    private static void ClickDefinition(IRenderedComponent<FormsAdminPage> cut, string formId)
    {
        var button = Assert.Single(
            cut.FindAll("div[role='gridcell'][data-column-id='key'] button.happ-link"),
            candidate => candidate.TextContent.Trim() == formId);
        button.Click();
    }

    private static AngleSharp.Dom.IElement FindVersionRow(
        IRenderedComponent<FormsAdminPage> cut,
        string version) =>
        Assert.Single(
            cut.FindAll("table.hl-table tbody tr"),
            row => row.QuerySelector("td")!.TextContent.Trim() == version);

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

    private sealed class SpyFormsAdminClient : IFormsAdminClient
    {
        private readonly FixtureFormsAdminClient inner = new();

        public int ListVersionsCalls { get; private set; }

        public int RestoreCalls { get; private set; }

        public (string FormId, string Version)? LastRestore { get; private set; }

        public Task<IReadOnlyList<FormDefinitionSummary>> ListDefinitionsAsync(CancellationToken ct = default) =>
            inner.ListDefinitionsAsync(ct);

        public Task<IReadOnlyList<FormVersionSummary>> ListVersionsAsync(
            string formId,
            CancellationToken ct = default)
        {
            ListVersionsCalls++;
            return inner.ListVersionsAsync(formId, ct);
        }

        public Task<RestoreResult> RestoreVersionAsync(
            string formId,
            string version,
            CancellationToken ct = default)
        {
            RestoreCalls++;
            LastRestore = (formId, version);
            return inner.RestoreVersionAsync(formId, version, ct);
        }
    }
}
