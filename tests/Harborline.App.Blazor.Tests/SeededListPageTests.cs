using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost.Workshop;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;
using Harborline.UIAdapters.Blazor.Components.DataDisplay;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Harborline.App.Blazor.Tests;

public sealed class SeededListPageTests : BunitContext
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delayed_action_disables_declared_buttons_without_double_dispatch_and_restores_them(bool refuse)
    {
        var handler = new WorkshopWorkflowTests.WorkflowHandler
        {
            FormRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            RefuseForm = refuse,
        };
        Services.AddSingleton<IWorkshopCatalogueClient>(WorkshopWorkflowTests.Client(handler));
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new StubMediaQueryObserver());
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms"));
        const string formPath = "/api/local-node/catalogue/definitions/FormDefinition/declared.author";
        cut.WaitForAssertion(() => Assert.Equal(7, cut.FindAll(".hl-view-runtime__actions button").Count));
        var pending = cut.Find(".hl-view-runtime__actions button").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.All(cut.FindAll(".hl-view-runtime__actions button"), button =>
        {
            Assert.True(button.HasAttribute("disabled"));
            Assert.Equal("true", button.GetAttribute("aria-disabled"));
        }));
        cut.Find(".hl-view-runtime__actions button").Click();
        Assert.Single(handler.Requests, request => request.Path == formPath);
        handler.FormRelease.SetResult();
        await pending;
        cut.WaitForAssertion(() => Assert.All(cut.FindAll(".hl-view-runtime__actions button"), button => Assert.False(button.HasAttribute("disabled"))));
        if (refuse) Assert.Contains("Form temporarily unavailable", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal);
        else Assert.Single(cut.FindAll("textarea"));
        handler.RefuseForm = false;
        cut.Find(".hl-view-runtime__actions button").Click();
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Requests.Count(request => request.Path == formPath)));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("textarea")));
        Assert.All(cut.FindAll(".hl-view-runtime__actions button"), button => Assert.False(button.HasAttribute("disabled")));
    }

    [Fact]
    public void Unsupported_plan_is_inert_including_host_workflow_controls()
    {
        Services.AddSingleton<IWorkshopCatalogueClient>(new FixtureWorkshopCatalogueClient { Unsupported = true });
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms"));
        cut.WaitForAssertion(() => Assert.Equal(string.Empty, cut.Markup.Trim()));
        Assert.Empty(cut.FindAll("button,form,a,aside"));
    }

    [Fact]
    public void Declared_buttons_and_compiled_forms_complete_the_authenticated_pack_record_and_trace_workflow()
    {
        var handler = new WorkshopWorkflowTests.WorkflowHandler();
        Services.AddSingleton<IWorkshopCatalogueClient>(WorkshopWorkflowTests.Client(handler));
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new StubMediaQueryObserver());
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms"));

        void Action(string label) => cut.FindAll("button[type=button]").Single(button => button.TextContent.Trim() == label).Click();
        cut.WaitForAssertion(() => Assert.Equal(7, cut.FindAll("button[type=button]").Count));
        Action("Check this draft");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("textarea")));
        cut.Find("textarea").Input(WorkshopWorkflowTests.PackJson);
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.Contains("\"valid\": true", cut.Find("pre").TextContent, StringComparison.Ordinal));
        Action("Download this artifact");
        cut.WaitForAssertion(() => Assert.Equal("data:application/octet-stream;base64,AQIDBA==", cut.Find("a[download]").GetAttribute("href")));
        Action("Check signature");
        Action("Add artifact");
        Action("Use artifact");
        Action("Capture entry");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("textarea")));
        Assert.Empty(cut.FindAll("pre"));
        cut.Find("input[type=text]").Input("User supplied value");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.Contains("record-1", cut.Find("pre").TextContent, StringComparison.Ordinal));
        Action("Inspect entry");
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("User supplied value", cut.Find("pre").TextContent, StringComparison.Ordinal);
            Assert.Contains("verdict:allowed", cut.Find("pre").TextContent, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll("section[role=alert]"));
        });
    }

    [Fact]
    public void Selection_restoration_does_not_activate_but_explicit_inspection_of_the_same_row_does()
    {
        Services.AddSingleton<IWorkshopCatalogueClient>(new FixtureWorkshopCatalogueClient());
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new StubMediaQueryObserver());
        JSInterop.Mode = JSRuntimeMode.Loose;

        ViewRuntimeRow? activated = null;
        ViewRuntimeRow? restored = null;
        var cut = Render<SeededListPage>(parameters => parameters
            .Add(page => page.ItemId, "forms")
            .Add(page => page.SelectedRowId, "inspection@1.0.0")
            .Add(page => page.SelectionRestored, EventCallback.Factory.Create<ViewRuntimeRow>(this, row => restored = row))
            .Add(page => page.RowActivated, EventCallback.Factory.Create<ViewRuntimeRow>(this, row => activated = row)));

        cut.WaitForAssertion(() =>
        {
            // The fieldset's default min-content floor prevents the browser grid from shrinking.
            var fieldset = cut.Find("[role=grid]").Closest("fieldset")!;
            Assert.Contains("min-inline-size:0", fieldset.GetAttribute("style"), StringComparison.Ordinal);
            Assert.False(fieldset.HasAttribute("disabled"));
            Assert.Equal(["Key", "Title", "Version", "Cascade layer"], cut.FindAll("[role=columnheader]").Select(cell => cell.TextContent.Trim()));
            var row = cut.Find("[data-row-id='inspection@1.0.0']");
            Assert.Equal("inspection", row.QuerySelector("[data-column-id=formId]")!.TextContent.Trim());
            Assert.Equal("Inspection", row.QuerySelector("[data-column-id=title]")!.TextContent.Trim());
            Assert.Contains("platform.list.forms", cut.Find("[data-definition-source]").GetAttribute("data-definition-source"), StringComparison.Ordinal);
        });

        cut.WaitForAssertion(() => Assert.Equal("inspection@1.0.0", restored!.Id));
        Assert.Null(activated);
        cut.Find("[data-row-id='inspection@1.0.0']").DoubleClick();
        cut.WaitForAssertion(() => Assert.Equal("inspection@1.0.0", activated!.Id));
        Assert.Empty(cut.FindAll("aside[aria-label='Definition inspector']"));
    }

    private sealed class FixtureWorkshopCatalogueClient : IWorkshopCatalogueClient
    {
        public bool Unsupported { get; init; }
        private static readonly ViewRenderPlan Plan = new("sha256:test", "platform.list.forms", "1.0.0", "harborline.platform", "1.0.0", "ViewDefinition",
            new ViewRenderPlanBindings("views.entity-list/grid", new ViewRenderPlanParameters([
                new("formId", "Key"), new("title", "Title"), new("version", "Version"), new("cascadeLayer", "Cascade layer")])));
        private static readonly JsonElement Body = JsonElement.Parse("""{"cascadeLayer":"Pack"}""");
        private static readonly WorkshopCatalogueEntry Entry = new("inspection", "1.0.0", "Active", new WorkshopLocalizedText("en", new Dictionary<string, string> { ["en"] = "Inspection" }), Body, null);
        public Task<WorkshopCatalogueEntry> ReadViewAsync(string viewId, CancellationToken cancellationToken = default) => Task.FromResult(Entry with
        {
            Id = viewId, RenderPlan = Unsupported ? Plan with { DefinitionKind = "UnknownDefinition" } : Plan with { DefinitionId = viewId },
        });
        public Task<WorkshopCatalogueList> ListAsync(string kind, CancellationToken cancellationToken = default) => Task.FromResult(new WorkshopCatalogueList([Entry], []));
        public Task<WorkshopCatalogueEntry> ReadFormAsync(string id, string? version = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JsonElement> ReadJsonAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JsonElement> PostJsonAsync(string path, object body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JsonElement> PostArtifactAsync(string path, byte[] artifact, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportAsync(JsonElement candidate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubMediaQueryObserver : IMediaQueryObserver
    {
        public ValueTask<IMediaQuerySubscription> ObserveAsync(string query, Func<MediaQueryChange, ValueTask> onChanged, CancellationToken cancellationToken = default) => new(new StubSubscription(query));
        private sealed class StubSubscription(string query) : IMediaQuerySubscription
        {
            public string Query { get; } = query;
            public bool Matches => true;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
