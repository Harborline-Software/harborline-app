import { DataExchangeAdminError } from './types'
import type { DataExchangeDefinitionDetail, DataExchangeDefinitionSummary, DataExchangeAdminClient, DataExchangeVersionList } from './types'

const DEFINITIONS: readonly DataExchangeDefinitionSummary[] = [
  { key: 'bank-feed-import', version: '2.1.0', title: 'Bank feed import', exchangeKind: 'import', cascadeLayer: 'Tenant' },
  { key: 'erpnext-sync', version: '1.4.2', title: 'ERPNext sync', exchangeKind: 'import', cascadeLayer: 'Tenant' },
  { key: 'legacy-ledger-export', version: '0.3.0', title: 'Legacy ledger export', exchangeKind: 'export', cascadeLayer: 'Tenant' },
]

const summary = (head: DataExchangeDefinitionSummary, version: string): DataExchangeDefinitionSummary => ({ ...head, version })

const VERSION_LISTS: Readonly<Record<string, DataExchangeVersionList>> = {
  'bank-feed-import': { ordering: 'semver', versions: ['2.1.0', '2.0.0', '1.9.0'].map(value => summary(DEFINITIONS[0], value)) },
  'erpnext-sync': { ordering: 'semver', versions: ['1.4.2', '1.4.1'].map(value => summary(DEFINITIONS[1], value)) },
  'legacy-ledger-export': { ordering: 'ordinal', versions: ['2024-legacy', '0.3.0'].map(value => summary(DEFINITIONS[2], value)) },
}

const DETAILS: Readonly<Record<string, DataExchangeDefinitionDetail>> = {
  'bank-feed-import': { ...DEFINITIONS[0], schemaVersion: 1, settings: { mapping: 'csv-standard' }, provenance: { originPackKey: 'harborline.core-exchange', tier: 'Vendor' } },
  'erpnext-sync': { ...DEFINITIONS[1], schemaVersion: 1, settings: { mapping: 'csv-standard' }, provenance: { originPackKey: 'harborline.core-exchange', tier: 'Vendor' } },
  'legacy-ledger-export': { ...DEFINITIONS[2], schemaVersion: 1, settings: { format: 'ofx' }, provenance: { originPackKey: 'demo.exchange', tier: 'Community' } },
}

function notFound(key: string): DataExchangeAdminError {
  return new DataExchangeAdminError(404, `No data exchange definition '${key}' for this tenant.`)
}

export function createFixtureDataExchangeAdminClient(): DataExchangeAdminClient {
  return {
    async listDefinitions() {
      return DEFINITIONS.map(row => ({ ...row }))
    },
    async getDefinition(key) {
      const detail = DETAILS[key]
      if (!detail) throw notFound(key)
      return { ...detail, settings: structuredClone(detail.settings), provenance: structuredClone(detail.provenance) }
    },
    async listVersions(key) {
      const list = VERSION_LISTS[key]
      if (!list) throw notFound(key)
      return { ordering: list.ordering, versions: list.versions.map(row => ({ ...row })) }
    },
  }
}
