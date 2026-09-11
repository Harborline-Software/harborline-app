#!/usr/bin/env node
// Local-verification receipt for harborline-app. Same shape as harborline-api's eng/verify-receipt.mjs
// and harborline-platform's tooling/verify-phase4-receipt.mjs — deliberately, so the three
// repositories fail the same way and a developer learns the pattern once.
//
// Why this exists: GitHub Actions is switched off here (see the ACTIONS_ENABLED block at the top of
// .github/workflows/packages.yml — free plan, private-repo minutes exhausted 2026-08-24). With CI
// off, nothing verified this repository at all. eng/verify.sh is the replacement; this is what stops
// it from being optional.
//
// The receipt lives inside .git/, NOT in the tree: a tracked receipt would change the tree it
// attests to and could never match itself.
//
//   node eng/verify-receipt.mjs --record <step-id>...   # written by eng/verify.sh on success
//   node eng/verify-receipt.mjs                         # verify; the pre-push hook calls this
import {execFileSync} from 'node:child_process'
import {existsSync, readFileSync, writeFileSync} from 'node:fs'
import path from 'node:path'
import {coverageRecord, receiptCoverage} from './coverage-receipt.mjs'

const REPOSITORY = 'harborline-app'
const SCHEMA_VERSION = 1

// Must stay in sync with the steps in eng/verify.sh. Recording refuses a receipt missing any of
// these, so removing a step there fails loudly instead of quietly narrowing the gate.
export const requiredStepIds = [
  'boundaries',
  'react-typecheck',
  'react-test',
  'react-build',
  'dotnet-test',
  'dotnet-coverage',
  'packages',
  'host-manifest',
]

const root = execFileSync('git', ['rev-parse', '--show-toplevel'], {encoding: 'utf8'}).trim()
const git = (...args) => execFileSync('git', ['-C', root, ...args], {encoding: 'utf8'}).trim()
const receiptPath = path.resolve(root, git('rev-parse', '--git-common-dir'), 'harborline-app-verify-receipt.json')

const head = git('rev-parse', 'HEAD')
const tree = git('rev-parse', 'HEAD^{tree}')

if (process.argv.includes('--record')) {
  // The receipt attests to HEAD's TREE, but eng/verify.sh runs against the WORKING tree. On a dirty
  // tree those are different things, and the receipt would vouch for code the run never saw. Refuse,
  // rather than record a claim that is quietly false.
  const dirty = git('status', '--porcelain')
  if (dirty) {
    console.error('refusing to record a receipt from a dirty working tree - commit first, then verify.')
    console.error('The receipt attests to HEAD; uncommitted changes were not what the run tested:')
    console.error(dirty.split(String.fromCharCode(10)).slice(0, 10).map(line => '  ' + line).join(String.fromCharCode(10)))
    process.exit(1)
  }
  const recordArgs = process.argv.slice(process.argv.indexOf('--record') + 1)
  const optionIndex = recordArgs.findIndex(argument => argument.startsWith('--'))
  const passed = (optionIndex < 0 ? recordArgs : recordArgs.slice(0, optionIndex))
  const coverageEnabled = process.env.HARBORLINE_GATE_COVERAGE === '1'
  const required = coverageEnabled ? requiredStepIds : requiredStepIds.filter(id => id !== 'dotnet-coverage')
  const missing = required.filter(id => !passed.includes(id))
  if (missing.length > 0) {
    console.error(`refusing to record a receipt missing: ${missing.join(', ')}`)
    process.exit(1)
  }
  const coverageSummaryIndex = recordArgs.indexOf('--coverage-summary')
  const coverage = receiptCoverage({
    enabled: coverageEnabled,
    summary: coverageEnabled && coverageSummaryIndex >= 0 ? JSON.parse(readFileSync(recordArgs[coverageSummaryIndex + 1] ?? '', 'utf8')) : undefined,
  })
  writeFileSync(receiptPath, JSON.stringify({
    schemaVersion: SCHEMA_VERSION,
    repository: REPOSITORY,
    baseHead: head,
    testedTree: tree,
    steps: passed,
    ...(coverage ? {coverage} : {}),
    recordedAt: new Date().toISOString(),
  }, null, 2) + '\n')
  console.log(`recorded verification receipt for ${head.slice(0, 12)} (tree ${tree.slice(0, 12)})`)
  process.exit(0)
}

const refuse = (why) => {
  console.error(`\n  ${REPOSITORY}: refusing to push — ${why}`)
  console.error('\n  GitHub Actions is off in this repository, so this receipt is the only thing that')
  console.error('  verifies the commits you are pushing. Run:\n')
  console.error('      bash eng/verify.sh\n')
  console.error('  and push again. To push anyway (and own that nothing checked it): git push --no-verify\n')
  process.exit(1)
}

if (!existsSync(receiptPath)) refuse('no verification receipt exists')

let receipt
try {
  receipt = JSON.parse(readFileSync(receiptPath, 'utf8'))
} catch {
  refuse('the verification receipt is not readable JSON')
}

if (receipt.schemaVersion !== SCHEMA_VERSION) refuse(`receipt schemaVersion ${receipt.schemaVersion}, expected ${SCHEMA_VERSION}`)
if (receipt.repository !== REPOSITORY) refuse(`receipt is for ${receipt.repository}, not ${REPOSITORY}`)
if (receipt.testedTree !== tree) {
  refuse(`the receipt attests to tree ${String(receipt.testedTree).slice(0, 12)}, but HEAD's tree is ${tree.slice(0, 12)}`)
}
if (receipt.baseHead !== head) {
  refuse(`the receipt attests to commit ${String(receipt.baseHead).slice(0, 12)}, but HEAD is ${head.slice(0, 12)}`)
}

const required = process.env.HARBORLINE_GATE_COVERAGE === '1' ? requiredStepIds : requiredStepIds.filter(id => id !== 'dotnet-coverage')
const missing = required.filter(id => !(receipt.steps ?? []).includes(id))
if (missing.length > 0) refuse(`the receipt does not cover: ${missing.join(', ')}`)
if (process.env.HARBORLINE_GATE_COVERAGE === '1') {
  try {
    if (!Array.isArray(receipt.coverage) || receipt.coverage.length === 0) throw new Error('receipt is missing coverage')
    receipt.coverage.forEach(coverageRecord)
  } catch (error) {
    refuse(error instanceof Error ? error.message : String(error))
  }
}

console.log(`${REPOSITORY}: verification receipt matches HEAD ${head.slice(0, 12)} — ${receipt.steps.length} steps`)
