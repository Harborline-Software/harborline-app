using Harborline.App.Abstractions;
using Harborline.App.Testing;

namespace Harborline.App.Tests;

public sealed class AppComposerTests
{
    [Fact]
    public async Task Composes_extension_capabilities_and_navigation()
    {
        var extension = new StaticHarborlineExtension(new(
            "sample.field-operations",
            "Sample Field Operations",
            HarborlineAppRevisions.ProductInterface,
            [new("sample.inspection", "1")],
            [new("sample.inspection.start", "Start inspection", "/inspections/new", "Field work")]));

        var result = await new HarborlineAppComposer([extension]).ComposeAsync(Session(), HarborlineHostForm.Ios);

        Assert.Single(result.Capabilities);
        Assert.Single(result.Navigation);
        Assert.Equal(HarborlineAppRevisions.ProductInterface, result.ProductInterfaceRevision);
    }

    [Fact]
    public async Task Refuses_incompatible_interface_revision()
    {
        var extension = new StaticHarborlineExtension(new(
            "legacy.extension",
            "Legacy extension",
            "happ.preview.0",
            [],
            []));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new HarborlineAppComposer([extension]).ComposeAsync(Session(), HarborlineHostForm.Browser));

        Assert.Equal(
            "Extensions target an incompatible Harborline App interface: legacy.extension",
            error.Message);
    }

    [Fact]
    public async Task Refuses_conflicting_capability_revisions()
    {
        var extensions = new[] { ("one", "1"), ("two", "2") }.Select(item =>
            new StaticHarborlineExtension(new(
                item.Item1,
                item.Item1,
                HarborlineAppRevisions.ProductInterface,
                [new("sample.inspection", item.Item2)],
                [])));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new HarborlineAppComposer(extensions).ComposeAsync(Session(), HarborlineHostForm.Browser));

        Assert.Equal(
            "Extensions disagree on capability revisions: sample.inspection",
            error.Message);
    }

    [Fact]
    public async Task Refuses_duplicate_routes()
    {
        var extensions = new[] { "one", "two" }.Select(id => new StaticHarborlineExtension(new(
            id, id, HarborlineAppRevisions.ProductInterface, [],
            [new($"{id}.route", id, "/duplicate", "Test")])));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new HarborlineAppComposer(extensions).ComposeAsync(Session(), HarborlineHostForm.Browser));

        Assert.Equal("Extensions contribute duplicate routes: /duplicate", error.Message);
    }

    private static HarborlineUserSessionSnapshot Session() =>
        new("user", "User", "user@example.test", "tenant", new HashSet<string>(), true);
}
