using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Harborline.App.Tests;

/// <summary>
/// Protects the fixture half of the lane-parity rule: the React fixture client is the parity
/// authority for a pillar's admin surface and the Blazor fixture client must carry the same
/// dataset, value for value. Like <see cref="LaneParityArchTests"/> this reads source text, so it
/// costs neither lane a runtime dependency.
/// </summary>
public sealed class FixtureParityArchTests
{
    private static readonly AuthorizationFixturePair AuthorizationPair = new(
        "apps/react/src/admin/authorization/client/fixtureClient.ts",
        "apps/blazor/Admin/Authorization/FixtureAuthorizationAdminClient.cs");

    /// <summary>
    /// The registered fixture pairs. A new pillar joins the fence with one entry here; the pattern
    /// spec makes both file locations and both helper shapes predictable per pillar.
    /// </summary>
    private static readonly FixturePair[] Pairs =
    [
        new(
            "Forms",
            "apps/react/src/admin/forms/client/fixtureClient.ts",
            "apps/blazor/Admin/Forms/FixtureFormsAdminClient.cs",
            source => ParseReactFormVersions(source).Select(FormatVersion).ToArray(),
            source => ParseBlazorFormVersions(source).Select(FormatVersion).ToArray(),
            source => ParseReactFormDefinitions(source).Select(FormatDefinition).ToArray(),
            source => ParseBlazorFormDefinitions(source).Select(FormatDefinition).ToArray()),
        new(
            "Reports",
            "apps/react/src/admin/reports/client/fixtureClient.ts",
            "apps/blazor/Admin/Reports/FixtureReportsAdminClient.cs",
            source => ParseReactReportVersions(source, "reportKind"),
            ParseBlazorReportVersions,
            source => ParseReactReportDefinitions(source, "reportKind"),
            ParseBlazorReportDefinitions),
        new(
            "Views",
            "apps/react/src/admin/views/client/fixtureClient.ts",
            "apps/blazor/Admin/Views/FixtureViewsAdminClient.cs",
            source => ParseReactReportVersions(source, "viewKind"),
            ParseBlazorReportVersions,
            source => ParseReactReportDefinitions(source, "viewKind"),
            ParseBlazorReportDefinitions),
        new(
            "DataExchange",
            "apps/react/src/admin/data-exchange/client/fixtureClient.ts",
            "apps/blazor/Admin/DataExchange/FixtureDataExchangeAdminClient.cs",
            source => ParseReactReportVersions(source, "exchangeKind"),
            ParseBlazorReportVersions,
            source => ParseReactReportDefinitions(source, "exchangeKind"),
            ParseBlazorReportDefinitions),
        new(
            "Scheduling",
            "apps/react/src/admin/scheduling/client/fixtureClient.ts",
            "apps/blazor/Admin/Scheduling/FixtureSchedulingAdminClient.cs",
            ParseReactSchedulingVersions,
            ParseBlazorSchedulingVersions,
            ParseReactSchedulingDefinitions,
            ParseBlazorSchedulingDefinitions),
    ];

    /// <summary>
    /// Supplies the registered pillar names so each pillar reports as its own test case.
    /// </summary>
    /// <returns>One row per registered fixture pair.</returns>
    public static TheoryData<string> RegisteredPillars()
    {
        var pillars = new TheoryData<string>();
        foreach (var pair in Pairs)
        {
            pillars.Add(pair.Pillar);
        }

        return pillars;
    }

    /// <summary>
    /// Verifies that both lanes seed the same version-history rows for a pillar.
    /// </summary>
    /// <param name="pillar">The registered pillar name.</param>
    [Theory]
    [MemberData(nameof(RegisteredPillars))]
    public void Fixture_version_rows_are_identical_across_lanes(string pillar)
    {
        var pair = Pairs.Single(candidate => candidate.Pillar == pillar);
        AssertSetEquality(
            pair,
            "version rows",
            pair.ReactVersions(ReadLane(pair.ReactPath)),
            pair.BlazorVersions(ReadLane(pair.BlazorPath)),
            text => text);
    }

    /// <summary>
    /// Verifies that both lanes seed the same definition summaries for a pillar.
    /// </summary>
    /// <param name="pillar">The registered pillar name.</param>
    [Theory]
    [MemberData(nameof(RegisteredPillars))]
    public void Fixture_definition_summaries_are_identical_across_lanes(string pillar)
    {
        var pair = Pairs.Single(candidate => candidate.Pillar == pillar);
        AssertSetEquality(
            pair,
            "definition summaries",
            pair.ReactDefinitions(ReadLane(pair.ReactPath)),
            pair.BlazorDefinitions(ReadLane(pair.BlazorPath)),
            text => text);
    }

    /// <summary>
    /// Canonicalizes all three Authorization fixture datasets so qualified role identity, empty
    /// bindings, and standing field definitions cannot drift independently between framework lanes.
    /// </summary>
    [Fact]
    public void Authorization_fixture_rows_are_identical_across_lanes()
    {
        var react = ReadLane(AuthorizationPair.ReactPath);
        var blazor = ReadLane(AuthorizationPair.BlazorPath);

        AssertAuthorizationRows("role definitions", ParseReactAuthorizationRoles(react), ParseBlazorAuthorizationRoles(blazor));
        AssertAuthorizationRows("capability/binding rows", ParseReactAuthorizationCapabilities(react), ParseBlazorAuthorizationCapabilities(blazor));
        AssertAuthorizationRows("standing field/type rows", ParseReactStandingRows(react), ParseBlazorStandingRows(blazor));
    }

    private static void AssertAuthorizationRows(string subject, IReadOnlyCollection<string> react, IReadOnlyCollection<string> blazor)
    {
        Assert.NotEmpty(react);
        Assert.NotEmpty(blazor);
        Assert.Equal(react.Order(StringComparer.Ordinal), blazor.Order(StringComparer.Ordinal));
    }

    private static IReadOnlyCollection<string> ParseReactAuthorizationRoles(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(source, @"\bROLE_DEFINITIONS\b[^=]*=\s*\[", '[', ']');
        return Regex.Matches(region,
                @"roleDefinitionId:\s*'(?<id>[^']+)'\s*,\s*role:\s*\{\s*vocabulary:\s*'(?<vocabulary>[^']+)'\s*,\s*name:\s*'(?<name>[^']+)'\s*\}\s*,\s*displayName:\s*'(?<display>[^']+)'\s*,\s*owner:\s*\{\s*kind:\s*'(?<kind>[^']+)'\s*,\s*ownerId:\s*'(?<owner>[^']+)'\s*\}\s*,\s*isSealed:\s*(?<sealed>true|false)",
                RegexOptions.CultureInvariant | RegexOptions.Singleline)
            .Select(match => string.Join('|', match.Groups["id"].Value, match.Groups["vocabulary"].Value,
                match.Groups["name"].Value, match.Groups["display"].Value, match.Groups["kind"].Value,
                match.Groups["owner"].Value, match.Groups["sealed"].Value))
            .ToArray();
    }

    private static IReadOnlyCollection<string> ParseBlazorAuthorizationRoles(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(source, @"\bRoleDefinitions\b\s*=\s*\[", '[', ']');
        return Regex.Matches(region,
                "new\\(Guid\\.Parse\\(\"(?<id>[^\"]+)\"\\),\\s*new\\(\"(?<vocabulary>[^\"]+)\",\\s*\"(?<name>[^\"]+)\"\\),\\s*\"(?<display>[^\"]+)\",\\s*new\\(\"(?<kind>[^\"]+)\",\\s*\"(?<owner>[^\"]+)\"\\),\\s*(?<sealed>true|false)\\)",
                RegexOptions.CultureInvariant | RegexOptions.Singleline)
            .Select(match => string.Join('|', match.Groups["id"].Value, match.Groups["vocabulary"].Value,
                match.Groups["name"].Value, match.Groups["display"].Value, match.Groups["kind"].Value,
                match.Groups["owner"].Value, match.Groups["sealed"].Value))
            .ToArray();
    }

    private static IReadOnlyCollection<string> ParseReactAuthorizationCapabilities(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(source, @"\bCAPABILITY_DEFINITIONS\b[^=]*=\s*\[", '[', ']');
        return Regex.Matches(region,
                @"definitionId:\s*'(?<id>[^']+)'\s*,\s*publisherPackageId:\s*'(?<publisher>[^']+)'\s*,\s*definitionRevision:\s*(?<definitionRevision>\d+)\s*,\s*atom:\s*\{\s*operation:\s*'(?<operation>[^']+)'\s*,\s*scopeType:\s*'(?<scopeType>[^']+)'\s*,\s*scopeValue:\s*'(?<scopeValue>[^']+)'\s*\}\s*,\s*offeredRoles:\s*(?<offered>\[[^\]]*\])\s*,\s*binding:\s*\{\s*revision:\s*(?<bindingRevision>\d+)\s*,\s*effectiveRoles:\s*(?<effective>\[[^\]]*\])\s*,\s*warning:\s*(?<warning>null|'EmptyBinding')",
                RegexOptions.CultureInvariant | RegexOptions.Singleline)
            .Select(match => FormatAuthorizationCapability(match, ParseReactRoleList))
            .ToArray();
    }

    private static IReadOnlyCollection<string> ParseBlazorAuthorizationCapabilities(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(source, @"\bdefinitions\b\s*=\s*\[", '[', ']');
        return Regex.Matches(region,
                "new\\(\\s*Guid\\.Parse\\(\"(?<id>[^\"]+)\"\\),\\s*\"(?<publisher>[^\"]+)\",\\s*(?<definitionRevision>\\d+)\\s*,\\s*new\\(\"(?<operation>[^\"]+)\",\\s*\"(?<scopeType>[^\"]+)\",\\s*\"(?<scopeValue>[^\"]+)\"\\)\\s*,\\s*(?<offered>\\[[^\\]]*\\])\\s*,\\s*new\\((?<bindingRevision>\\d+)\\s*,\\s*(?<effective>\\[[^\\]]*\\])\\s*,\\s*(?<warning>null|BindingWarningCode\\.EmptyBinding)\\)\\)",
                RegexOptions.CultureInvariant | RegexOptions.Singleline)
            .Select(match => FormatAuthorizationCapability(match, ParseBlazorRoleList))
            .ToArray();
    }

    private static string FormatAuthorizationCapability(Match match, Func<string, string> parseRoles) =>
        string.Join('|', match.Groups["id"].Value, match.Groups["publisher"].Value,
            match.Groups["definitionRevision"].Value, match.Groups["operation"].Value,
            match.Groups["scopeType"].Value, match.Groups["scopeValue"].Value,
            parseRoles(match.Groups["offered"].Value), match.Groups["bindingRevision"].Value,
            parseRoles(match.Groups["effective"].Value), NormalizeWarning(match.Groups["warning"].Value));

    private static string ParseReactRoleList(string source) => string.Join(',', Regex.Matches(source,
            @"vocabulary:\s*'(?<vocabulary>[^']+)'\s*,\s*name:\s*'(?<name>[^']+)'",
            RegexOptions.CultureInvariant)
        .Select(match => $"{match.Groups["vocabulary"].Value}/{match.Groups["name"].Value}"));

    private static string ParseBlazorRoleList(string source) => string.Join(',', Regex.Matches(source,
            "new\\(\"(?<vocabulary>sys\\.platform-roles|tax\\.roles)\",\\s*\"(?<name>[^\"]+)\"\\)",
            RegexOptions.CultureInvariant)
        .Select(match => $"{match.Groups["vocabulary"].Value}/{match.Groups["name"].Value}"));

    private static string NormalizeWarning(string warning) => warning.Contains("EmptyBinding", StringComparison.Ordinal) ? "EmptyBinding" : "null";

    private static IReadOnlyCollection<string> ParseReactStandingRows(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(source, @"\bSTANDING_DEFINITIONS\b[^=]*=\s*\[", '[', ']');
        var header = Regex.Match(region, @"ruleId:\s*'(?<rule>[^']+)'\s*,\s*ruleVersion:\s*'(?<version>[^']+)'\s*,\s*standing:\s*'(?<standing>[^']+)'\s*,\s*declaredRecordType:\s*'(?<record>[^']+)'", RegexOptions.CultureInvariant);
        return FormatStandingRows(header, Regex.Matches(region,
            @"field:\s*'(?<field>[^']+)'\s*,\s*carryingRecordTypes:\s*\[(?<types>[^\]]*)\]",
            RegexOptions.CultureInvariant), "'(?<type>[^']+)'");
    }

    private static IReadOnlyCollection<string> ParseBlazorStandingRows(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(source, @"\bStandingDefinitions\b\s*=\s*\[", '[', ']');
        var header = Regex.Match(region,
            "new\\(\\s*\"(?<rule>[^\"]+)\",\\s*\"(?<version>[^\"]+)\",\\s*\"(?<standing>[^\"]+)\",\\s*\"(?<record>[^\"]+)\"",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        return FormatStandingRows(header, Regex.Matches(region,
            "new\\(\"(?<field>[^\"]+)\",\\s*\\[(?<types>[^\\]]*)\\]\\)",
            RegexOptions.CultureInvariant), "\"(?<type>[^\"]+)\"");
    }

    private static IReadOnlyCollection<string> FormatStandingRows(Match header, MatchCollection fields, string typePattern)
    {
        Assert.True(header.Success, "Standing fixture header no longer matches the parity parser.");
        return fields.Select(field => string.Join('|', header.Groups["rule"].Value, header.Groups["version"].Value,
            header.Groups["standing"].Value, header.Groups["record"].Value, field.Groups["field"].Value,
            string.Join(',', Regex.Matches(field.Groups["types"].Value, typePattern, RegexOptions.CultureInvariant)
                .Select(match => match.Groups["type"].Value)))).ToArray();
    }

    /// <summary>
    /// Reads a lane fixture file addressed relative to the repository root.
    /// </summary>
    /// <param name="relativePath">The forward-slash path recorded in the registration table.</param>
    /// <returns>The fixture source text.</returns>
    private static string ReadLane(string relativePath)
    {
        var path = Path.Combine(
            LaneSourceScanner.LocateHostSourceRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Registered fixture '{relativePath}' does not exist at {path}.");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Compares two extracted fixture sets and reports every one-sided row.
    /// </summary>
    /// <typeparam name="T">The extracted row type.</typeparam>
    /// <param name="pair">The registered pair under test.</param>
    /// <param name="subject">The dataset name used in the failure message.</param>
    /// <param name="react">Rows extracted from the React parity authority.</param>
    /// <param name="blazor">Rows extracted from the Blazor lane.</param>
    /// <param name="format">Renders one row for the failure message.</param>
    private static void AssertSetEquality<T>(
        FixturePair pair,
        string subject,
        IReadOnlyCollection<T> react,
        IReadOnlyCollection<T> blazor,
        Func<T, string> format)
    {
        Assert.True(react.Count > 0,
            $"parser extracted 0 {subject} from {pair.ReactPath} — the fixture shape no longer matches the parser");
        Assert.True(blazor.Count > 0,
            $"parser extracted 0 {subject} from {pair.BlazorPath} — the fixture shape no longer matches the parser");

        var missingFromBlazor = react.Except(blazor).Select(format).OrderBy(text => text, StringComparer.Ordinal).ToArray();
        var missingFromReact = blazor.Except(react).Select(format).OrderBy(text => text, StringComparer.Ordinal).ToArray();
        var message = new StringBuilder($"{pair.Pillar} fixture {subject} differ between lanes.")
            .AppendLine()
            .AppendLine($"Present in React ({pair.ReactPath}) but missing in Blazor:")
            .AppendLine(FormatRows(missingFromBlazor))
            .AppendLine($"Present in Blazor ({pair.BlazorPath}) but missing in React:")
            .Append(FormatRows(missingFromReact))
            .ToString();

        Assert.True(missingFromBlazor.Length == 0 && missingFromReact.Length == 0, message);
    }

    /// <summary>
    /// Extracts version rows from the React fixture, whose helper reads
    /// <c>version(formId, version, status, owner, derivedFrom, updatedAt)</c>.
    /// </summary>
    /// <param name="source">The React fixture source.</param>
    /// <returns>The distinct version rows.</returns>
    private static IReadOnlyCollection<VersionRow> ParseReactFormVersions(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(
            source, @"\bINITIAL_VERSIONS\b[^=]*=\s*\{", '{', '}');
        var rows = new HashSet<VersionRow>();
        var index = 0;
        foreach (var arguments in ExtractCalls(region, @"\bversion\s*\("))
        {
            var where = $"React version(...) row {++index}";
            AssertArity(arguments, 6, where);
            rows.Add(new VersionRow(
                ReadLiteral(arguments[0], $"{where} formId"),
                ReadLiteral(arguments[1], $"{where} version"),
                ReadLiteral(arguments[2], $"{where} status"),
                ReadOptionalLiteral(arguments[3], $"{where} owner"),
                ReadOptionalLiteral(arguments[4], $"{where} derivedFrom"),
                ReadTimestamp(arguments[5], $"{where} updatedAt")));
        }

        return rows;
    }

    /// <summary>
    /// Extracts version rows from the Blazor fixture, whose helper reads
    /// <c>Row(formId, version, status, owner, timestamp, derivedFrom)</c> — the final two
    /// parameters are transposed relative to the React helper.
    /// </summary>
    /// <param name="source">The Blazor fixture source.</param>
    /// <returns>The distinct version rows.</returns>
    private static IReadOnlyCollection<VersionRow> ParseBlazorFormVersions(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(
            source, @"\bversions\b\s*=\s*new\b[^{]*\{", '{', '}');
        var rows = new HashSet<VersionRow>();
        var index = 0;
        foreach (var arguments in ExtractCalls(region, @"\bRow\s*\("))
        {
            var where = $"Blazor Row(...) row {++index}";
            AssertArity(arguments, 6, where);
            rows.Add(new VersionRow(
                ReadLiteral(arguments[0], $"{where} formId"),
                ReadLiteral(arguments[1], $"{where} version"),
                ReadLiteral(arguments[2], $"{where} status"),
                ReadOptionalLiteral(arguments[3], $"{where} owner"),
                ReadOptionalLiteral(arguments[5], $"{where} derivedFrom"),
                ReadTimestamp(arguments[4], $"{where} timestamp")));
        }

        return rows;
    }

    /// <summary>
    /// Extracts definition summaries from the React fixture's DEFINITIONS object literals.
    /// </summary>
    /// <param name="source">The React fixture source.</param>
    /// <returns>The distinct definition summaries.</returns>
    private static IReadOnlyCollection<DefinitionRow> ParseReactFormDefinitions(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(
            source, @"\bDEFINITIONS\b[^=]*=\s*\[", '[', ']');
        var rows = new HashSet<DefinitionRow>();
        var index = 0;
        foreach (Match match in Regex.Matches(region, @"\{\s*formId\s*:", RegexOptions.CultureInvariant))
        {
            var where = $"React DEFINITIONS entry {++index}";
            var fields = ParseObjectLiteral(LaneSourceScanner.ExtractBalanced(region, match.Index, '{', '}'), where);
            rows.Add(new DefinitionRow(
                ReadLiteral(ReadField(fields, "formId", where), $"{where} formId"),
                ReadLiteral(ReadField(fields, "version", where), $"{where} version"),
                ReadReactTitle(ReadField(fields, "title", where), $"{where} title"),
                ReadTimestamp(ReadField(fields, "updatedAt", where), $"{where} updatedAt"),
                ReadOptionalLiteral(ReadField(fields, "cascadeLayer", where), $"{where} cascadeLayer")));
        }

        return rows;
    }

    /// <summary>
    /// Extracts definition summaries from the Blazor fixture's target-typed
    /// <c>new(formId, version, title, updatedAt, cascadeLayer)</c> records.
    /// </summary>
    /// <param name="source">The Blazor fixture source.</param>
    /// <returns>The distinct definition summaries.</returns>
    private static IReadOnlyCollection<DefinitionRow> ParseBlazorFormDefinitions(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(
            source, @"\bdefinitions\b\s*=\s*\[", '[', ']');
        var rows = new HashSet<DefinitionRow>();
        var index = 0;
        foreach (var arguments in ExtractCalls(region, @"\bnew\s*\("))
        {
            var where = $"Blazor definitions entry {++index}";
            AssertArity(arguments, 5, where);
            rows.Add(new DefinitionRow(
                ReadLiteral(arguments[0], $"{where} FormId"),
                ReadLiteral(arguments[1], $"{where} Version"),
                ReadBlazorTitle(arguments[2], $"{where} Title"),
                ReadTimestamp(arguments[3], $"{where} UpdatedAt"),
                ReadOptionalLiteral(arguments[4], $"{where} CascadeLayer")));
        }

        return rows;
    }

    /// <summary>
    /// Returns the top-level argument list of every call whose callee matches the pattern.
    /// </summary>
    /// <param name="region">The isolated source region to search.</param>
    /// <param name="calleePattern">A regular expression ending at the call's opening parenthesis.</param>
    /// <returns>One trimmed argument list per call site, in source order.</returns>
    private static List<List<string>> ExtractCalls(string region, string calleePattern)
    {
        var calls = new List<List<string>>();
        foreach (Match match in Regex.Matches(region, calleePattern, RegexOptions.CultureInvariant))
        {
            var body = LaneSourceScanner.ExtractBalanced(region, match.Index + match.Length - 1, '(', ')');
            Assert.True(body.Length >= 2,
                $"Unbalanced argument list for '{match.Value.Trim()}' at offset {match.Index}.");
            calls.Add(LaneSourceScanner.SplitTopLevel(body[1..^1], ','));
        }

        return calls;
    }

    /// <summary>
    /// Splits an object literal into its top-level field values, keyed by field name.
    /// </summary>
    /// <param name="objectText">The literal including its braces.</param>
    /// <param name="where">The location description used in failure messages.</param>
    /// <returns>Field values keyed by name, with the value text left unparsed.</returns>
    private static Dictionary<string, string> ParseObjectLiteral(string objectText, string where)
    {
        Assert.True(objectText.Length >= 2, $"{where}: unbalanced object literal.");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var segment in LaneSourceScanner.SplitTopLevel(objectText[1..^1], ','))
        {
            if (segment.Length == 0)
            {
                continue;
            }

            var parts = LaneSourceScanner.SplitTopLevel(segment, ':');
            Assert.True(parts.Count >= 2, $"{where}: '{segment}' is not a name/value pair.");
            fields[parts[0].Trim('\'', '\"')] = string.Join(":", parts.Skip(1)).Trim();
        }

        return fields;
    }

    /// <summary>
    /// Reads a required field from a parsed object literal.
    /// </summary>
    /// <param name="fields">The parsed fields.</param>
    /// <param name="name">The field name.</param>
    /// <param name="where">The location description used in failure messages.</param>
    /// <returns>The unparsed value text.</returns>
    private static string ReadField(IReadOnlyDictionary<string, string> fields, string name, string where)
    {
        Assert.True(fields.TryGetValue(name, out var value), $"{where}: field '{name}' is absent.");
        return value!;
    }

    /// <summary>
    /// Asserts that a call site carries the argument count the parser was written against, so a
    /// changed helper signature fails loudly instead of silently mis-mapping values.
    /// </summary>
    /// <param name="arguments">The extracted arguments.</param>
    /// <param name="expected">The expected count.</param>
    /// <param name="where">The location description used in failure messages.</param>
    private static void AssertArity(IReadOnlyCollection<string> arguments, int expected, string where) =>
        Assert.True(arguments.Count == expected,
            $"{where}: expected {expected} arguments but found {arguments.Count} — the fixture helper signature changed, so the parser must change with it.");

    /// <summary>
    /// Reads a required single-quoted or double-quoted string literal.
    /// </summary>
    /// <param name="text">The argument or field value text.</param>
    /// <param name="where">The location description used in failure messages.</param>
    /// <returns>The literal's unescaped value.</returns>
    private static string ReadLiteral(string text, string where)
    {
        var value = ReadOptionalLiteral(text, where);
        Assert.True(value is not null, $"{where}: expected a string literal but found 'null'.");
        return value!;
    }

    /// <summary>
    /// Reads a string literal, or null for a literal null.
    /// </summary>
    /// <param name="text">The argument or field value text.</param>
    /// <param name="where">The location description used in failure messages.</param>
    /// <returns>The literal's unescaped value, or null.</returns>
    private static string? ReadOptionalLiteral(string text, string where)
    {
        var trimmed = text.Trim();
        if (trimmed == "null")
        {
            return null;
        }

        var match = Regex.Match(
            trimmed,
            @"^(?<quote>['""])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>$",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(match.Success, $"{where}: '{trimmed}' is neither a string literal nor null.");
        return Unescape(match.Groups["value"].Value);
    }

    /// <summary>
    /// Reads the timestamp a fixture value carries, whether written as a bare literal (React) or
    /// wrapped in a parse call (Blazor), and canonicalises it to UTC so an equivalent instant
    /// written in a different offset is not reported as a divergence.
    /// </summary>
    /// <param name="text">The argument or field value text.</param>
    /// <param name="where">The location description used in failure messages.</param>
    /// <returns>The instant in round-trip UTC form.</returns>
    private static string ReadTimestamp(string text, string where)
    {
        var match = Regex.Match(
            text,
            @"(?<quote>['""])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(match.Success, $"{where}: '{text.Trim()}' contains no timestamp literal.");
        var literal = Unescape(match.Groups["value"].Value);
        Assert.True(
            DateTimeOffset.TryParse(literal, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant),
            $"{where}: '{literal}' is not a parsable timestamp.");
        return instant.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads the React internationalized-title literal
    /// <c>{ defaultLocale: 'x', values: { x: 'y' } }</c>.
    /// </summary>
    /// <param name="text">The field value text.</param>
    /// <param name="where">The location description used in failure messages.</param>
    /// <returns>The canonical title form, or null when the title is absent.</returns>
    private static string? ReadReactTitle(string text, string where)
    {
        var trimmed = text.Trim();
        if (trimmed == "null")
        {
            return null;
        }

        var title = ParseObjectLiteral(trimmed, where);
        var values = ParseObjectLiteral(ReadField(title, "values", where), $"{where} values");
        return FormatTitle(
            ReadLiteral(ReadField(title, "defaultLocale", where), $"{where} defaultLocale"),
            values.Select(entry => KeyValuePair.Create(entry.Key, ReadLiteral(entry.Value, $"{where} values['{entry.Key}']"))));
    }

    /// <summary>
    /// Reads the Blazor internationalized-title argument
    /// <c>new InternationalizedText("x", new Dictionary&lt;string, string&gt; { ["x"] = "y" })</c>.
    /// </summary>
    /// <param name="text">The argument text.</param>
    /// <param name="where">The location description used in failure messages.</param>
    /// <returns>The canonical title form, or null when the title is absent.</returns>
    private static string? ReadBlazorTitle(string text, string where)
    {
        var trimmed = text.Trim();
        if (trimmed == "null")
        {
            return null;
        }

        var calls = ExtractCalls(trimmed, @"\bnew\s+InternationalizedText\s*\(");
        Assert.True(calls.Count == 1, $"{where}: expected one InternationalizedText construction in '{trimmed}'.");
        AssertArity(calls[0], 2, where);

        var mapText = calls[0][1];
        var braceIndex = LaneSourceScanner.IndexOfUnquoted(mapText, '{', 0);
        Assert.True(braceIndex >= 0, $"{where}: the title values argument '{mapText}' has no initializer.");
        var mapBody = LaneSourceScanner.ExtractBalanced(mapText, braceIndex, '{', '}');
        Assert.True(mapBody.Length >= 2, $"{where}: the title values initializer is unbalanced.");

        var values = new List<KeyValuePair<string, string>>();
        foreach (var entry in LaneSourceScanner.SplitTopLevel(mapBody[1..^1], ','))
        {
            if (entry.Length == 0)
            {
                continue;
            }

            var parts = LaneSourceScanner.SplitTopLevel(entry, '=');
            Assert.True(parts.Count == 2, $"{where}: '{entry}' is not an indexer initializer.");
            var key = parts[0].Trim();
            Assert.True(key.StartsWith('[') && key.EndsWith(']'), $"{where}: '{entry}' is not an indexer initializer.");
            values.Add(KeyValuePair.Create(
                ReadLiteral(key[1..^1], $"{where} values key"),
                ReadLiteral(parts[1], $"{where} values value")));
        }

        return FormatTitle(ReadLiteral(calls[0][0], $"{where} defaultLocale"), values);
    }

    /// <summary>
    /// Renders an internationalized title in a lane-neutral, order-independent form.
    /// </summary>
    /// <param name="defaultLocale">The default locale.</param>
    /// <param name="values">The localized values.</param>
    /// <returns>The canonical title form.</returns>
    private static string FormatTitle(string defaultLocale, IEnumerable<KeyValuePair<string, string>> values) =>
        $"{defaultLocale}|{string.Join(";", values.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => $"{entry.Key}={entry.Value}"))}";

    /// <summary>
    /// Resolves the escape sequences the two lanes share in fixture string literals.
    /// </summary>
    /// <param name="value">The raw literal body.</param>
    /// <returns>The unescaped text.</returns>
    private static string Unescape(string value) =>
        Regex.Replace(value, @"\\(?<escaped>.)", match => match.Groups["escaped"].Value switch
        {
            "n" => "\n",
            "r" => "\r",
            "t" => "\t",
            var other => other,
        }, RegexOptions.CultureInvariant | RegexOptions.Singleline);

    /// <summary>
    /// Renders a version row with every compared value visible in assertion output.
    /// </summary>
    /// <param name="row">The row to render.</param>
    /// <returns>A stable representation.</returns>
    private static string FormatVersion(VersionRow row) =>
        $"{row.FormId} {row.Version} status={row.Status} owner={Show(row.Owner)} derivedFrom={Show(row.DerivedFrom)} updatedAt={row.UpdatedAt}";

    /// <summary>
    /// Renders a definition summary with every compared value visible in assertion output.
    /// </summary>
    /// <param name="row">The row to render.</param>
    /// <returns>A stable representation.</returns>
    private static string FormatDefinition(DefinitionRow row) =>
        $"{row.FormId} {row.Version} title={Show(row.Title)} updatedAt={row.UpdatedAt} cascadeLayer={Show(row.CascadeLayer)}";

    /// <summary>
    /// Renders a nullable fixture value so an absent value is distinguishable from an empty one.
    /// </summary>
    /// <param name="value">The value to render.</param>
    /// <returns>The quoted value, or a null marker.</returns>
    private static string Show(string? value) => value is null ? "(null)" : $"'{value}'";

    /// <summary>
    /// Formats rendered rows as assertion-message lines.
    /// </summary>
    /// <param name="rows">The rendered rows.</param>
    /// <returns>Formatted rows, or a marker indicating there are none.</returns>
    private static string FormatRows(IReadOnlyCollection<string> rows) =>
        rows.Count == 0 ? "  (none)" : string.Join(Environment.NewLine, rows.Select(row => $"  {row}"));

    /// <summary>
    /// Extracts Reports version rows from the React fixture, where histories are derived from the
    /// DEFINITIONS heads: <c>'key': { ordering: 'x', versions: [...].map(value => summary(DEFINITIONS[i], value)) }</c>.
    /// </summary>
    /// <param name="source">The React fixture source.</param>
    /// <param name="kindField">The pillar's kind field name in the head object literals.</param>
    /// <returns>Canonical "key|version|title|kind|cascade|ordering" rows.</returns>
    private static IReadOnlyCollection<string> ParseReactReportVersions(string source, string kindField)
    {
        var heads = ParseReactReportHeads(source, kindField);
        var region = LaneSourceScanner.ExtractAssignedCollection(
            source, @"\bVERSION_LISTS\b[^=]*=\s*\{", '{', '}');
        var rows = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(
            region,
            @"'(?<key>[^']+)':\s*\{\s*ordering:\s*'(?<ordering>[^']+)',\s*versions:\s*\[(?<versions>[^\]]*)\]\s*\.map\(\s*value\s*=>\s*summary\(DEFINITIONS\[(?<index>\d+)\]",
            RegexOptions.CultureInvariant))
        {
            var head = heads[int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture)];
            Assert.Equal(head.Key, match.Groups["key"].Value);
            foreach (Match version in Regex.Matches(match.Groups["versions"].Value, @"'([^']+)'"))
            {
                rows.Add($"{head.Key}|{version.Groups[1].Value}|{head.Title}|{head.Kind}|{head.Cascade}|{match.Groups["ordering"].Value}");
            }
        }

        return rows;
    }

    /// <summary>
    /// Extracts Reports version rows from the Blazor fixture's
    /// <c>versions</c> dictionary of <c>new("ordering", [ new(key, version, title, kind, cascade), ... ])</c> entries.
    /// </summary>
    /// <param name="source">The Blazor fixture source.</param>
    /// <returns>Canonical "key|version|title|kind|cascade|ordering" rows.</returns>
    private static IReadOnlyCollection<string> ParseBlazorReportVersions(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(
            source, @"\bversions\b\s*=\s*new Dictionary<string, \w+VersionList>\(StringComparer\.Ordinal\)\s*\{", '{', '}');
        var rows = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match entry in Regex.Matches(region, @"\[""(?<key>[^""]+)""\]\s*=\s*new\(", RegexOptions.CultureInvariant))
        {
            var block = LaneSourceScanner.ExtractBalanced(
                region, region.IndexOf('(', entry.Index + entry.Length - 1), '(', ')');
            var ordering = Regex.Match(block, @"""([^""]+)""").Groups[1].Value;
            foreach (var arguments in ExtractCalls(block, @"\bnew\s*\(").Where(call => call.Count == 5))
            {
                rows.Add(string.Join("|",
                    ReadLiteral(arguments[0], "Blazor report version key"),
                    ReadLiteral(arguments[1], "Blazor report version version"),
                    ReadLiteral(arguments[2], "Blazor report version title"),
                    ReadLiteral(arguments[3], "Blazor report version kind"),
                    ReadLiteral(arguments[4], "Blazor report version cascade")) + $"|{ordering}");
            }
        }

        return rows;
    }

    /// <summary>
    /// Extracts Reports definition summaries from the React fixture's DEFINITIONS object literals.
    /// </summary>
    /// <param name="source">The React fixture source.</param>
    /// <param name="kindField">The pillar's kind field name in the head object literals.</param>
    /// <returns>Canonical "key|version|title|kind|cascade" rows.</returns>
    private static IReadOnlyCollection<string> ParseReactReportDefinitions(string source, string kindField) =>
        ParseReactReportHeads(source, kindField)
            .Select(head => $"{head.Key}|{head.Version}|{head.Title}|{head.Kind}|{head.Cascade}")
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Extracts Reports definition summaries from the Blazor fixture's <c>definitions</c> list of
    /// five-argument <c>new(...)</c> rows.
    /// </summary>
    /// <param name="source">The Blazor fixture source.</param>
    /// <returns>Canonical "key|version|title|kind|cascade" rows.</returns>
    private static IReadOnlyCollection<string> ParseBlazorReportDefinitions(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(
            source, @"\bdefinitions\b\s*=\s*\[", '[', ']');
        return ExtractCalls(region, @"\bnew\s*\(")
            .Where(call => call.Count == 5)
            .Select(arguments => string.Join("|",
                ReadLiteral(arguments[0], "Blazor report definition key"),
                ReadLiteral(arguments[1], "Blazor report definition version"),
                ReadLiteral(arguments[2], "Blazor report definition title"),
                ReadLiteral(arguments[3], "Blazor report definition kind"),
                ReadLiteral(arguments[4], "Blazor report definition cascade")))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Parses the React DEFINITIONS heads in declaration order (histories address them by index).
    /// </summary>
    /// <param name="source">The React fixture source.</param>
    /// <param name="kindField">The pillar's kind field name in the head object literals.</param>
    /// <returns>The ordered head rows.</returns>
    private static List<(string Key, string Version, string Title, string Kind, string Cascade)> ParseReactReportHeads(string source, string kindField)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(
            source, @"\bDEFINITIONS\b[^=]*=\s*\[", '[', ']');
        var heads = new List<(string, string, string, string, string)>();
        var index = 0;
        foreach (Match match in Regex.Matches(region, @"\{\s*key\s*:", RegexOptions.CultureInvariant))
        {
            var where = $"React report DEFINITIONS entry {++index}";
            var fields = ParseObjectLiteral(LaneSourceScanner.ExtractBalanced(region, match.Index, '{', '}'), where);
            heads.Add((
                ReadLiteral(ReadField(fields, "key", where), $"{where} key"),
                ReadLiteral(ReadField(fields, "version", where), $"{where} version"),
                ReadLiteral(ReadField(fields, "title", where), $"{where} title"),
                ReadLiteral(ReadField(fields, kindField, where), $"{where} {kindField}"),
                ReadLiteral(ReadField(fields, "cascadeLayer", where), $"{where} cascadeLayer")));
        }

        return heads;
    }

    /// <summary>
    /// Extracts Scheduling revision rows from the React fixture's INITIAL_VERSIONS map, whose helper
    /// reads <c>summary(id, revision, title, updatedAt, updatedBy?)</c>. The final argument carries a
    /// default in the helper signature, so the default is read from source rather than assumed —
    /// changing it must move both lanes, not silently move one.
    /// </summary>
    /// <param name="source">The React fixture source.</param>
    /// <returns>Canonical "id|revision|title|updatedAt|updatedBy" rows.</returns>
    private static IReadOnlyCollection<string> ParseReactSchedulingVersions(string source)
    {
        var fallbackOwner = ReadReactSummaryOwnerDefault(source);
        var region = LaneSourceScanner.ExtractAssignedCollection(
            source, @"\bINITIAL_VERSIONS\b[^=]*=\s*\{", '{', '}');
        var rows = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var arguments in ExtractCalls(region, @"\bsummary\s*\("))
        {
            var where = $"React summary(...) row {++index}";
            Assert.True(arguments.Count is 4 or 5,
                $"{where}: expected 4 or 5 arguments but found {arguments.Count} — the fixture helper signature changed, so the parser must change with it.");
            rows.Add(string.Join("|",
                ReadLiteral(arguments[0], $"{where} id"),
                ReadScalar(arguments[1], $"{where} revision"),
                ReadLiteral(arguments[2], $"{where} title"),
                ReadTimestamp(arguments[3], $"{where} updatedAt"),
                arguments.Count == 5 ? ReadLiteral(arguments[4], $"{where} updatedBy") : fallbackOwner));
        }

        return rows;
    }

    /// <summary>
    /// Extracts Scheduling revision rows from the Blazor fixture's <c>histories</c> dictionary, whose
    /// entries read <c>["id"] = [ Row(revision, title, timestamp, updatedBy), ... ]</c> — the
    /// definition id lives on the dictionary key, not on the row.
    /// </summary>
    /// <param name="source">The Blazor fixture source.</param>
    /// <returns>Canonical "id|revision|title|updatedAt|updatedBy" rows.</returns>
    private static IReadOnlyCollection<string> ParseBlazorSchedulingVersions(string source) =>
        ParseBlazorSchedulingHistories(source)
            .SelectMany(history => history.Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Extracts Scheduling definition summaries from the React fixture's DEFINITIONS literals.
    /// </summary>
    /// <param name="source">The React fixture source.</param>
    /// <returns>Canonical "id|revision|title|updatedAt|updatedBy" rows.</returns>
    private static IReadOnlyCollection<string> ParseReactSchedulingDefinitions(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(
            source, @"\bDEFINITIONS\b[^=]*=\s*\[", '[', ']');
        var rows = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (Match match in Regex.Matches(region, @"\{\s*id\s*:", RegexOptions.CultureInvariant))
        {
            var where = $"React scheduling DEFINITIONS entry {++index}";
            var fields = ParseObjectLiteral(LaneSourceScanner.ExtractBalanced(region, match.Index, '{', '}'), where);
            rows.Add(string.Join("|",
                ReadLiteral(ReadField(fields, "id", where), $"{where} id"),
                ReadScalar(ReadField(fields, "revision", where), $"{where} revision"),
                ReadLiteral(ReadField(fields, "title", where), $"{where} title"),
                ReadTimestamp(ReadField(fields, "updatedAt", where), $"{where} updatedAt"),
                ReadLiteral(ReadField(fields, "updatedBy", where), $"{where} updatedBy")));
        }

        return rows;
    }

    /// <summary>
    /// Derives Scheduling definition summaries from the Blazor fixture, which carries no literal
    /// definitions list: its <c>ListDefinitionsAsync</c> walks <c>definitionOrder</c> and takes each
    /// history's head. Deriving the same way makes an id listed in one place but absent from the
    /// other a parser failure rather than an invisible divergence.
    /// </summary>
    /// <param name="source">The Blazor fixture source.</param>
    /// <returns>Canonical "id|revision|title|updatedAt|updatedBy" rows.</returns>
    private static IReadOnlyCollection<string> ParseBlazorSchedulingDefinitions(string source)
    {
        var histories = ParseBlazorSchedulingHistories(source);
        var order = LaneSourceScanner.ExtractAssignedCollection(
            source, @"\bdefinitionOrder\b\s*=\s*\[", '[', ']');
        var rows = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var entry in LaneSourceScanner.SplitTopLevel(order[1..^1], ','))
        {
            if (entry.Trim().Length == 0)
            {
                continue;
            }

            var where = $"Blazor definitionOrder entry {++index}";
            var id = ReadLiteral(entry, where);
            Assert.True(histories.TryGetValue(id, out var revisions),
                $"{where}: '{id}' is listed in definitionOrder but has no history — the list would throw at runtime.");
            rows.Add(revisions![0]);
        }

        Assert.True(rows.Count == histories.Count,
            $"Blazor definitionOrder lists {rows.Count} ids but histories seeds {histories.Count} — a seeded definition would never appear in the list.");
        return rows;
    }

    /// <summary>
    /// Parses the Blazor Scheduling fixture's <c>histories</c> dictionary into canonical rows, keyed
    /// by definition id and kept in source order so the head is the first row.
    /// </summary>
    /// <param name="source">The Blazor fixture source.</param>
    /// <returns>Canonical rows per definition id, newest first.</returns>
    private static Dictionary<string, List<string>> ParseBlazorSchedulingHistories(string source)
    {
        var region = LaneSourceScanner.ExtractAssignedCollection(
            source, @"\bhistories\b\s*=\s*new\b[^{]*\{", '{', '}');
        var histories = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (Match entry in Regex.Matches(region, @"\[""(?<id>[^""]+)""\]\s*=", RegexOptions.CultureInvariant))
        {
            var id = entry.Groups["id"].Value;
            var openIndex = LaneSourceScanner.IndexOfUnquoted(region, '[', entry.Index + entry.Length);
            Assert.True(openIndex >= 0, $"Blazor histories entry '{id}' has no revision collection.");
            var block = LaneSourceScanner.ExtractBalanced(region, openIndex, '[', ']');
            var rows = new List<string>();
            var index = 0;
            foreach (var arguments in ExtractCalls(block, @"\bRow\s*\("))
            {
                var where = $"Blazor Row(...) row {++index} of '{id}'";
                AssertArity(arguments, 4, where);
                rows.Add(string.Join("|",
                    id,
                    ReadScalar(arguments[0], $"{where} revision"),
                    ReadLiteral(arguments[1], $"{where} title"),
                    ReadTimestamp(arguments[2], $"{where} timestamp"),
                    ReadLiteral(arguments[3], $"{where} updatedBy")));
            }

            Assert.True(rows.Count > 0, $"Blazor histories entry '{id}' seeds no revisions.");
            histories[id] = rows;
        }

        return histories;
    }

    /// <summary>
    /// Reads the default owner the React <c>summary</c> helper applies when a row omits it.
    /// </summary>
    /// <param name="source">The React fixture source.</param>
    /// <returns>The default owner literal's value.</returns>
    private static string ReadReactSummaryOwnerDefault(string source)
    {
        var signature = Regex.Match(
            source,
            @"\bconst\s+summary\s*=\s*\((?<params>[^)]*)\)",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(signature.Success, "The React scheduling fixture no longer declares a 'summary' helper.");
        var fallback = Regex.Match(
            signature.Groups["params"].Value,
            @"\bupdatedBy\b[^=]*=\s*(?<literal>'[^']*'|""[^""]*"")",
            RegexOptions.CultureInvariant);
        Assert.True(fallback.Success,
            "The React scheduling 'summary' helper no longer defaults updatedBy — every row must now pass it explicitly, so the parser must change with it.");
        return ReadLiteral(fallback.Groups["literal"].Value, "React summary() updatedBy default");
    }

    /// <summary>
    /// Reads a value written either as a string literal or as a bare number, so a pillar whose
    /// revisions are integers compares against a lane that quotes them.
    /// </summary>
    /// <param name="text">The argument or field value text.</param>
    /// <param name="where">The location description used in failure messages.</param>
    /// <returns>The value's canonical text.</returns>
    private static string ReadScalar(string text, string where)
    {
        var trimmed = text.Trim();
        if (Regex.IsMatch(trimmed, @"^-?\d+$", RegexOptions.CultureInvariant))
        {
            return trimmed;
        }

        return ReadLiteral(trimmed, where);
    }

    /// <summary>
    /// Registers one pillar's fixture pair together with the parsers for each lane's shape. The
    /// parsers render canonical row strings, so pillars with different tuple shapes share one fence.
    /// </summary>
    /// <param name="Pillar">The pillar name, used as the test case identity.</param>
    /// <param name="ReactPath">The React fixture path relative to the repository root.</param>
    /// <param name="BlazorPath">The Blazor fixture path relative to the repository root.</param>
    /// <param name="ReactVersions">Extracts canonical version rows from the React fixture source.</param>
    /// <param name="BlazorVersions">Extracts canonical version rows from the Blazor fixture source.</param>
    /// <param name="ReactDefinitions">Extracts canonical definition summaries from the React fixture source.</param>
    /// <param name="BlazorDefinitions">Extracts canonical definition summaries from the Blazor fixture source.</param>
    private sealed record FixturePair(
        string Pillar,
        string ReactPath,
        string BlazorPath,
        Func<string, IReadOnlyCollection<string>> ReactVersions,
        Func<string, IReadOnlyCollection<string>> BlazorVersions,
        Func<string, IReadOnlyCollection<string>> ReactDefinitions,
        Func<string, IReadOnlyCollection<string>> BlazorDefinitions);

    private sealed record AuthorizationFixturePair(string ReactPath, string BlazorPath);

    /// <summary>
    /// Represents one seeded version-history row in lane-neutral terms.
    /// </summary>
    /// <param name="FormId">The definition key.</param>
    /// <param name="Version">The revision.</param>
    /// <param name="Status">The revision status.</param>
    /// <param name="Owner">The owning identity, or null.</param>
    /// <param name="DerivedFrom">The restore provenance, or null.</param>
    /// <param name="UpdatedAt">The revision instant in round-trip UTC form.</param>
    private sealed record VersionRow(
        string FormId,
        string Version,
        string Status,
        string? Owner,
        string? DerivedFrom,
        string UpdatedAt);

    /// <summary>
    /// Represents one seeded definition summary in lane-neutral terms.
    /// </summary>
    /// <param name="FormId">The definition key.</param>
    /// <param name="Version">The published head revision.</param>
    /// <param name="Title">The canonical internationalized title, or null.</param>
    /// <param name="UpdatedAt">The head instant in round-trip UTC form.</param>
    /// <param name="CascadeLayer">The cascade layer, or null.</param>
    private sealed record DefinitionRow(
        string FormId,
        string Version,
        string? Title,
        string UpdatedAt,
        string? CascadeLayer);
}
