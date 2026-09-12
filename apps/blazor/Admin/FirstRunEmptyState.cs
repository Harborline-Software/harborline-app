namespace Harborline.App.Blazor.ReferenceHost.Admin;

/// <summary>Derives the five honest first-run states from facts the existing list routes expose.</summary>
public readonly record struct FirstRunObservation(
    bool HasInstalledContent,
    bool HasAuthoredContent,
    bool ViewerHasPermittingGrant);

public static class FirstRunEmptyState
{
    public static string Copy(FirstRunObservation observation) => observation switch
    {
        { HasInstalledContent: false } => "First-run state 1: nothing is installed yet. Use the installer to add the platform package.",
        { HasAuthoredContent: false, ViewerHasPermittingGrant: false } => "First-run state 2: you hold no grant that permits this list.",
        { HasAuthoredContent: true, ViewerHasPermittingGrant: false } => "First-run state 3: access surfaces are preloaded, but you hold no grant that permits this list.",
        { HasAuthoredContent: false } => "First-run state 4: nothing is authored yet. You can author the first definition.",
        _ => "First-run state 5: a domain package is installed, but this list has no matching definitions yet.",
    };

    public static FirstRunObservation FromList(int rowCount) => new(true, rowCount > 0, true);

    public static bool TryFromListFailure(int status, out FirstRunObservation observation)
    {
        observation = status switch
        {
            404 => new(false, false, false),
            403 => new(true, false, false),
            _ => default,
        };
        return status is 403 or 404;
    }
}
