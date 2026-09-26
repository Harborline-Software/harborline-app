import assert from 'node:assert/strict'
import {execFileSync} from 'node:child_process'
import path from 'node:path'
import {test} from 'node:test'
import {configProblems, invocation, mappedSpans, razorLocation, reportCounts} from '../stryker.mjs'

const standard = {project: 'Lib.csproj', since: {enabled: true, target: 'origin/main'}, thresholds: {high: 80, low: 60, break: 60}, reporters: ['json']}
const files = (config, extra = {}) => ({
  'tests/Lib.Tests/Lib.Tests.csproj': '<Project><ItemGroup><PackageReference Include="Microsoft.NET.Test.Sdk" /><ProjectReference Include="../../src/Lib/Lib.csproj" /></ItemGroup></Project>',
  'src/Lib/Lib.csproj': '<Project />',
  ...(config && {'tests/Lib.Tests/stryker-config.json': JSON.stringify({'stryker-config': config})}),
  ...extra,
})
const problems = (fileMap, exclusions = {}) => configProblems({testProjects: ['tests/Lib.Tests/Lib.Tests.csproj'], exclusions, readFile: file => fileMap[file]})

test('a config that holds the standard and names its ProjectReference passes', () => {
  assert.deepEqual(problems(files(standard)), [])
})

test('a test project with neither a config nor an exclusion fails, and an exclusion satisfies it', () => {
  assert.match(problems(files())[0], /no tests\/Lib.Tests\/stryker-config.json and no entry/)
  assert.deepEqual(problems(files(), {'tests/Lib.Tests/Lib.Tests.csproj': 'unmutable because'}), [])
})

test('an exclusion needs a reason and an existing project', () => {
  assert.deepEqual(problems(files(), {'tests/Lib.Tests/Lib.Tests.csproj': ' ', 'src/Gone/Gone.csproj': 'x'}),
    ['eng/stryker-exclusions.json: tests/Lib.Tests/Lib.Tests.csproj has no reason', 'eng/stryker-exclusions.json: src/Gone/Gone.csproj does not exist'])
})

test('a config pointing at a project that is not referenced, or not on disk, fails', () => {
  assert.match(problems(files({...standard, project: 'Other.csproj'})).join('\n'), /"Other.csproj" is not a ProjectReference/)
  const missing = files(standard); delete missing['src/Lib/Lib.csproj']
  assert.match(problems(missing).join('\n'), /project src\/Lib\/Lib.csproj does not exist/)
})

test('a second ProjectReference must be excluded, and the standard is enforced', () => {
  const twoRefs = files(standard, {'tests/Lib.Tests/Lib.Tests.csproj': '<Project><ProjectReference Include="../../src/Lib/Lib.csproj" /><ProjectReference Include="..\\..\\src\\Help\\Help.csproj" /></Project>', 'src/Help/Help.csproj': '<Project />'})
  assert.match(problems(twoRefs).join('\n'), /src\/Help\/Help.csproj is neither mutated nor excluded/)
  assert.deepEqual(problems(twoRefs, {'src/Help/Help.csproj': 'test doubles'}), [])
  assert.deepEqual(problems(files({...standard, thresholds: {high: 80, low: 60, break: 0}, reporters: ['html'], since: {enabled: false}})), [
    'tests/Lib.Tests/stryker-config.json: thresholds must be high 80, low 60, break 60',
    'tests/Lib.Tests/stryker-config.json: reporters must include json',
    'tests/Lib.Tests/stryker-config.json: since must be enabled against origin/main'])
})

test('report counts: 0 tested is visible, and the score is detected over detected plus undetected', () => {
  assert.deepEqual(reportCounts({files: {}}), {total: 0, tested: 0, detected: 0, undetected: 0, score: null})
  const mutants = ['Killed', 'Killed', 'Timeout', 'Survived', 'NoCoverage', 'CompileError', 'Ignored'].map(status => ({status}))
  assert.deepEqual(reportCounts({files: {'a.cs': {mutants}}}), {total: 7, tested: 4, detected: 3, undetected: 2, score: 60})
  assert.equal(reportCounts({files: {'a.cs': {mutants: [{status: 'CompileError'}, {status: 'Ignored'}]}}}).tested, 0)
})

// The shape the RC1 Razor generator emits: #line maps user code back to the .razor file, #line default/hidden is plumbing.
const generated = ['namespace X {', '#line (3,8)-(5,1) "C:\\app\\Shell.razor"', 'var visible = count > 0;', 'count++;', '#line default', '__builder.OpenElement(0, "div");', '#line hidden', '#line 12 "C:\\app\\Shell.razor"', 'Save();', '#line default', '}'].join('\n')

test('mapped spans cover the code #line maps to the .razor file and nothing of the plumbing', () => {
  const spans = mappedSpans(generated)
  assert.deepEqual(spans.map(([start, end]) => generated.slice(start, end + 1)), ['var visible = count > 0;\ncount++;\n', 'Save();\n'])
})

test('a generated line maps back to its .razor line, and plumbing maps to nothing', () => {
  assert.equal(razorLocation(generated, 3), 'Shell.razor:3')
  assert.equal(razorLocation(generated, 4), 'Shell.razor:4')
  assert.equal(razorLocation(generated, 9), 'Shell.razor:12')
  assert.equal(razorLocation(generated, 6), undefined)
})

test('Stryker on a Razor project always runs with the opt-in property; a plain project never does', () => {
  const config = {project: 'Host.csproj', since: {enabled: true, target: 'origin/main'}}
  for (const all of [false, true]) {
    const razor = invocation({config, razor: true, all, handWritten: ['Program.cs'], copies: [{path: 'Shell_razor.cs', text: generated}]})
    assert.equal(razor.env.HarborlineStrykerBuild, 'true')
    assert.equal(razor.config.since.enabled, false)
    const [start, end] = mappedSpans(generated)[0]
    assert.deepEqual(razor.config.mutate.slice(0, 1), ['**/Program.cs'])
    assert.ok(razor.config.mutate[1].startsWith(`**/obj/stryker-razor/Shell_razor.cs{${start}..${end}}`))
    assert.equal(invocation({config, razor: false, all}).env.HarborlineStrykerBuild, undefined)
  }
  assert.deepEqual(invocation({config, razor: false, all: false}), {env: {}, config: undefined})
})

test('this repository passes the check', () => {
  const root = path.resolve(import.meta.dirname, '../..')
  execFileSync(process.execPath, [path.join(root, 'eng/stryker.mjs'), 'check'], {cwd: root, stdio: 'pipe'})
})
