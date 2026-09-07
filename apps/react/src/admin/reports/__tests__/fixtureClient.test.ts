import { describe, expect, it } from 'vitest'
import { createFixtureReportsAdminClient } from '../client/fixtureClient'
import { ReportsAdminError } from '../client/types'

describe('createFixtureReportsAdminClient', () => {
  it('lists the deterministic definitions and version histories', async () => {
    const first = createFixtureReportsAdminClient()
    const second = createFixtureReportsAdminClient()
    const definitions = [
      { key: 'trial-balance', version: '2.1.0', title: 'Trial balance', reportKind: 'standard', cascadeLayer: 'Tenant' },
      { key: 'ar-aging', version: '1.4.2', title: 'AR aging summary', reportKind: 'standard', cascadeLayer: 'Tenant' },
      { key: 'occupancy', version: '0.3.0', title: 'Occupancy snapshot', reportKind: 'snapshot', cascadeLayer: 'Tenant' },
    ]

    const firstDefinitions = await first.listDefinitions()
    await expect(second.listDefinitions()).resolves.toEqual(firstDefinitions)
    expect(firstDefinitions).toEqual(definitions)
    const semver = await first.listVersions('trial-balance')
    expect(semver.ordering).toBe('semver')
    expect(semver.versions.map(row => row.version)).toEqual(['2.1.0', '2.0.0', '1.9.0'])
    const ordinal = await first.listVersions('occupancy')
    expect(ordinal.ordering).toBe('ordinal')
    expect(ordinal.versions.map(row => row.version)).toEqual(['2024-legacy', '0.3.0'])
  })

  it('returns the pinned definition detail', async () => {
    const client = createFixtureReportsAdminClient()

    await expect(client.getDefinition('trial-balance')).resolves.toEqual({
      key: 'trial-balance', version: '2.1.0', title: 'Trial balance', reportKind: 'standard', cascadeLayer: 'Tenant', schemaVersion: 1,
      parameters: { chartId: 'chart-7' },
      provenance: { originPackKey: 'harborline.core-reports', tier: 'Vendor' },
    })
    await expect(client.getDefinition('occupancy')).resolves.toEqual({
      key: 'occupancy', version: '0.3.0', title: 'Occupancy snapshot', reportKind: 'snapshot', cascadeLayer: 'Tenant', schemaVersion: 1,
      parameters: { scope: 'portfolio' },
      provenance: { originPackKey: 'demo.occupancy', tier: 'Community' },
    })
  })

  it('rejects an unknown definition with a status-bearing error', async () => {
    const rejection = createFixtureReportsAdminClient().getDefinition('nope')

    await expect(rejection).rejects.toBeInstanceOf(ReportsAdminError)
    await expect(rejection).rejects.toMatchObject({ status: 404, message: "No report definition 'nope' for this tenant." })
  })
})
