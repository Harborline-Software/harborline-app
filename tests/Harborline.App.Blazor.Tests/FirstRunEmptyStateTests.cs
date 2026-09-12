using Harborline.App.Blazor.ReferenceHost.Admin;

namespace Harborline.App.Blazor.Tests;

public sealed class FirstRunEmptyStateTests
{
    [Theory]
    [InlineData(false, false, false, "First-run state 1")]
    [InlineData(true, false, false, "First-run state 2")]
    [InlineData(true, true, false, "First-run state 3")]
    [InlineData(true, false, true, "First-run state 4")]
    [InlineData(true, true, true, "First-run state 5")]
    public void Each_first_run_state_has_its_own_empty_copy(
        bool installed,
        bool authored,
        bool permittingGrant,
        string state)
    {
        var copy = FirstRunEmptyState.Copy(new(installed, authored, permittingGrant));

        Assert.StartsWith(state, copy, StringComparison.Ordinal);
    }

    [Fact]
    public void Installed_authored_and_permitting_grant_causes_are_distinct_sentences()
    {
        var copies = new[]
        {
            FirstRunEmptyState.Copy(new(false, false, false)),
            FirstRunEmptyState.Copy(new(true, false, false)),
            FirstRunEmptyState.Copy(new(true, false, true)),
        };

        Assert.Equal(3, copies.Distinct(StringComparer.Ordinal).Count());
    }
}
