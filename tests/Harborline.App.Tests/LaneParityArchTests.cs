using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Harborline.App.Tests;

/// <summary>
/// Protects structural parity between the two application lanes and enforces consumer-neutral naming.
/// </summary>
public sealed class LaneParityArchTests
{
    private static readonly Regex ReactItemPattern = new(
        @"\{\s*\bid\s*:\s*(?<quote>['""])(?<id>.*?)\k<quote>\s*,\s*\blabel\s*:\s*(?<labelQuote>['""])(?<label>.*?)\k<labelQuote>\s*,?\s*\}",
        RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex BlazorItemPattern = new(
        @"\bnew\s+ShellNavItem\s*\(\s*(?<quote>['""])(?<id>.*?)\k<quote>\s*,\s*(?<labelQuote>['""])(?<label>.*?)\k<labelQuote>\s*\)",
        RegexOptions.CultureInvariant | RegexOptions.Singleline);

    /// <summary>
    /// Verifies that both application lanes expose the same fully qualified navigation entries.
    /// </summary>
    [Fact]
    public void Composed_navigation_is_identical_across_lanes()
    {
        var root = LaneSourceScanner.LocateHostSourceRoot();
        var reactPath = Path.Combine(root, "apps", "react", "src", "App.tsx");
        var blazorPath = Path.Combine(root, "apps", "blazor", "Shell.razor");
        var reactEntries = ExtractReactNavigation(File.ReadAllText(reactPath));
        var blazorEntries = ExtractBlazorNavigation(File.ReadAllText(blazorPath));

        Assert.True(reactEntries.Count > 0,
            $"parser extracted 0 nav items from {reactPath} — the tolerant regex no longer matches");
        Assert.True(blazorEntries.Count > 0,
            $"parser extracted 0 nav items from {blazorPath} — the tolerant regex no longer matches");

        var missingFromBlazor = reactEntries.Except(blazorEntries).OrderBy(FormatEntry).ToArray();
        var missingFromReact = blazorEntries.Except(reactEntries).OrderBy(FormatEntry).ToArray();
        var message = new StringBuilder("Lane navigation differs.")
            .AppendLine()
            .AppendLine("Present in React but missing in Blazor:")
            .AppendLine(FormatEntries(missingFromBlazor))
            .AppendLine("Present in Blazor but missing in React:")
            .Append(FormatEntries(missingFromReact))
            .ToString();

        Assert.True(missingFromBlazor.Length == 0 && missingFromReact.Length == 0, message);
    }

    /// <summary>
    /// Keeps the Settings leaf in the same position and pairs its System framing with the Authorization body in both lanes.
    /// </summary>
    [Fact]
    public void Settings_system_navigation_framing_and_authorization_body_are_identical_across_lanes()
    {
        var root = LaneSourceScanner.LocateHostSourceRoot();
        var react = File.ReadAllText(Path.Combine(root, "apps", "react", "src", "App.tsx"));
        var blazor = File.ReadAllText(Path.Combine(root, "apps", "blazor", "Shell.razor"));

        var reactPage = File.ReadAllText(Path.Combine(root, "apps", "react", "src", "admin", "authorization", "AuthorizationAdminPage.tsx"));
        var blazorPage = File.ReadAllText(Path.Combine(root, "apps", "blazor", "Admin", "Authorization", "AuthorizationAdminPage.razor"));

        AssertOrder(react, "{ id: 'admin-scheduling', label: 'Scheduling' }", "{ id: 'admin-authorization', label: 'Settings' }");
        AssertOrder(blazor, "new ShellNavItem(\"admin-scheduling\", \"Scheduling\")", "new ShellNavItem(\"admin-authorization\", \"Settings\")");
        Assert.Matches(@"activeItemId\s*===\s*'admin-authorization'[\s\S]*?<AuthorizationAdminPage\s*/>", react);
        Assert.Matches("activeItemId\\s*==\\s*\"admin-authorization\"[\\s\\S]*?<AuthorizationAdminPage\\s*/>", blazor);
        Assert.Contains("title: 'Settings › System'", react, StringComparison.Ordinal);
        Assert.Contains("Harborline / Portfolio / {body.title}", react, StringComparison.Ordinal);
        Assert.Contains("\"admin-authorization\" => \"Settings › System\"", blazor, StringComparison.Ordinal);
        Assert.Contains("Harborline / Portfolio / @ActiveLabel", blazor, StringComparison.Ordinal);
        Assert.Contains("<h1>Settings › System</h1>", reactPage, StringComparison.Ordinal);
        Assert.Contains("<h1>Settings › System</h1>", blazorPage, StringComparison.Ordinal);
        Assert.Contains("Authorization capability bindings", reactPage, StringComparison.Ordinal);
        Assert.Contains("Authorization capability bindings", blazorPage, StringComparison.Ordinal);
    }

    private static void AssertOrder(string source, string first, string second)
    {
        var firstIndex = source.IndexOf(first, StringComparison.Ordinal);
        var secondIndex = source.IndexOf(second, StringComparison.Ordinal);
        Assert.True(firstIndex >= 0, $"Could not find '{first}'.");
        Assert.True(secondIndex > firstIndex, $"Expected '{second}' after '{first}'.");
    }

    /// <summary>
    /// Verifies that no git-tracked path or text file contains the consumer name defined by the boundary script.
    /// </summary>
    [Fact]
    public void Tracked_repository_files_are_consumer_neutral()
    {
        var root = LaneSourceScanner.LocateHostSourceRoot();
        var scriptPath = Path.Combine(root, "eng", "verify-boundaries.sh");
        var consumerName = ParseConsumerName(File.ReadAllText(scriptPath));
        var trackedPaths = GetTrackedPaths(root);
        var pathMatches = trackedPaths
            .Where(path => path.Contains(consumerName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var contentMatches = FindContentMatches(root, trackedPaths, consumerName);

        var message = new StringBuilder("Consumer-specific naming is forbidden in Harborline App.");
        foreach (var path in pathMatches)
        {
            message.AppendLine().Append(path);
        }

        foreach (var match in contentMatches)
        {
            message.AppendLine().Append(match.Path).Append(':').Append(match.LineNumber).Append(':').Append(match.Line);
        }

        Assert.True(pathMatches.Length == 0 && contentMatches.Count == 0, message.ToString());
    }

    /// <summary>
    /// Extracts fully qualified navigation leaves from the React workspace composition.
    /// </summary>
    /// <param name="source">The React application source.</param>
    /// <returns>The extracted navigation entries.</returns>
    private static HashSet<NavEntry> ExtractReactNavigation(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(source, @"\bNAV_ITEMS\b[^=]*=\s*\[", '[', ']');
        var entries = new HashSet<NavEntry>();
        Assert.Matches(@"id:\s*'portfolio'[\s\S]*?labelKey:\s*'Portfolio'[\s\S]*?id:\s*'portfolio-group'[\s\S]*?labelKey:\s*'Portfolio'[\s\S]*?itemIds:\s*NAV_ITEMS", source);
        foreach (Match itemMatch in ReactItemPattern.Matches(region))
        {
            entries.Add(new NavEntry(
                "portfolio", "Portfolio", "portfolio-group", "Portfolio",
                itemMatch.Groups["id"].Value, itemMatch.Groups["label"].Value));
        }

        return entries;
    }

    /// <summary>
    /// Extracts fully qualified navigation leaves from the Blazor workspace composition.
    /// </summary>
    /// <param name="source">The Blazor shell source.</param>
    /// <returns>The extracted navigation entries.</returns>
    private static HashSet<NavEntry> ExtractBlazorNavigation(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(source, @"\bNavigationItems\b\s*=\s*\[", '[', ']');
        var entries = new HashSet<NavEntry>();
        Assert.Matches(@"new\(""portfolio"", ""Portfolio"", Groups: \[new\(""portfolio-group"", ""Portfolio"", NavigationItems", source);
        foreach (Match itemMatch in BlazorItemPattern.Matches(region))
        {
            entries.Add(new NavEntry(
                "portfolio", "Portfolio", "portfolio-group", "Portfolio",
                itemMatch.Groups["id"].Value, itemMatch.Groups["label"].Value));
        }

        return entries;
    }

    /// <summary>
    /// Parses and concatenates the quoted segments in the boundary script's consumer-name assignment.
    /// </summary>
    /// <param name="script">The boundary script text.</param>
    /// <returns>The assembled consumer name.</returns>
    private static string ParseConsumerName(string script)
    {
        var assignment = Regex.Match(script, @"(?m)^\s*consumer_name\s*=\s*(?<value>[^\r\n#]+)");
        Assert.True(assignment.Success, "Could not find consumer_name assignment in eng/verify-boundaries.sh.");
        var segments = Regex.Matches(assignment.Groups["value"].Value, @"(['""])(?<segment>.*?)\1")
            .Select(match => match.Groups["segment"].Value)
            .ToArray();
        Assert.True(segments.Length > 0, "The consumer_name assignment contains no quoted segments.");

        // DECODE the printf hex escapes. The script builds the name from character codes so that no
        // readable copy of it sits anywhere in the tree - including in this guard. Concatenating the
        // quoted segments therefore yields the ESCAPE TEXT, not the name, and searching the tree for
        // that text found exactly one hit: the script's own assignment line. This fence was thus
        // simultaneously RED and VACUOUS - it could never have matched the real name anywhere.
        //
        // It regressed silently. When this test landed the script spelled the name as a quoted
        // concatenation, which string.Concat decoded correctly; the later change that replaced that
        // with printf escapes kept the shell working (printf decodes) and broke only the C# port.
        // Decoding here works for BOTH spellings: a segment with no escapes passes through unchanged.
        const string hexEscape = @"\\x(?<hex>[0-9A-Fa-f]{2})";
        return Regex.Replace(
            string.Concat(segments),
            hexEscape,
            match => ((char)Convert.ToInt32(match.Groups["hex"].Value, 16)).ToString());
    }

    /// <summary>
    /// Runs git to obtain the exact tracked-file scope used by the boundary script.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <returns>Tracked paths relative to the repository root.</returns>
    private static string[] GetTrackedPaths(string root)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git", "ls-files")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        Assert.True(process.Start(), "Failed to start 'git ls-files'.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0,
            $"'git ls-files' exited with code {process.ExitCode}: {standardError}");
        return standardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Finds case-insensitive text hits in tracked non-binary readable files.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="trackedPaths">Tracked paths relative to the root.</param>
    /// <param name="consumerName">The dynamically parsed name to locate.</param>
    /// <returns>Every matching line with its path and one-based line number.</returns>
    private static List<ContentMatch> FindContentMatches(string root, IEnumerable<string> trackedPaths, string consumerName)
    {
        var matches = new List<ContentMatch>();
        foreach (var path in trackedPaths)
        {
            string text;
            try
            {
                text = File.ReadAllText(Path.Combine(root, path));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (text.Contains('\0'))
            {
                continue;
            }

            var lines = text.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].Contains(consumerName, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(new ContentMatch(path, index + 1, lines[index].TrimEnd('\r')));
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// Formats a navigation entry with every id and label visible in assertion output.
    /// </summary>
    /// <param name="entry">The entry to format.</param>
    /// <returns>A stable path-like representation.</returns>
    private static string FormatEntry(NavEntry entry) =>
        $"workspace '{entry.WorkspaceId}' ('{entry.WorkspaceLabel}') / group '{entry.GroupId}' ('{entry.GroupLabel}') / item '{entry.ItemId}' ('{entry.ItemLabel}')";

    /// <summary>
    /// Formats navigation entries as assertion-message lines.
    /// </summary>
    /// <param name="entries">The entries to format.</param>
    /// <returns>Formatted entries, or a marker indicating there are none.</returns>
    private static string FormatEntries(IReadOnlyCollection<NavEntry> entries) =>
        entries.Count == 0 ? "  (none)" : string.Join(Environment.NewLine, entries.Select(entry => $"  {FormatEntry(entry)}"));

    /// <summary>
    /// Represents one navigation leaf together with its workspace and group ancestry.
    /// </summary>
    /// <param name="WorkspaceId">The workspace identifier.</param>
    /// <param name="WorkspaceLabel">The workspace label.</param>
    /// <param name="GroupId">The group identifier.</param>
    /// <param name="GroupLabel">The group label.</param>
    /// <param name="ItemId">The item identifier.</param>
    /// <param name="ItemLabel">The item label.</param>
    private sealed record NavEntry(
        string WorkspaceId,
        string WorkspaceLabel,
        string GroupId,
        string GroupLabel,
        string ItemId,
        string ItemLabel);

    /// <summary>
    /// Represents a boundary content hit in git-grep-compatible path, line, and text form.
    /// </summary>
    /// <param name="Path">The tracked path.</param>
    /// <param name="LineNumber">The one-based matching line number.</param>
    /// <param name="Line">The matching line text.</param>
    private sealed record ContentMatch(string Path, int LineNumber, string Line);
}
