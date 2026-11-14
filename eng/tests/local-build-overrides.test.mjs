import assert from 'node:assert/strict'
import {spawnSync} from 'node:child_process'
import {copyFileSync, existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync} from 'node:fs'
import {tmpdir} from 'node:os'
import path from 'node:path'
import {fileURLToPath} from 'node:url'
import {test} from 'node:test'
import {findLocalBuildOverrides, isLocalBuildOverride} from '../local-build-overrides.mjs'

// Expected names are literals from MSBuild's <Project>.csproj.user import and Vite's documented env-file
// order (.env.local, .env.[mode].local), not from the module's own patterns.
test('names MSBuild and Vite treat as local build inputs are overrides', () => {
  for (const name of ['Harborline.App.csproj.user', 'Tool.fsproj.user', 'X.CSPROJ.USER', 'build.proj.user', '.env.local', '.env.production.local', '.env.development.local', '.ENV.LOCAL']) {
    assert.equal(isLocalBuildOverride(name), true, name)
  }
})

test('tracked env files and per-user IDE files that never reach the build are not overrides', () => {
  for (const name of ['.env', '.env.development', '.env.example', '.env.localhost', 'Harborline.App.csproj', 'Harborline.sln.DotSettings.user', 'x.suo', 'settings.user', 'a.local']) {
    assert.equal(isLocalBuildOverride(name), false, name)
  }
})

test('the scan finds overrides anywhere in the tree, skips dependency, build output and agent worktrees, and ignores git', () => {
  const root = mkdtempSync(path.join(tmpdir(), 'local-build-overrides-'))
  try {
    const put = (relative) => {
      mkdirSync(path.dirname(path.join(root, relative)), {recursive: true})
      writeFileSync(path.join(root, relative), '')
    }
    // A .gitignore that hides them must not hide them from the scan.
    put('.gitignore')
    writeFileSync(path.join(root, '.gitignore'), '*.user\n.env.local\n.env.*.local\n')
    put('apps/react/.env.local')
    put('apps/react/.env.production.local')
    put('apps/react/.env.development')
    put('src/Harborline.App/Harborline.App.csproj.user')
    put('apps/react/node_modules/pkg/.env.local')
    put('src/Harborline.App/obj/Harborline.App.csproj.user')
    put('src/Harborline.App/bin/Release/.env.local')
    // Agent worktrees are other checkouts; their overrides change their builds, not this one.
    put('.claude/worktrees/lane-1/apps/react/.env.local')
    put('.codex/worktrees/lane-2/src/Harborline.App/Harborline.App.csproj.user')
    assert.deepEqual(findLocalBuildOverrides(root), [
      'apps/react/.env.local',
      'apps/react/.env.production.local',
      'src/Harborline.App/Harborline.App.csproj.user',
    ])
  } finally {
    rmSync(root, {recursive: true, force: true})
  }
})

test('a clean tree has no overrides', () => {
  const root = mkdtempSync(path.join(tmpdir(), 'local-build-overrides-'))
  try {
    mkdirSync(path.join(root, 'apps/react'), {recursive: true})
    writeFileSync(path.join(root, 'apps/react/.env.development'), '')
    assert.deepEqual(findLocalBuildOverrides(root), [])
  } finally {
    rmSync(root, {recursive: true, force: true})
  }
})

// The wiring: --record must refuse while an ignored override exists, though `git status` is clean, and
// record once it is gone. Runs the real script in a throwaway repository.
test('verify-receipt --record refuses while an ignored override is on disk, and records once it is gone', () => {
  const engDirectory = path.dirname(path.dirname(fileURLToPath(import.meta.url)))
  const root = mkdtempSync(path.join(tmpdir(), 'verify-receipt-overrides-'))
  try {
    mkdirSync(path.join(root, 'eng'))
    for (const file of ['verify-receipt.mjs', 'coverage-receipt.mjs', 'local-build-overrides.mjs']) {
      copyFileSync(path.join(engDirectory, file), path.join(root, 'eng', file))
    }
    writeFileSync(path.join(root, '.gitignore'), '.env.local\n')
    // An empty global config, so the developer's own ignore file and settings play no part.
    const globalConfig = path.join(tmpdir(), `${path.basename(root)}.gitconfig`)
    writeFileSync(globalConfig, '')
    const environment = {...process.env, GIT_CONFIG_GLOBAL: globalConfig, GIT_CONFIG_NOSYSTEM: '1', HARBORLINE_GATE_COVERAGE: '0'}
    const gitIn = (...args) => spawnSync('git', ['-c', 'user.name=test', '-c', 'user.email=test@example.invalid', ...args], {cwd: root, env: environment, encoding: 'utf8'})
    assert.equal(gitIn('init', '-q').status, 0)
    assert.equal(gitIn('add', '-A').status, 0)
    assert.equal(gitIn('commit', '-q', '-m', 'fixture').status, 0)
    mkdirSync(path.join(root, 'apps/react'), {recursive: true})
    writeFileSync(path.join(root, 'apps/react/.env.local'), 'LOCAL_NODE_SESSION_TOKEN=x\n')
    assert.equal(gitIn('status', '--porcelain').stdout, '')

    // The step ids eng/verify.sh passes, written out here rather than read from the script.
    const steps = ['boundaries', 'dependency-ledger', 'react-typecheck', 'react-test', 'react-build', 'dotnet-test', 'packages', 'host-manifest']
    const record = () => spawnSync(process.execPath, ['eng/verify-receipt.mjs', '--record', ...steps], {cwd: root, env: environment, encoding: 'utf8'})
    const receipt = path.join(root, '.git', 'harborline-app-verify-receipt.json')

    const refused = record()
    assert.equal(refused.status, 1, refused.stderr)
    assert.match(refused.stderr, /apps\/react\/\.env\.local/)
    assert.equal(existsSync(receipt), false)

    rmSync(path.join(root, 'apps/react/.env.local'))
    const recorded = record()
    assert.equal(recorded.status, 0, recorded.stderr)
    assert.equal(existsSync(receipt), true)
  } finally {
    rmSync(root, {recursive: true, force: true})
    rmSync(path.join(tmpdir(), `${path.basename(root)}.gitconfig`), {force: true})
  }
})
