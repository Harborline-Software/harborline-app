// Stryker.NET across every .NET test project (control PROC-0002, "Mutation evidence as an artefact").
//   node eng/stryker.mjs check   every test project has a stryker-config.json or an entry in eng/stryker-exclusions.json,
//                                every config names one of its test project's ProjectReferences and holds the standard
//   node eng/stryker.mjs run     check, then mutate each configured project whose .cs source differs from origin/main
//                                (the config's since mode), and fail unless the json report shows mutants tested
// Stryker's exit code is not evidence: 5.0.0 exits 0 having mutated nothing (control T-720 spike), so `run` reads the report.
import {execFileSync, spawnSync} from 'node:child_process'
import {existsSync, readFileSync, rmSync} from 'node:fs'
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

function run(repo) {
  let failed = false
  for (const test of repo.testProjects.filter(project => !(project in repo.exclusions))) {
    const testDirectory = path.posix.dirname(test)
    const config = JSON.parse(read(`${testDirectory}/stryker-config.json`))['stryker-config']
    const target = references(test, read(test)).find(ref => path.posix.basename(ref) === config.project)
    // ponytail: a changed .cs file with nothing mutable in it (an interface, a comment) will fail the assertion below; judge it by the report.
    const changed = git('diff', '--name-only', 'origin/main', '--', `${path.posix.dirname(target)}/*.cs`)
    if (!changed.length) { console.log(`${test}: no source change in ${path.posix.dirname(target)} since origin/main, skipped`); continue }
    const output = path.join(root, 'StrykerOutput', path.posix.basename(testDirectory))
    rmSync(output, {recursive: true, force: true})
    const stryker = spawnSync('dotnet', ['stryker', '--output', output, ...msbuildArgs()], {cwd: path.join(root, testDirectory), stdio: 'inherit'})
    const reportPath = path.join(output, 'reports', 'mutation-report.json')
    const counts = existsSync(reportPath) ? reportCounts(JSON.parse(readFileSync(reportPath, 'utf8'))) : undefined
    console.log(`${test}: exit ${stryker.status}, ${JSON.stringify(counts ?? 'no json report')}`)
    if (!counts?.tested) { console.error(`${test}: ${changed.length} changed source file(s) but 0 mutants tested`); failed = true }
    if (stryker.status !== 0) failed = true
  }
  return failed
}

if (import.meta.main) {
  const [command] = process.argv.slice(2)
  if (command !== 'check' && command !== 'run') throw new Error('usage: node eng/stryker.mjs check|run')
  const repo = repository(), problems = configProblems(repo)
  problems.forEach(problem => console.error(problem))
  if (problems.length) process.exit(1)
  console.log(`Stryker config: ${repo.testProjects.length} test project(s), each configured or excluded: PASS`)
  if (command === 'run' && run(repo)) process.exit(1)
}
