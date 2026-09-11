import assert from 'node:assert/strict'
import {mkdtempSync, mkdirSync, rmSync, writeFileSync} from 'node:fs'
import {tmpdir} from 'node:os'
import path from 'node:path'
import {test} from 'node:test'
import {coverageSummary, summarizeCoberturaReports} from '../coverage.mjs'
import {receiptCoverage} from '../coverage-receipt.mjs'

// The repository itself is the fixture: eng/verify.sh is tracked, and the report names it relative to a
// <source> root the way coverlet does; missing.cs maps to nothing and must be listed, not dropped.
test('Cobertura union counts lines once, maps through the source root and lists unmapped paths', () => {
  const root = path.resolve(import.meta.dirname, '../..')
  const xml = '<coverage><sources><source>' + path.join(root, 'eng') + '/</source></sources><packages><package><classes>'
    + '<class filename="verify.sh"><lines><line number="1" hits="0"/><line number="2" hits="1"/></lines></class>'
    + '<class filename="verify.sh"><lines><line number="1" hits="2"/></lines></class>'
    + '<class filename="missing.cs"><lines><line number="9" hits="0"/></lines></class></classes></package></packages></coverage>'
  assert.deepEqual(coverageSummary(xml, root), {coveredLines: 2, validLines: 3, mappedPaths: ['eng/verify.sh'], unmappedPaths: ['missing.cs']})
})

test('a repository-relative filename still maps without a source root', () => {
  const root = path.resolve(import.meta.dirname, '../..')
  const xml = '<coverage><packages><package><classes><class filename="eng/verify.sh"><lines><line number="1" hits="1"/></lines></class></classes></package></packages></coverage>'
  assert.deepEqual(coverageSummary(xml, root).mappedPaths, ['eng/verify.sh'])
})

test('coverage unions every source report and the receipt names its artifacts and counts', () => {
  const root = path.resolve(import.meta.dirname, '../..')
  const first = '<coverage><packages><package><classes><class filename="eng/verify.sh"><lines><line number="1" hits="0"/><line number="2" hits="1"/></lines></class></classes></package></packages></coverage>'
  const second = '<coverage><packages><package><classes><class filename="eng/verify.sh"><lines><line number="1" hits="2"/><line number="3" hits="1"/></lines></class></classes></package></packages></coverage>'
  const results = mkdtempSync(path.join(tmpdir(), 'harborline-coverage-'))
  try {
    for (const [directory, xml] of [['first', first], ['second', second]]) { mkdirSync(path.join(results, directory)); writeFileSync(path.join(results, directory, 'coverage.cobertura.xml'), xml) }
    const reportSummary = summarizeCoberturaReports(results, root)
    const summary = {suite: 'dotnet', artifactPaths: ['artifacts/quality/coverage/dotnet/reports/1-coverage.cobertura.xml', 'artifacts/quality/coverage/dotnet/reports/2-coverage.cobertura.xml'], ...reportSummary}
    assert.deepEqual({...summary, sourceReports: summary.sourceReports.map(source => path.basename(path.dirname(source)))}, {suite: 'dotnet', artifactPaths: summary.artifactPaths, files: summary.files, sourceReports: ['first', 'second'], coveredLines: 3, validLines: 3, mappedPaths: ['eng/verify.sh'], unmappedPaths: []})
    assert.deepEqual(receiptCoverage({enabled: true, summary}), [{suite: 'dotnet', artifactPaths: summary.artifactPaths, coveredLines: 3, validLines: 3}])
    assert.equal(receiptCoverage({enabled: false, summary}), undefined)
  } finally { rmSync(results, {recursive: true, force: true}) }
})
