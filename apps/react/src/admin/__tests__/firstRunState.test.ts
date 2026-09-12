import { describe, expect, it } from 'vitest'
import { firstRunEmptyStateCopy, tenantFirstRunState } from '../firstRunState'

describe('tenant first-run empty states', () => {
  it.each([
    [{ hasInstalledContent: false, hasAuthoredContent: false, viewerHasPermittingGrant: false }, 'installer'],
    [{ hasInstalledContent: true, hasAuthoredContent: false, viewerHasPermittingGrant: false }, 'platform-installed'],
    [{ hasInstalledContent: true, hasAuthoredContent: true, viewerHasPermittingGrant: false }, 'access-preloaded'],
    [{ hasInstalledContent: true, hasAuthoredContent: false, viewerHasPermittingGrant: true }, 'administrator-ready'],
    [{ hasInstalledContent: true, hasAuthoredContent: true, viewerHasPermittingGrant: true }, 'domain-installed'],
  ] as const)('derives %s from installed, authored, and permitting-grant observations', (observation, expected) => {
    expect(tenantFirstRunState(observation)).toBe(expected)
  })

  it('keeps the installed, authored, and permitting-grant causes as distinct sentences', () => {
    const nothingInstalled = firstRunEmptyStateCopy({ hasInstalledContent: false, hasAuthoredContent: false, viewerHasPermittingGrant: false })
    const noGrant = firstRunEmptyStateCopy({ hasInstalledContent: true, hasAuthoredContent: false, viewerHasPermittingGrant: false })
    const nothingAuthored = firstRunEmptyStateCopy({ hasInstalledContent: true, hasAuthoredContent: false, viewerHasPermittingGrant: true })

    expect(new Set([nothingInstalled, noGrant, nothingAuthored]).size).toBe(3)
    expect(nothingInstalled).toContain('nothing is installed yet')
    expect(noGrant).toContain('you hold no grant that permits this list')
    expect(nothingAuthored).toContain('nothing is authored yet')
  })
})
