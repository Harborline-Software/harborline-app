import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
import {test} from 'node:test'
import {runInNewContext} from 'node:vm'

const workflow = name => readFileSync(new URL(`../../.github/workflows/${name}.yml`, import.meta.url), 'utf8').replaceAll('\r', '')
const packages = workflow('packages')
// Evaluate the actual workflow's small expression subset. Literal scenarios below
// are the oracle: superseded PR feedback may cancel; distinct evidence/publication
// runs must never share a group, even when they are pending (GitHub concurrency docs).
function expression(key, github) {
  const value = packages.match(new RegExp(`^  ${key}: (.+)$`, 'm'))?.[1]
  assert.ok(value, `missing ${key}`)
  return value.replace(/\$\{\{\s*(.*?)\s*\}\}/g, (_, code) => String(runInNewContext(code, {github})))
}
function context(event_name, number, run_id = 1) {
  return {event_name, run_id, event: {pull_request: {number}}, ref: 'refs/heads/main'}
}

test('same PR cancels superseded work; draft and stacked PRs remain independent', () => {
  for (const flags of [{draft: false}, {draft: true}, {draft: false, base: {ref: 'feature/parent'}}]) {
    const first = context('pull_request', 70, 1)
    Object.assign(first.event.pull_request, flags)
    const next = {...first, run_id: 2}
    assert.equal(expression('group', first), 'packages-70')
    assert.equal(expression('group', next), 'packages-70')
    assert.equal(expression('cancel-in-progress', first), 'true')
    assert.equal(expression('group', context('pull_request', 71)), 'packages-71')
  }
})

test('main, queue, tag, release and dispatch never cancel or replace each other', () => {
  const groups = []
  for (const event of ['push', 'merge_group', 'release', 'workflow_dispatch']) {
    for (const ref of ['refs/heads/main', 'refs/tags/v0.1.0-preview.1']) {
      for (const run of [1, 2]) {
        const github = {...context(event, undefined, groups.length + 100), ref}
        assert.equal(expression('cancel-in-progress', github), 'false')
        groups.push(expression('group', github))
      }
    }
  }
  assert.equal(new Set(groups).size, groups.length)
})

test('package triggers and both Linux proof owners remain intact', () => {
  const triggers = packages.slice(packages.indexOf('\non:\n'), packages.indexOf('\nconcurrency:'))
  for (const event of ['pull_request', 'push', 'workflow_dispatch']) assert.match(triggers, new RegExp(`^  ${event}:`, 'm'))
  for (const event of ['merge_group', 'release']) assert.doesNotMatch(triggers, new RegExp(`^  ${event}:`, 'm'))
  assert.match(triggers, /branches: \[main\]/)
  assert.match(triggers, /tags: \["v\*-preview\.\*", "v\*-alpha\.\*", "v\*-beta\.\*", "v\*-rc\.\*"\]/)
  for (const job of ['react', 'pack-consume-publish']) {
    assert.match(packages, new RegExp(`^  ${job}:\\n    if: vars.ACTIONS_ENABLED == 'true'\\n    runs-on: ubuntu-latest`, 'm'))
  }
  for (const command of ['pnpm install --frozen-lockfile', 'pnpm run typecheck', 'pnpm test', 'pnpm run build', 'dotnet test Harborline.App.slnx -c Release', 'bash eng/verify-packages.sh', 'npm run validate']) assert.ok(packages.includes(command), command)
  assert.match(packages, /node-version: "22"/)
  assert.match(packages, /global-json-file: global.json/)
  assert.match(packages, /path: \|\n            artifacts\/packages\/\*\.nupkg\n            artifacts\/packages\/consumer-proof\.json\n            artifacts\/packages\/package-manifest\.json/)
  assert.equal(packages.match(/if: startsWith\(github.ref, 'refs\/tags\/'\) \|\| inputs.publish == true/g)?.length, 3)
  assert.match(packages, /packageDistributionAuthority/)
  assert.match(packages, /Stable package publication is not authorized/)
})

test('required verify and SBOM still report on PR and merge queue', () => {
  for (const name of ['verify', 'license-sbom']) {
    const source = workflow(name)
    assert.match(source, /^  pull_request:/m)
    assert.match(source, /^  merge_group:/m)
  }
  assert.match(workflow('verify'), /needs: \[verify-shared\]\n    if: always\(\)/)
  assert.match(workflow('verify'), /github.event.pull_request.draft == false/)
  assert.match(workflow('license-sbom'), /  sbom:\n    runs-on: ubuntu-latest/)
})
