export function coverageRecord(summary) {
  if (!summary || typeof summary !== 'object') throw new Error('coverage summary must be an object')
  const {suite, artifactPaths, coveredLines, validLines} = summary
  if (typeof suite !== 'string' || suite.length === 0) throw new Error('coverage summary is missing suite')
  if (!Array.isArray(artifactPaths) || artifactPaths.length === 0 || !artifactPaths.every(path => typeof path === 'string' && path.length > 0)) throw new Error('coverage summary is missing artifact paths')
  if (!Number.isInteger(coveredLines) || !Number.isInteger(validLines) || coveredLines < 0 || validLines < coveredLines) throw new Error('coverage summary has invalid line counts')
  return {suite, artifactPaths, coveredLines, validLines}
}

export function receiptCoverage({enabled, summary}) {
  return enabled ? [coverageRecord(summary)] : undefined
}
