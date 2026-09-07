namespace Harborline.App.Abstractions;

public static class HarborlineAppRevisions
{
    public const string ProductInterface = "happ.preview.1";
    public const string AppEssentialCapabilities = "app-essential.preview.1";
}

public enum HarborlineHostForm
{
    Browser,
    Desktop,
    Ios,
    Android,
}

public sealed record HarborlineCapability(string Id, string Revision, bool Available = true);

public sealed record HarborlineNavigationItem(
    string Id,
    string Label,
    string Route,
    string Group,
    int Order = 0);

public sealed record HarborlineUserSessionSnapshot(
    string UserId,
    string DisplayName,
    string? Email,
    string TenantId,
    IReadOnlySet<string> Permissions,
    bool IsAuthenticated);

public interface IHarborlineUserSession
{
    event EventHandler? Changed;
    ValueTask<HarborlineUserSessionSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

public sealed record HarborlineExtensionContribution(
    string ExtensionId,
    string DisplayName,
    string ProductInterfaceRevision,
    IReadOnlyList<HarborlineCapability> Capabilities,
    IReadOnlyList<HarborlineNavigationItem> Navigation);

public interface IHarborlineAppExtension
{
    ValueTask<HarborlineExtensionContribution> ContributeAsync(
        HarborlineUserSessionSnapshot session,
        HarborlineHostForm host,
        CancellationToken cancellationToken = default);
}

public sealed record HarborlineAppDefinition(
    string ProductInterfaceRevision,
    string CapabilityManifestRevision,
    IReadOnlyList<HarborlineCapability> Capabilities,
    IReadOnlyList<HarborlineNavigationItem> Navigation);

public sealed class HarborlineAppComposer(IEnumerable<IHarborlineAppExtension> extensions)
{
    private readonly IReadOnlyList<IHarborlineAppExtension> _extensions = extensions.ToArray();

    public async ValueTask<HarborlineAppDefinition> ComposeAsync(
        HarborlineUserSessionSnapshot session,
        HarborlineHostForm host,
        CancellationToken cancellationToken = default)
    {
        var contributions = new List<HarborlineExtensionContribution>(_extensions.Count);
        foreach (var extension in _extensions)
            contributions.Add(await extension.ContributeAsync(session, host, cancellationToken));

        var incompatible = contributions
            .Where(item => item.ProductInterfaceRevision != HarborlineAppRevisions.ProductInterface)
            .Select(item => item.ExtensionId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (incompatible.Length > 0)
            throw new InvalidOperationException($"Extensions target an incompatible Harborline App interface: {string.Join(", ", incompatible)}");

        var duplicateCapabilities = contributions.SelectMany(item => item.Capabilities)
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Where(group => group.Select(item => item.Revision).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (duplicateCapabilities.Length > 0)
            throw new InvalidOperationException($"Extensions disagree on capability revisions: {string.Join(", ", duplicateCapabilities)}");

        var navigation = contributions.SelectMany(item => item.Navigation).ToArray();
        var duplicateRoutes = navigation.GroupBy(item => item.Route, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (duplicateRoutes.Length > 0)
            throw new InvalidOperationException($"Extensions contribute duplicate routes: {string.Join(", ", duplicateRoutes)}");

        return new HarborlineAppDefinition(
            HarborlineAppRevisions.ProductInterface,
            HarborlineAppRevisions.AppEssentialCapabilities,
            contributions.SelectMany(item => item.Capabilities)
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            navigation.OrderBy(item => item.Group, StringComparer.Ordinal)
                .ThenBy(item => item.Order)
                .ThenBy(item => item.Label, StringComparer.Ordinal)
                .ToArray());
    }
}
