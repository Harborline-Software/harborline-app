using Harborline.App.Blazor.ReferenceHost;

namespace Harborline.App.Blazor.Tests;

public sealed class PackActionProductionReachabilityTests
{
    [Fact]
    public void Legacy_holder_component_is_absent_from_the_shipping_assembly_and_both_shell_route_graphs()
    {
        Assert.DoesNotContain(typeof(Shell).Assembly.GetTypes(), type => type.Name.Contains("AccessHoldersPage", StringComparison.Ordinal));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Harborline.App.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        foreach (var file in new[] { "apps/react/src/App.tsx", "apps/blazor/Shell.razor" })
        {
            var source = File.ReadAllText(Path.Combine(root.FullName, file));
            Assert.DoesNotContain("AccessHoldersPage", source, StringComparison.Ordinal);
            Assert.Contains("PackActionHost", source, StringComparison.Ordinal);
        }
        Assert.False(File.Exists(Path.Combine(root.FullName, "apps/react/src/admin/authorization/AccessHoldersPage.tsx")));
        Assert.False(File.Exists(Path.Combine(root.FullName, "apps/blazor/Admin/Authorization/AccessHoldersPage.razor")));
    }
}
