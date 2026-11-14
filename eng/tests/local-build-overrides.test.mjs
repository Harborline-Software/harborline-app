import assert from 'node:assert/strict'
import {mkdirSync, mkdtempSync, rmSync, writeFileSync} from 'node:fs'
import {tmpdir} from 'node:os'
import path from 'node:path'
import {test} from 'node:test'
import {findLocalBuildOverrides, isLocalBuildOverride} from '../local-build-overrides.mjs'

// Expected names are literals from MSBuild's <Project>.csproj.user import and Vite's documented env-file
// order (.env.local, .env.[mode].local), not from the module's own patterns.
test('names MSBuild and Vite treat as local build inputs are overrides', () => {
  for (const name of ['Harborline.App.csproj.user', 'Tool.fsproj.user', 'X.CSPROJ.USER', '.env.local', '.env.production.local', '.env.development.local']) {
    assert.equal(isLocalBuildOverride(name), true, name)
  }
})

test('tracked env files and per-user IDE files that never reach the build are not overrides', () => {
  for (const name of ['.env', '.env.development', '.env.example', '.env.localhost', 'Harborline.App.csproj', 'Harborline.sln.DotSettings.user', 'x.suo', 'settings.user', 'a.local']) {
    assert.equal(isLocalBuildOverride(name), false, name)
  }
})

test('the scan finds overrides anywhere in the tree, skips dependency and build output, and ignores git', () => {
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
