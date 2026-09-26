// Stryker.NET across every .NET test project (control PROC-0002, "Mutation evidence as an artefact").
//   node eng/stryker.mjs check        every test project has a stryker-config.json or an entry in eng/stryker-exclusions.json,
//                                     every config names one of its test project's ProjectReferences and holds the standard
//   node eng/stryker.mjs run [--all]  check, then mutate each configured project whose .cs or .razor source differs from
//                                     origin/main (--all: every project, every file), and fail unless the json report shows
//                                     mutants tested
// Stryker's exit code is not evidence: 5.0.0 exits 0 having mutated nothing (control T-720 spike), so `run` reads the report.
import {execFileSync, spawnSync} from 'node:child_process'
import {existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync} from 'node:fs'
import path from 'node:path'

const root = path.resolve(import.meta.dirname, '..')
const exclusionsFile = 'eng/stryker-exclusions.json'
const git = (...args) => execFileSync('git', ['-C', root, ...args], {encoding: 'utf8'}).split(/\r?\n/).filter(Boolean)
const read = file => existsSync(path.join(root, file)) ? readFileSync(path.join(root, file), 'utf8') : undefined

// Status names from the mutation-testing-report schema; the score is Stryker's own: detected / (detected + undetected).
export function reportCounts(report) {
  const mutants = Object.values(report.files ?? {}).flatMap(file => file.mutants ?? [])
  const count = (...statuses) => mutants.filter(mutant => statuses.includes(mutant.status)).length
  const detected = count('Killed', 'Timeout'), undetected = count('Survived', 'NoCoverage')
  return {total: mutants.length, tested: count('Killed', 'Survived', 'Timeout', 'RuntimeError'), detected, undetected,
    score: detected + undetected ? Math.floor(10000 * detected / (detected + undetected)) / 100 : null}
}

const references = (testProject, text) => [...text.matchAll(/<ProjectReference\s+Include="([^"]+)"/g)]
  .map(match => path.posix.normalize(`${path.posix.dirname(testProject)}/${match[1].replaceAll('\\', '/')}`))

// testProjects: repository-relative csproj paths; readFile(path) returns the text or undefined when absent.
export function configProblems({testProjects, exclusions, readFile}) {
  const problems = []
  for (const [project, reason] of Object.entries(exclusions)) {
    if (readFile(project) === undefined) problems.push(`${exclusionsFile}: ${project} does not exist`)
    if (!String(reason ?? '').trim()) problems.push(`${exclusionsFile}: ${project} has no reason`)
  }
  for (const test of testProjects) {
    const configPath = `${path.posix.dirname(test)}/stryker-config.json`, raw = readFile(configPath)
    if (raw === undefined) {
      if (!(test in exclusions)) problems.push(`${test}: no ${configPath} and no entry in ${exclusionsFile}`)
      continue
    }
    if (test in exclusions) problems.push(`${test}: has both ${configPath} and an exclusion`)
    const config = JSON.parse(raw)['stryker-config'] ?? {}
    const refs = references(test, readFile(test))
    const target = refs.find(ref => path.posix.basename(ref) === config.project)
    if (!target) problems.push(`${configPath}: project "${config.project}" is not a ProjectReference of ${test}`)
    else if (readFile(target) === undefined) problems.push(`${configPath}: project ${target} does not exist`)
    for (const ref of refs) if (ref !== target && !(ref in exclusions)) problems.push(`${test}: ProjectReference ${ref} is neither mutated nor excluded`)
    const {high, low, break: breakAt} = config.thresholds ?? {}
    if (high !== 80 || low !== 60 || breakAt !== 60) problems.push(`${configPath}: thresholds must be high 80, low 60, break 60`)
    if (!config.reporters?.includes('json')) problems.push(`${configPath}: reporters must include json`)
    if (config.since?.enabled !== true || config.since?.target !== 'origin/main') problems.push(`${configPath}: since must be enabled against origin/main`)
  }
  return problems
}

function repository() {
  const testProjects = git('ls-files', '*.csproj').filter(file => read(file).includes('Microsoft.NET.Test.Sdk'))
  return {testProjects, exclusions: JSON.parse(read(exclusionsFile)), readFile: read}
}

// Buildalyzer reads TargetFramework literally from the csproj; ours comes from Directory.Build.props, so it guesses
// .NET Framework and, on a host with Visual Studio Build Tools (winbox), runs that MSBuild.exe, which cannot resolve
// Microsoft.NET.Sdk and Stryker reports "No project found" (stryker-net#3758). Point it at the global.json SDK's MSBuild.
function msbuildArgs() {
  if (process.platform !== 'win32') return []
  const version = execFileSync('dotnet', ['--version'], {cwd: root, encoding: 'utf8'}).trim()
  const line = execFileSync('dotnet', ['--list-sdks'], {cwd: root, encoding: 'utf8'}).split(/\r?\n/).find(sdk => sdk.startsWith(`${version} `))
  const directory = /\[(.*)\]/.exec(line ?? '')?.[1]
  if (!directory) throw new Error(`dotnet --list-sdks does not list ${version}`)
  return ['--msbuild-path', path.join(directory, version, 'MSBuild.exe')]
}

// Razor under Stryker 5.0.0 (stryker-net#3813): its Roslyn 5.9 cannot load the RC1 Razor generator (needs 5.11), so we
// build once normally with the generator's output emitted, copy that output to obj/stryker-razor as plain C#, and
// Directory.Build.targets compiles the copies instead of running the generator when HarborlineStrykerBuild=true.
// Delete this path, and that target, when a Stryker release carries Roslyn 5.11+.
const razorGenerator = 'Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator'
const walk = dir => existsSync(dir) ? readdirSync(dir, {withFileTypes: true}).flatMap(entry => entry.isDirectory() ? walk(path.join(dir, entry.name)) : [path.join(dir, entry.name)]) : []
const lineDirective = /^\s*#line\s+(?:\((\d+),\d+\)-\(\d+,\d+\)(?:\s+\d+)?|(\d+))\s+"([^"]+)"/

// Character spans of the code a #line directive maps back to a .razor file: @code blocks and markup expressions.
// Everything else in the generated file is render-tree plumbing, and a mutant must sit wholly inside a span to be kept.
export function mappedSpans(text) {
  const spans = []
  let offset = 0, start
  for (const line of text.split('\n')) {
    if (/^\s*#line\b/.test(line)) {
      if (start !== undefined && offset > start) spans.push([start, offset - 1])
      start = lineDirective.test(line) ? offset + line.length + 1 : undefined
    }
    offset += line.length + 1
  }
  return spans
}

// Maps a 1-based line of a generated copy back to "<file>.razor:<line>", or undefined when it is plumbing.
export function razorLocation(text, line) {
  const lines = text.split('\n')
  for (let index = line - 2; index >= 0; index--) {
    if (!/^\s*#line\b/.test(lines[index])) continue
    const match = lineDirective.exec(lines[index])
    return match ? `${path.basename(match[3].replaceAll('\\', '/'))}:${Number(match[1] ?? match[2]) + (line - 2 - index)}` : undefined
  }
}

// What Stryker is started with. A Razor project gets the opt-in property and a config whose mutate list keeps
// hand-written .cs whole and generated code only inside mapped spans. Since is off there because the copies are
// untracked, so Stryker's own diff would ignore every one of them; run()'s changed-file list plays its part instead.
export function invocation({config, razor, all, handWritten = [], copies = []}) {
  if (!razor && !all) return {env: {}, config: undefined}
  const mutate = razor ? [...handWritten.map(file => `**/${file}`),
    ...copies.map(copy => ({copy, spans: mappedSpans(copy.text)})).filter(({spans}) => spans.length)
      .map(({copy, spans}) => `**/obj/stryker-razor/${copy.path}${spans.map(([start, end]) => `{${start}..${end}}`).join('')}`)] : undefined
  return {env: razor ? {HarborlineStrykerBuild: 'true'} : {}, config: {...config, since: {enabled: false}, ...(mutate && {mutate})}}
}

function prepareRazor(projectFile, keep) {
  const projectDirectory = path.join(root, path.dirname(projectFile))
  const emitted = path.join(projectDirectory, 'obj', 'stryker-razor-emit'), copies = path.join(projectDirectory, 'obj', 'stryker-razor')
  rmSync(emitted, {recursive: true, force: true}); rmSync(copies, {recursive: true, force: true})
  const {HarborlineStrykerBuild, ...env} = process.env
  execFileSync('dotnet', ['build', path.join(root, projectFile), '-p:EmitCompilerGeneratedFiles=true', `-p:CompilerGeneratedFilesOutputPath=${emitted}`], {env, stdio: 'inherit'})
  const source = path.join(emitted, razorGenerator)
  return walk(source).map(file => {
    const relative = path.relative(source, file).replaceAll('\\', '/').replace(/\.g\.cs$/, '.cs')
    // Stryker never mutates a file marked <auto-generated/> or named *.g.cs; a BOM would shift every span by one.
    const text = readFileSync(file, 'utf8').replace(/^﻿/, '').replace(/^\/\/ <auto-generated\/>\r?\n/m, '')
    mkdirSync(path.dirname(path.join(copies, relative)), {recursive: true})
    writeFileSync(path.join(copies, relative), text)
    return {path: relative, text, razor: relative.replace(/_razor\.cs$/, '.razor')}
  }).filter(copy => keep(copy.razor))
}

function run(repo, {all}) {
  let failed = false
  for (const test of repo.testProjects.filter(project => !(project in repo.exclusions))) {
    const testDirectory = path.posix.dirname(test)
    const config = JSON.parse(read(`${testDirectory}/stryker-config.json`))['stryker-config']
    const target = references(test, read(test)).find(ref => path.posix.basename(ref) === config.project)
    const targetDirectory = path.posix.dirname(target), relative = file => path.posix.relative(targetDirectory, file)
    const razor = git('ls-files', '--', `${targetDirectory}/*.razor`).length > 0
    // ponytail: a changed file with nothing mutable in it (an interface, a comment) will fail the assertion below; judge it by the report.
    const changed = all ? undefined : git('diff', '--name-only', 'origin/main', '--', `${targetDirectory}/*.cs`, `${targetDirectory}/*.razor`).map(relative)
    if (changed && !changed.length) { console.log(`${test}: no source change in ${targetDirectory} since origin/main, skipped`); continue }
    const keep = file => !changed || changed.includes(file)
    const copies = razor ? prepareRazor(target, keep) : []
    const handWritten = git('ls-files', '--', `${targetDirectory}/*.cs`).map(relative).filter(keep)
    const start = invocation({config, razor, all, handWritten, copies})
    const output = path.join(root, 'StrykerOutput', path.posix.basename(testDirectory))
    rmSync(output, {recursive: true, force: true}); mkdirSync(output, {recursive: true})
    const args = ['stryker', '--output', output, ...msbuildArgs()]
    if (start.config) {
      writeFileSync(path.join(output, 'stryker-config.json'), JSON.stringify({'stryker-config': start.config}, null, 2))
      args.push('--config-file', path.join(output, 'stryker-config.json'))
    }
    const stryker = spawnSync('dotnet', args, {cwd: path.join(root, testDirectory), stdio: 'inherit', env: {...process.env, ...start.env}})
    const reportPath = path.join(output, 'reports', 'mutation-report.json')
    const report = existsSync(reportPath) ? JSON.parse(readFileSync(reportPath, 'utf8')) : undefined
    const counts = report && reportCounts(report)
    console.log(`${test}: exit ${stryker.status}, ${JSON.stringify(counts ?? 'no json report')}`)
    for (const [file, {mutants}] of Object.entries(report?.files ?? {})) {
      const copy = copies.find(candidate => file.replaceAll('\\', '/').endsWith(`stryker-razor/${candidate.path}`))
      for (const mutant of copy ? mutants.filter(m => m.status === 'Survived') : [])
        console.log(`  survived ${razorLocation(copy.text, mutant.location.start.line) ?? copy.path}: ${mutant.mutatorName} -> ${mutant.replacement}`)
    }
    if (!counts?.tested) { console.error(`${test}: ${changed ? `${changed.length} changed source file(s)` : 'a full run'} but 0 mutants tested`); failed = true }
    if (stryker.status !== 0) failed = true
  }
  return failed
}

if (import.meta.main) {
  const [command, flag] = process.argv.slice(2)
  if (command !== 'check' && command !== 'run') throw new Error('usage: node eng/stryker.mjs check | run [--all]')
  const repo = repository(), problems = configProblems(repo)
  problems.forEach(problem => console.error(problem))
  if (problems.length) process.exit(1)
  console.log(`Stryker config: ${repo.testProjects.length} test project(s), each configured or excluded: PASS`)
  if (command === 'run' && run(repo, {all: flag === '--all'})) process.exit(1)
}
