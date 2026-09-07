import { describe, expect, it } from 'vitest'
import { createFixtureDataExchangeAdminClient } from '../client/fixtureClient'
import { DataExchangeAdminError } from '../client/types'

describe('createFixtureDataExchangeAdminClient', () => {
  it('lists the deterministic definitions and version histories', async () => {
    const first = createFixtureDataExchangeAdminClient()
    const second = createFixtureDataExchangeAdminClient()
    const definitions = [
      { key: 'bank-feed-import', version: '2.1.0', title: 'Bank feed import', exchangeKind: 'import', cascadeLayer: 'Tenant' },
      { key: 'erpnext-sync', version: '1.4.2', title: 'ERPNext sync', exchangeKind: 'import', cascadeLayer: 'Tenant' },
      { key: 'legacy-ledger-export', version: '0.3.0', title: 'Legacy ledger export', exchangeKind: 'export', cascadeLayer: 'Tenant' },
    ]

    const firstDefinitions = await first.listDefinitions()
    await expect(second.listDefinitions()).resolves.toEqual(firstDefinitions)
    expect(firstDefinitions).toEqual(definitions)
    const semver = await first.listVersions('bank-feed-import')
    expect(semver.ordering).toBe('semver')
    expect(semver.versions.map(row => row.version)).toEqual(['2.1.0', '2.0.0', '1.9.0'])
    const ordinal = await first.listVersions('legacy-ledger-export')
    expect(ordinal.ordering).toBe('ordinal')
    expect(ordinal.versions.map(row => row.version)).toEqual(['2024-legacy', '0.3.0'])
  })

  it('returns the pinned definition detail', async () => {
    const client = createFixtureDataExchangeAdminClient()

    await expect(client.getDefinition('bank-feed-import')).resolves.toEqual({
      key: 'bank-feed-import', version: '2.1.0', title: 'Bank feed import', exchangeKind: 'import', cascadeLayer: 'Tenant', schemaVersion: 1,
      settings: { mapping: 'csv-standard' },
      provenance: { originPackKey: 'harborline.core-exchange', tier: 'Vendor' },
    })
    await expect(client.getDefinition('legacy-ledger-export')).resolves.toEqual({
      key: 'legacy-ledger-export', version: '0.3.0', title: 'Legacy ledger export', exchangeKind: 'export', cascadeLayer: 'Tenant', schemaVersion: 1,
      settings: { format: 'ofx' },
      provenance: { originPackKey: 'demo.exchange', tier: 'Community' },
    })
  })

  it('rejects an unknown definition with a status-bearing error', async () => {
    const rejection = createFixtureDataExchangeAdminClient().getDefinition('nope')

    await expect(rejection).rejects.toBeInstanceOf(DataExchangeAdminError)
    await expect(rejection).rejects.toMatchObject({ status: 404, message: "No data exchange definition 'nope' for this tenant." })
  })
})
