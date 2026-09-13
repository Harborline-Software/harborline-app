using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost.Workshop;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Browser;
using Harborline.UIAdapters.Blazor.Components.DataDisplay;
using Microsoft.Extensions.DependencyInjection;

namespace Harborline.App.Blazor.Tests;

public sealed class SeededListPageTests : BunitContext
{
    [Fact]
    public void Forms_uses_the_seeded_compiled_view_and_forwards_its_row_action()
    {
        Services.AddSingleton<IWorkshopCatalogueClient>(new FixtureWorkshopCatalogueClient());
        Services.AddHarborlineUiAdapters();
        Services.AddSingleton<IMediaQueryObserver>(new StubMediaQueryObserver());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<SeededListPage>(parameters => parameters.Add(page => page.ItemId, "forms"));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["Key", "Title", "Version", "Cascade layer"], cut.FindAll("[role=columnheader]").Select(cell => cell.TextContent.Trim()));
            var row = cut.Find("[data-row-id='inspection@1.0.0']");
            Assert.Equal("inspection", row.QuerySelector("[data-column-id=formId]")!.TextContent.Trim());
            Assert.Equal("Inspection", row.QuerySelector("[data-column-id=title]")!.TextContent.Trim());
            Assert.Contains("platform.list.forms", cut.Find("[data-definition-source]").GetAttribute("data-definition-source"), StringComparison.Ordinal);
        });

        cut.Find("[data-row-id='inspection@1.0.0']").DoubleClick();
        cut.WaitForAssertion(() => Assert.Equal("Inspection", cut.Find("aside[aria-label='Definition inspector'] h2").TextContent.Trim()));
    }

    private sealed class FixtureWorkshopCatalogueClient : IWorkshopCatalogueClient
    {
        private static readonly ViewRenderPlan Plan = new("sha256:test", "platform.list.forms", "1.0.0", "harborline.platform", "1.0.0", "ViewDefinition",
            new ViewRenderPlanBindings("views.entity-list/grid", new ViewRenderPlanParameters([
                new("formId", "Key"), new("title", "Title"), new("version", "Version"), new("cascadeLayer", "Cascade layer")])));
        private static readonly JsonElement Body = JsonElement.Parse("""{"cascadeLayer":"Pack"}""");
        private static readonly WorkshopCatalogueEntry Entry = new("inspection", "1.0.0", "Active", new WorkshopLocalizedText("en", new Dictionary<string, string> { ["en"] = "Inspection" }), Body, null);
        public Task<WorkshopCatalogueEntry> ReadViewAsync(string itemId, CancellationToken cancellationToken = default) => Task.FromResult(Entry with { Id = $"platform.list.{itemId}", RenderPlan = Plan });
        public Task<IReadOnlyList<WorkshopCatalogueEntry>> ListAsync(string kind, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkshopCatalogueEntry>>([Entry]);
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
