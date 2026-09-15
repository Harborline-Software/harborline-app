using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Harborline.App.Tests;

/// <summary>
/// Freezes the five compiled-inspector roots before retirement and keeps their lane fixtures as
/// test-only positive controls. The baseline is read from Git so the fence remains meaningful after
/// the production files themselves have been removed.
/// </summary>
public sealed class CompiledInspectorRetirementControlsTests
{
    private const string SourcePin = "11e38b663bb7e953bae9596af12bf9788b1edc7e";
    private const string FixtureDirectory = "tests/fixtures/compiled-inspector-controls";

    private static readonly string[] Pillars = ["Forms", "Reports", "Views", "DataExchange", "Scheduling"];
    private static readonly string[] ReactRoots = ["forms", "reports", "views", "data-exchange", "scheduling"];
    private static readonly string[] BlazorRoots = ["Forms", "Reports", "Views", "DataExchange", "Scheduling"];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Regex DeclaredSymbols = new(
        @"\b(?:export|public)\s+(?:sealed\s+|abstract\s+|static\s+)*(?:class|record|interface|type|function|const)\s+(?<name>[A-Z][A-Za-z0-9_]*)",
        RegexOptions.CultureInvariant);

    [Fact]
    public void All_ten_retired_roots_are_absolutely_empty()
    {
        var roots = ReactRoots.Select(root => $"apps/react/src/admin/{root}")
            .Concat(BlazorRoots.Select(root => $"apps/blazor/Admin/{root}"));
        foreach (var root in roots)
        {
            var directory = Path.Combine(RepositoryRoot(), root);
            Assert.True(!Directory.Exists(directory) || !Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any(),
                $"Retired inspector root contains files: {root}");
        }
    }

    [Fact]
    public void Production_has_no_retired_symbols_routes_or_test_control_dependencies()
    {
        var retiredSymbols = ReadBaselinePaths().Where(path => !path.Contains("/__tests__/", StringComparison.Ordinal))
            .SelectMany(path => DeclaredSymbols.Matches(ReadGitText(path)).Select(match => match.Groups["name"].Value))
            .Distinct(StringComparer.Ordinal).ToArray();
        Assert.Contains("FormsAdminClient", retiredSymbols);
        Assert.Contains("FormDefinitionSummary", retiredSymbols);
        Assert.Contains("HttpSchedulingAdminClient", retiredSymbols);
        var retired = new Regex(@"\b(?:" + string.Join('|', retiredSymbols.Select(Regex.Escape))
            + @")\b|admin-(?:forms|reports|views|data-exchange|scheduling)\b", RegexOptions.CultureInvariant);
        var dependency = new Regex(
            @"(?:admin[/\\](?:forms|reports|views|data-exchange|scheduling)[/\\]|Admin[./\\](?:Forms|Reports|Views|DataExchange|Scheduling)\b|compiled-inspector-controls|Harborline\.App\.(?:Tests|Blazor\.Tests))",
            RegexOptions.CultureInvariant);
        foreach (var path in ProductionFiles())
        {
            var source = File.ReadAllText(Path.Combine(RepositoryRoot(), path));
            Assert.False(retired.IsMatch(source), $"Retired inspector symbol or route in {path}: {retired.Match(source).Value}");
            Assert.False(dependency.IsMatch(source), $"Retired inspector or test-only dependency in {path}: {dependency.Match(source).Value}");
        }
    }

    [Fact]
    public void Baseline_diff_adds_no_compiled_administration_file_or_entry_point()
    {
        var baseline = RunGitText("ls-tree", "-r", "--name-only", SourcePin, "--", "apps/react/src/admin", "apps/blazor/Admin")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        var current = ProductionFiles().Where(path => path.StartsWith("apps/react/src/admin/", StringComparison.Ordinal)
            || path.StartsWith("apps/blazor/Admin/", StringComparison.Ordinal)).ToArray();
        Assert.DoesNotContain(current, path => !baseline.Contains(path));
        foreach (var path in current)
        {
            var original = ReadGitText(path);
            var tested = File.ReadAllText(Path.Combine(RepositoryRoot(), path));
            var originalSymbols = DeclaredSymbols.Matches(original).Select(match => match.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);
            Assert.DoesNotContain(DeclaredSymbols.Matches(tested), match => !originalSymbols.Contains(match.Groups["name"].Value));
            Assert.Equal(Regex.Matches(original, @"(?m)^\s*@page\s+.*").Select(match => match.Value.Trim()),
                Regex.Matches(tested, @"(?m)^\s*@page\s+.*").Select(match => match.Value.Trim()));
        }

        var entries = new Regex(@"\b[A-Za-z][A-Za-z0-9]*AdminPage\b", RegexOptions.CultureInvariant);
        var baselineEntries = new[] { "apps/react/src/App.tsx", "apps/blazor/Shell.razor" }
            .SelectMany(path => entries.Matches(ReadGitText(path)).Select(match => match.Value)).ToHashSet(StringComparer.Ordinal);
        foreach (var path in ProductionFiles())
        {
            var addedEntries = entries.Matches(File.ReadAllText(Path.Combine(RepositoryRoot(), path)))
                .Select(match => match.Value).Where(entry => !baselineEntries.Contains(entry));
            Assert.Empty(addedEntries);
        }
    }

    private static IEnumerable<string> ProductionFiles()
    {
        foreach (var root in new[] { "apps/react/src", "apps/blazor", "src", "hosts" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), root), "*", SearchOption.AllDirectories))
            {
                var path = Path.GetRelativePath(RepositoryRoot(), file).Replace('\\', '/');
                if (path.Split('/').Any(part => part is "bin" or "obj" or "node_modules" or ".feed" or "__tests__")
                    || path.Contains(".test.", StringComparison.Ordinal)) continue;
                if (Path.GetExtension(path) is ".cs" or ".razor" or ".ts" or ".tsx" or ".js" or ".mjs" or ".json" or ".csproj")
                    yield return path;
            }
        }
    }

    [Fact]
    public void Retirement_manifest_is_the_exact_hashed_ninety_path_baseline()
    {
        var manifest = ReadJson<RetirementManifest>("retirement.manifest.json");

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal(SourcePin, manifest.SourcePin);
        Assert.Equal("sha256", manifest.Algorithm);
        Assert.Equal(90, manifest.FileCount);
        Assert.Equal(90, manifest.Files.Count);
        Assert.Equal(90, manifest.Files.Select(file => file.Path).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(60, manifest.Files.Count(file => file.Path.StartsWith("apps/react/", StringComparison.Ordinal)));
        Assert.Equal(30, manifest.Files.Count(file => file.Path.StartsWith("apps/blazor/", StringComparison.Ordinal)));

        var expectedPaths = ReadBaselinePaths();
        Assert.Equal(expectedPaths, manifest.Files.Select(file => file.Path).Order(StringComparer.Ordinal));
        foreach (var file in manifest.Files)
        {
            Assert.Matches("^[0-9a-f]{64}$", file.Sha256);
            Assert.Equal(file.Sha256, Sha256(ReadGitBlob(file.Path)));
        }

        var aggregatePayload = string.Concat(manifest.Files.OrderBy(file => file.Path, StringComparer.Ordinal)
            .Select(file => $"{file.Path}\0{file.Sha256}\n"));
        Assert.Equal(manifest.AggregateSha256, Sha256(Encoding.UTF8.GetBytes(aggregatePayload)));
    }

    public static TheoryData<string> CanonicalControls()
    {
        var data = new TheoryData<string>();
        foreach (var pillar in Pillars)
        {
            data.Add(pillar);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CanonicalControls))]
    public void Canonical_control_matches_both_legacy_lane_fixtures(string pillar)
    {
        var fileName = pillar == "DataExchange"
            ? "data-exchange.json"
            : $"{pillar.ToLowerInvariant()}.json";
        var control = ReadJson<CanonicalControl>(fileName);

        Assert.Equal(2, control.SchemaVersion);
        Assert.Equal(SourcePin, control.SourcePin);
        Assert.Equal(pillar, control.Pillar);
        var paths = LegacyFixturePaths(pillar);
        var react = FixtureParityArchTests.ReadCanonicalRows(pillar, ReadGitText(paths.React), react: true);
        var blazor = FixtureParityArchTests.ReadCanonicalRows(pillar, ReadGitText(paths.Blazor), react: false);
        var definitions = control.DefinitionRows.Order(StringComparer.Ordinal).ToArray();
        var versions = control.VersionRows.Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(react.Definitions, definitions);
        Assert.Equal(blazor.Definitions, definitions);
        Assert.Equal(react.Versions, versions);
        Assert.Equal(blazor.Versions, versions);
    }

    [Theory]
    [MemberData(nameof(CanonicalControls))]
    public void Canonical_control_is_self_contained_after_legacy_sources_are_removed(string pillar)
    {
        var fileName = pillar == "DataExchange" ? "data-exchange.json" : $"{pillar.ToLowerInvariant()}.json";
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), FixtureDirectory, fileName)));
        var root = document.RootElement;
        var definitions = root.GetProperty("definitionRows");
        var versions = root.GetProperty("versionRows");
        var entries = root.GetProperty("entries");

        Assert.Equal(definitions.GetArrayLength(), entries.GetArrayLength());
        Assert.True(definitions.GetArrayLength() > 0);
        Assert.True(versions.GetArrayLength() >= definitions.GetArrayLength());
        foreach (var entry in entries.EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("id").GetString()));
            Assert.Equal(JsonValueKind.Object, entry.GetProperty("list").ValueKind);
            Assert.Equal(JsonValueKind.Object, entry.GetProperty("detail").ValueKind);
        }

        var behavior = root.GetProperty("behavior");
        Assert.Equal(JsonValueKind.Array, behavior.GetProperty("actions").ValueKind);
        Assert.Equal(404, behavior.GetProperty("notFound").GetProperty("status").GetInt32());
    }

    private static (string React, string Blazor) LegacyFixturePaths(string pillar) => pillar switch
    {
        "Forms" => ("apps/react/src/admin/forms/client/fixtureClient.ts", "apps/blazor/Admin/Forms/FixtureFormsAdminClient.cs"),
        "Reports" => ("apps/react/src/admin/reports/client/fixtureClient.ts", "apps/blazor/Admin/Reports/FixtureReportsAdminClient.cs"),
        "Views" => ("apps/react/src/admin/views/client/fixtureClient.ts", "apps/blazor/Admin/Views/FixtureViewsAdminClient.cs"),
        "DataExchange" => ("apps/react/src/admin/data-exchange/client/fixtureClient.ts", "apps/blazor/Admin/DataExchange/FixtureDataExchangeAdminClient.cs"),
        "Scheduling" => ("apps/react/src/admin/scheduling/client/fixtureClient.ts", "apps/blazor/Admin/Scheduling/FixtureSchedulingAdminClient.cs"),
        _ => throw new ArgumentOutOfRangeException(nameof(pillar)),
    };

    internal static string ReadGitText(string path) => Encoding.UTF8.GetString(ReadGitBlob(path));

    private static string[] ReadBaselinePaths() => RunGitText(
            "ls-tree", "-r", "--name-only", SourcePin, "--", "apps/react/src/admin", "apps/blazor/Admin")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(IsInspectorPath)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static bool IsInspectorPath(string path) =>
        path.StartsWith("apps/react/src/admin/", StringComparison.Ordinal) &&
        ReactRoots.Any(root => path.StartsWith($"apps/react/src/admin/{root}/", StringComparison.Ordinal)) ||
        path.StartsWith("apps/blazor/Admin/", StringComparison.Ordinal) &&
        BlazorRoots.Any(root => path.StartsWith($"apps/blazor/Admin/{root}/", StringComparison.Ordinal));

    private static byte[] ReadGitBlob(string path)
    {
        using var process = StartGit("show", $"{SourcePin}:{path}");
        using var bytes = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(bytes);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git show failed for '{path}': {error}");
        return bytes.ToArray();
    }

    private static string RunGitText(params string[] arguments)
    {
        using var process = StartGit(arguments);
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {error}");
        return output.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static Process StartGit(params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = RepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return Process.Start(start) ?? throw new InvalidOperationException("Could not start git.");
    }

    private static T ReadJson<T>(string fileName)
    {
        var path = Path.Combine(RepositoryRoot(), FixtureDirectory.Replace('/', Path.DirectorySeparatorChar), fileName);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Control '{fileName}' is empty.");
    }

    private static string RepositoryRoot() => LaneSourceScanner.LocateHostSourceRoot();

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record RetirementManifest(
        int SchemaVersion,
        string SourcePin,
        string Algorithm,
        string AggregateSha256,
        int FileCount,
        IReadOnlyList<ManifestFile> Files);

    private sealed record ManifestFile(string Path, string Sha256);

    private sealed record CanonicalControl(
        int SchemaVersion,
        string SourcePin,
        string Pillar,
        IReadOnlyList<string> DefinitionRows,
        IReadOnlyList<string> VersionRows);
}
