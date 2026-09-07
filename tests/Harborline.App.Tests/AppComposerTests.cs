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
    public async Task Rejects_duplicate_routes()
    {
        var extensions = new[] { "one", "two" }.Select(id => new StaticHarborlineExtension(new(
            id, id, HarborlineAppRevisions.ProductInterface, [],
            [new($"{id}.route", id, "/duplicate", "Test")])));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new HarborlineAppComposer(extensions).ComposeAsync(Session(), HarborlineHostForm.Browser));
    }

    private static HarborlineUserSessionSnapshot Session() =>
        new("user", "User", "user@example.test", "tenant", new HashSet<string>(), true);
}
