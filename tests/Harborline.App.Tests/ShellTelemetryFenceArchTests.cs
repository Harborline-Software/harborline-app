using System.Text.RegularExpressions;

namespace Harborline.App.Tests;

public sealed class ShellTelemetryFenceArchTests
{
    private static readonly string[] Roots = ["apps/react/src", "apps/blazor", "src"];
    private static readonly HashSet<string> Extensions = [".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs", ".cs", ".razor", ".html"];
    private static readonly HashSet<string> ExcludedDirectories = ["bin", "obj", "dist", "node_modules", "wwwroot", "scripts", "__tests__"];
    private static readonly Regex Emit = new(@"\b(?:console\s*(?:\?\.\s*\w+|\.\s*\w+|\[\s*['""'][^'""']+['""']\s*\])\s*\(|Invoke(?:Void)?Async\s*(?:<[^>]+>\s*)?\(\s*['""']console\.[^'""']+['""']|EventSource\b|ILogger(?:Factory)?\b|LoggerMessage\b|LoggerFactory\s*\.\s*Create\s*\(|Add(?:\w*Logger|Console|Debug|EventLog|OpenTelemetry)\s*\(|(?:TracerProvider|MeterProvider|IMeterFactory|ActivitySource|DiagnosticListener)\b|System\.Diagnostics\.Metrics\b|new\s+Meter\s*\(|Console\.(?:Write|Error|Out)\w*\b|Debug\.(?:Write|Print)\w*\b|Trace\.(?:Write|Trace)\w*\b|Log\s*\.\s*\w+\s*\(|navigator\s*(?:\?\.|\.)\s*sendBeacon\b|PerformanceObserver\b|(?:import|from|require)\b[^\r\n]*@opentelemetry/)", RegexOptions.CultureInvariant);

    // Add only exact paths with their count and admitting ticket or design clause.
    private static readonly Dictionary<string, (int Count, string Reason)> AllowList = [];

    [Fact]
    public void Shell_sources_hold_no_telemetry_or_logging_call_outside_the_allow_list()
    {
        var root = LaneSourceScanner.LocateHostSourceRoot();
        var (findings, counts) = Scan(root);
        Assert.All(Roots, sourceRoot => Assert.True(counts[sourceRoot] > 0, $"No source files scanned under {sourceRoot}"));
        Assert.All(AllowList, row => Assert.True(!string.IsNullOrWhiteSpace(row.Value.Reason) &&
            Regex.IsMatch(row.Value.Reason, @"\b(?:T-\d+|telemetry-(?:cc|ck)-\d+|DES-\d+)\b"), $"Missing admission ticket or design clause: {row.Key}"));

        AssertAllowed(findings, AllowList);
    }

    private static void AssertAllowed(List<(string Path, int Line)> findings, Dictionary<string, (int Count, string Reason)> allowList)
    {
        var actual = findings.GroupBy(finding => finding.Path).ToDictionary(group => group.Key, group => group.Count());
        var matches = actual.Count == allowList.Count && allowList.All(row => actual.TryGetValue(row.Key, out var count) && count == row.Value.Count);
        Assert.True(matches, "telemetry-cc-15: never notification text; telemetry-ck-2: telemetry carries no field values. " +
            "Admit a new emit path with an allow-list row and its ticket.\n" +
            string.Join("\n", findings.Select(finding => $"{finding.Path}:{finding.Line}")) +
            "\nAllow-list rows: " + string.Join(", ", allowList.Select(row => $"{row.Key} ({row.Value.Count})")));
    }

    [Fact]
    public void Shell_allow_list_rejects_unlisted_and_stale_findings()
    {
        var findings = new List<(string Path, int Line)> { ("src/planted.cs", 7) };
        var exception = Assert.Throws<Xunit.Sdk.TrueException>(() => AssertAllowed(findings, []));
        Assert.Contains("src/planted.cs:7", exception.Message);
        Assert.Contains("telemetry-cc-15: never notification text", exception.Message);
        Assert.Contains("allow-list row and its ticket", exception.Message);
        AssertAllowed(findings, new() { ["src/planted.cs"] = (1, "T-502") });
        Assert.Throws<Xunit.Sdk.TrueException>(() => AssertAllowed([], new() { ["src/planted.cs"] = (1, "T-502") }));
    }

    [Fact]
    public void Shell_fence_scans_each_line_independently()
    {
        var root = Path.Combine(Path.GetTempPath(), "harborline", "app104", $"shell-fence-{Guid.NewGuid():N}");
        try
        {
            foreach (var sourceRoot in Roots) Directory.CreateDirectory(Path.Combine(root, sourceRoot));
            File.WriteAllLines(Path.Combine(root, "apps/react/src/comment.ts"),
            ["const enabled = true; /*", "console.log(\"example\");", "*/", "console.log(after);", "console.log(before); /*", "ignored", "*/"]);
            var (findings, _) = Scan(root);
            Assert.Equal(new[] { "apps/react/src/comment.ts:2", "apps/react/src/comment.ts:4", "apps/react/src/comment.ts:5" },
                findings.Select(finding => $"{finding.Path}:{finding.Line}"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Shell_fence_reports_event_source_without_script_state()
    {
        var root = Path.Combine(Path.GetTempPath(), "harborline", "app104", $"shell-fence-{Guid.NewGuid():N}");
        try
        {
            foreach (var sourceRoot in Roots) Directory.CreateDirectory(Path.Combine(root, sourceRoot));
            File.WriteAllLines(Path.Combine(root, "apps/blazor/Script.razor"),
                ["<script>const events = new EventSource(\"/events\");</script>", "<script>", "new EventSource('/events');", "console.log(value);", "</script>"]);
            var (findings, _) = Scan(root);
            Assert.Equal(new[] { "apps/blazor/Script.razor:1", "apps/blazor/Script.razor:3", "apps/blazor/Script.razor:4" }, findings.Select(finding => $"{finding.Path}:{finding.Line}"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Shell_fence_reports_dotnet_event_source_in_razor_code()
    {
        var root = Path.Combine(Path.GetTempPath(), "harborline", "app104", $"shell-fence-{Guid.NewGuid():N}");
        try
        {
            foreach (var sourceRoot in Roots) Directory.CreateDirectory(Path.Combine(root, sourceRoot));
            File.WriteAllLines(Path.Combine(root, "apps/blazor/Code.razor"), ["@code {", "EventSource source;", "}"]);
            var (findings, _) = Scan(root);
            Assert.Equal(new[] { "apps/blazor/Code.razor:2" }, findings.Select(finding => $"{finding.Path}:{finding.Line}"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Shell_fence_reports_every_planted_emit_call()
    {
        var root = Path.Combine(Path.GetTempPath(), "harborline", "app104", $"shell-fence-{Guid.NewGuid():N}");
        try
        {
            foreach (var sourceRoot in Roots) Directory.CreateDirectory(Path.Combine(root, sourceRoot));
            File.WriteAllLines(Path.Combine(root, "apps/react/src/emits.ts"),
            [
                "console.log(value);", "navigator.sendBeacon(url);", "new PerformanceObserver(callback);",
                "import { trace } from '@opentelemetry/api';", "// console.log(ignored);",
                "console?.log(value);", "console['warn'](value);", "navigator?.sendBeacon(url);",
                "/* comment */ console.log(value);", "*/ console.log(value);", "new EventSource(url);"
            ]);
            File.WriteAllLines(Path.Combine(root, "apps/blazor/Emits.razor"),
            [
                "ILogger logger;", "ILoggerFactory factory;", "LoggerMessage.Define();", "ActivitySource source;",
                "System.Diagnostics.Metrics.Meter meter;", "new Meter(name);", "Console.WriteLine(value);",
                "Console.Error.WriteLine(value);", "Console.Out.WriteLine(value);", "Debug.WriteLine(value);",
                "Debug.Print(value);", "Trace.WriteLine(value);", "Trace.TraceError(value);", "EventSource source;",
                "LoggerFactory.Create(configure);", "builder.AddConsole();", "builder.AddDebug();", "builder.AddEventLog();",
                "builder.AddCustomLogger();", "builder.AddOpenTelemetry();", "TracerProvider provider;", "MeterProvider provider;",
                "IMeterFactory factory;", "DiagnosticListener listener;", "Log.Information(value);",
                "JS.InvokeAsync<object>(\"console.error\", value);"
            ]);
            File.WriteAllText(Path.Combine(root, "src/interop.cs"), "JS.InvokeVoidAsync(\"console.log\", value);\n");
            File.WriteAllText(Path.Combine(root, "apps/react/src/ignored.test.tsx"), "console.log(ignored);\n");
            File.WriteAllText(Path.Combine(root, "apps/react/src/emit.js"), "console.log(value);\n");
            File.WriteAllText(Path.Combine(root, "apps/react/src/emit.mjs"), "console.log(value);\n");
            File.WriteAllText(Path.Combine(root, "apps/react/src/emit.cjs"), "console.log(value);\n");
            File.WriteAllText(Path.Combine(root, "apps/react/src/emit.html"), "<script>console.log(value);</script>\n");
            Directory.CreateDirectory(Path.Combine(root, "apps/react/src/dist"));
            Directory.CreateDirectory(Path.Combine(root, "apps/react/src/__tests__"));
            Directory.CreateDirectory(Path.Combine(root, "apps/react/src/features/lib"));
            Directory.CreateDirectory(Path.Combine(root, "apps/blazor/wwwroot/vendor"));
            Directory.CreateDirectory(Path.Combine(root, "apps/blazor/wwwroot/scripts"));
            File.WriteAllText(Path.Combine(root, "apps/react/src/dist/ignored.js"), "console.log(ignored);\n");
            File.WriteAllText(Path.Combine(root, "apps/react/src/__tests__/ignored.js"), "console.log(ignored);\n");
            File.WriteAllText(Path.Combine(root, "apps/react/src/features/lib/emit.ts"), "console.log(value);\n");
            File.WriteAllText(Path.Combine(root, "apps/blazor/wwwroot/vendor/ignored.js"), "console.log(ignored);\n");
            File.WriteAllText(Path.Combine(root, "apps/blazor/wwwroot/blazor.web.js"), "console.log(ignored);\n");
            File.WriteAllText(Path.Combine(root, "apps/blazor/wwwroot/jquery.min.js"), "console.log(ignored);\n");
            File.WriteAllText(Path.Combine(root, "apps/blazor/wwwroot/interop.js"), "console.log(value);\n");
            File.WriteAllText(Path.Combine(root, "apps/blazor/wwwroot/scripts/interop.js"), "console.log(value);\n");

            // Independent literal plants and line numbers implement acceptance items 3 and 5.
            var regressions = new (string Path, string[] Lines, int[] Expected)[]
            {
                ("apps/react/src/jsx.tsx", ["const view = <p>hello</p>; console.log(value); // tail"], [1]),
                ("apps/blazor/ScriptString.razor", ["@code {", "var tag = \"<script>\";", "EventSource source;", "}"], [3]),
                ("apps/blazor/MarkupTick.razor", ["<p>Type ` for code.</p>", "@code { Console.WriteLine(value); }"], [2]),
                ("apps/blazor/Verbatim.razor", ["@code {", "var text = @\"`", "text\";", "Console.WriteLine(value);", "}"], [4]),
                ("apps/react/src/regex.ts", ["const pattern = /[/*]/;", "console.log(value);", "const tick = /`/;", "console.log(value);"], [2, 4]),
                ("apps/react/src/literal.ts", ["const text = `console.log(value);`;", "console.log(after);", "const value = `${console.log(value)}`;"], [1, 2, 3]),
                ("apps/blazor/MarkupComment.razor", ["<p>/*</p>", "@code { Console.WriteLine(value); }"], [2]),
                ("apps/blazor/InlineComment.razor", ["<!-- example --> <script>console.log(value);</script>", "@code { Console.WriteLine(value); }"], [1, 2]),
                ("src/comments.cs", ["// Console.WriteLine(ignored);", "/* Console.WriteLine(ignored); */", "* Console.WriteLine(ignored);", "@* Console.WriteLine(ignored); *@", "/* example */ Console.WriteLine(value);", "@* example *@ Console.WriteLine(value);", "var code = \"console.log(value);\";"], [5, 6, 7])
            };
            foreach (var row in regressions)
                File.WriteAllLines(Path.Combine(root, row.Path), row.Lines);
            Directory.CreateDirectory(Path.Combine(root, "apps/blazor/scripts"));
            File.WriteAllText(Path.Combine(root, "apps/blazor/scripts/build-local-feed.mjs"), "console.log(ignored);");

            var (findings, _) = Scan(root);
            Assert.All(regressions, row => Assert.Equal(row.Expected,
                findings.Where(finding => finding.Path == row.Path).Select(finding => finding.Line)));
            Assert.Equal(new[]
            {
                "apps/blazor/Emits.razor:1", "apps/blazor/Emits.razor:2", "apps/blazor/Emits.razor:3",
                "apps/blazor/Emits.razor:4", "apps/blazor/Emits.razor:5", "apps/blazor/Emits.razor:6",
                "apps/blazor/Emits.razor:7", "apps/blazor/Emits.razor:8", "apps/blazor/Emits.razor:9",
                "apps/blazor/Emits.razor:10", "apps/blazor/Emits.razor:11", "apps/blazor/Emits.razor:12",
                "apps/blazor/Emits.razor:13", "apps/blazor/Emits.razor:14", "apps/blazor/Emits.razor:15",
                "apps/blazor/Emits.razor:16", "apps/blazor/Emits.razor:17", "apps/blazor/Emits.razor:18",
                "apps/blazor/Emits.razor:19", "apps/blazor/Emits.razor:20", "apps/blazor/Emits.razor:21",
                "apps/blazor/Emits.razor:22", "apps/blazor/Emits.razor:23", "apps/blazor/Emits.razor:24",
                "apps/blazor/Emits.razor:25", "apps/blazor/Emits.razor:26",
                "apps/blazor/InlineComment.razor:1", "apps/blazor/InlineComment.razor:2", "apps/blazor/MarkupComment.razor:2", "apps/blazor/MarkupTick.razor:2", "apps/blazor/ScriptString.razor:3", "apps/blazor/Verbatim.razor:4",
                "apps/react/src/emit.cjs:1", "apps/react/src/emit.html:1", "apps/react/src/emit.js:1", "apps/react/src/emit.mjs:1",
                "apps/react/src/emits.ts:1", "apps/react/src/emits.ts:2", "apps/react/src/emits.ts:3", "apps/react/src/emits.ts:4",
                "apps/react/src/emits.ts:6", "apps/react/src/emits.ts:7", "apps/react/src/emits.ts:8",
                "apps/react/src/emits.ts:9", "apps/react/src/emits.ts:10", "apps/react/src/emits.ts:11", "apps/react/src/features/lib/emit.ts:1",
                "apps/react/src/jsx.tsx:1", "apps/react/src/literal.ts:1", "apps/react/src/literal.ts:2", "apps/react/src/literal.ts:3", "apps/react/src/regex.ts:2", "apps/react/src/regex.ts:4",
                "src/comments.cs:5", "src/comments.cs:6", "src/comments.cs:7", "src/interop.cs:1"
            }, findings.Select(finding => $"{finding.Path}:{finding.Line}"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Shell_fence_reports_react_entry_point_emit()
    {
        var root = Path.Combine(Path.GetTempPath(), "harborline", "app104", $"shell-fence-{Guid.NewGuid():N}");
        try
        {
            foreach (var sourceRoot in Roots) Directory.CreateDirectory(Path.Combine(root, sourceRoot));
            File.WriteAllText(Path.Combine(root, "apps/react/index.html"), "<script>console.log(secret);</script>\n");
            var (findings, _) = Scan(root);
            Assert.Equal(new[] { "apps/react/index.html:1" }, findings.Select(finding => $"{finding.Path}:{finding.Line}"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Shell_fence_handles_template_comments_and_interpolation()
    {
        var root = Path.Combine(Path.GetTempPath(), "harborline", "app104", $"shell-fence-{Guid.NewGuid():N}");
        try
        {
            foreach (var sourceRoot in Roots) Directory.CreateDirectory(Path.Combine(root, sourceRoot));
            File.WriteAllLines(Path.Combine(root, "apps/react/src/template.ts"),
                ["const pattern = `/*`;", "console.log(secret);", "const url = `//`; console.log(secret);",
                 "const value = `${console.log(secret)}`;", "/* console.log(hidden);", "console.log(hidden); */"]);
            var (findings, _) = Scan(root);
            Assert.Equal(new[] { "apps/react/src/template.ts:2", "apps/react/src/template.ts:3", "apps/react/src/template.ts:4", "apps/react/src/template.ts:6" },
                findings.Select(finding => $"{finding.Path}:{finding.Line}"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("regex-comment.ts", "const pattern = /[/*]/;")]
    [InlineData("regex-tick.ts", "const tick = /`/;")]
    public void Shell_fence_handles_regex_literals(string name, string firstLine)
    {
        var root = Path.Combine(Path.GetTempPath(), "harborline", "app104", $"shell-fence-{Guid.NewGuid():N}");
        try
        {
            foreach (var sourceRoot in Roots) Directory.CreateDirectory(Path.Combine(root, sourceRoot));
            File.WriteAllLines(Path.Combine(root, "apps/react/src", name), [firstLine, "console.log(secret);"]);
            var (findings, _) = Scan(root);
            Assert.Equal(new[] { (name == "regex-comment.ts" ? "apps/react/src/regex-comment.ts" : "apps/react/src/regex-tick.ts", 2) }, findings);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Shell_fence_handles_html_markup_backticks()
    {
        var root = Path.Combine(Path.GetTempPath(), "harborline", "app104", $"shell-fence-{Guid.NewGuid():N}");
        try
        {
            foreach (var sourceRoot in Roots) Directory.CreateDirectory(Path.Combine(root, sourceRoot));
            File.WriteAllLines(Path.Combine(root, "apps/react/src/help.html"), ["<p>Type ` for code.</p>", "<script>console.log(secret);</script>"]);
            var (findings, _) = Scan(root);
            Assert.Equal(new[] { ("apps/react/src/help.html", 2) }, findings);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static (List<(string Path, int Line)> Findings, Dictionary<string, int> Counts) Scan(string root)
    {
        var findings = new List<(string Path, int Line)>();
        var counts = new Dictionary<string, int>();
        foreach (var sourceRoot in Roots)
        {
            var directory = Path.Combine(root, sourceRoot);
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(file => !Path.GetRelativePath(directory, file).Split(Path.DirectorySeparatorChar).SkipLast(1).Any(ExcludedDirectories.Contains))
                .Where(file => Extensions.Contains(Path.GetExtension(file)) && !Regex.IsMatch(Path.GetFileName(file), @"\.(?:test|spec)\."))
                .Concat(sourceRoot == "apps/react/src" && File.Exists(Path.Combine(root, "apps/react/index.html"))
                    ? [Path.Combine(root, "apps/react/index.html")] : [])
                .OrderBy(file => file, StringComparer.Ordinal).ToArray();
            counts[sourceRoot] = files.Length;
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (var index = 0; index < lines.Length; index++)
                {
                    // Acceptance item 3 is line-based. Never let source syntax hide a later line.
                    var line = lines[index];
                    var comment = Regex.IsMatch(line, @"^\s*(?://|/\*|\*|@\*)", RegexOptions.CultureInvariant);
                    var emitAfterCloser = Regex.Matches(line, @"\*/|\*@", RegexOptions.CultureInvariant)
                        .Any(closer => Emit.IsMatch(line[(closer.Index + closer.Length)..]));
                    if ((!comment && Emit.IsMatch(line)) || (comment && emitAfterCloser))
                        findings.Add((Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'), index + 1));
                }
            }
        }

        return (findings.OrderBy(finding => finding.Path, StringComparer.Ordinal).ThenBy(finding => finding.Line).ToList(), counts);
    }
}