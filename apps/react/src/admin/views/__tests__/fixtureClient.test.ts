import { describe, expect, it } from 'vitest'
import { createFixtureViewsAdminClient } from '../client/fixtureClient'
import { ViewsAdminError } from '../client/types'

describe('createFixtureViewsAdminClient', () => {
  it('lists the deterministic definitions and version histories', async () => {
    const first = createFixtureViewsAdminClient()
    const second = createFixtureViewsAdminClient()
    const definitions = [
      { key: 'work-orders-table', version: '2.1.0', title: 'Work orders table', viewKind: 'table', cascadeLayer: 'Tenant' },
      { key: 'tenant-directory', version: '1.4.2', title: 'Tenant directory', viewKind: 'table', cascadeLayer: 'Tenant' },
      { key: 'occupancy-board', version: '0.3.0', title: 'Occupancy board', viewKind: 'board', cascadeLayer: 'Tenant' },
    ]

    const firstDefinitions = await first.listDefinitions()
    await expect(second.listDefinitions()).resolves.toEqual(firstDefinitions)
    expect(firstDefinitions).toEqual(definitions)
    const semver = await first.listVersions('work-orders-table')
    expect(semver.ordering).toBe('semver')
    expect(semver.versions.map(row => row.version)).toEqual(['2.1.0', '2.0.0', '1.9.0'])
    const ordinal = await first.listVersions('occupancy-board')
    expect(ordinal.ordering).toBe('ordinal')
    expect(ordinal.versions.map(row => row.version)).toEqual(['2024-legacy', '0.3.0'])
  })

  it('returns the pinned definition detail', async () => {
    const client = createFixtureViewsAdminClient()

    await expect(client.getDefinition('work-orders-table')).resolves.toEqual({
      key: 'work-orders-table', version: '2.1.0', title: 'Work orders table', viewKind: 'table', cascadeLayer: 'Tenant', schemaVersion: 1,
      parameters: { entityType: 'work-order' },
      provenance: { originPackKey: 'harborline.core-views', tier: 'Vendor' },
    })
    await expect(client.getDefinition('occupancy-board')).resolves.toEqual({
      key: 'occupancy-board', version: '0.3.0', title: 'Occupancy board', viewKind: 'board', cascadeLayer: 'Tenant', schemaVersion: 1,
      parameters: { scope: 'portfolio' },
      provenance: { originPackKey: 'demo.views', tier: 'Community' },
    })
  })

  it('rejects an unknown definition with a status-bearing error', async () => {
    const rejection = createFixtureViewsAdminClient().getDefinition('nope')

    await expect(rejection).rejects.toBeInstanceOf(ViewsAdminError)
    await expect(rejection).rejects.toMatchObject({ status: 404, message: "No view definition 'nope' for this tenant." })
  })
})
