import { ReportsAdminError } from './types'
import type { ReportDefinitionDetail, ReportDefinitionSummary, ReportsAdminClient, ReportVersionList } from './types'

const DEFINITIONS: readonly ReportDefinitionSummary[] = [
  { key: 'trial-balance', version: '2.1.0', title: 'Trial balance', reportKind: 'standard', cascadeLayer: 'Tenant' },
  { key: 'ar-aging', version: '1.4.2', title: 'AR aging summary', reportKind: 'standard', cascadeLayer: 'Tenant' },
  { key: 'occupancy', version: '0.3.0', title: 'Occupancy snapshot', reportKind: 'snapshot', cascadeLayer: 'Tenant' },
]

const summary = (head: ReportDefinitionSummary, version: string): ReportDefinitionSummary => ({ ...head, version })

const VERSION_LISTS: Readonly<Record<string, ReportVersionList>> = {
  'trial-balance': { ordering: 'semver', versions: ['2.1.0', '2.0.0', '1.9.0'].map(value => summary(DEFINITIONS[0], value)) },
  'ar-aging': { ordering: 'semver', versions: ['1.4.2', '1.4.1'].map(value => summary(DEFINITIONS[1], value)) },
  'occupancy': { ordering: 'ordinal', versions: ['2024-legacy', '0.3.0'].map(value => summary(DEFINITIONS[2], value)) },
}

const DETAILS: Readonly<Record<string, ReportDefinitionDetail>> = {
  'trial-balance': { ...DEFINITIONS[0], schemaVersion: 1, parameters: { chartId: 'chart-7' }, provenance: { originPackKey: 'harborline.core-reports', tier: 'Vendor' } },
  'ar-aging': { ...DEFINITIONS[1], schemaVersion: 1, parameters: { chartId: 'chart-7' }, provenance: { originPackKey: 'harborline.core-reports', tier: 'Vendor' } },
  'occupancy': { ...DEFINITIONS[2], schemaVersion: 1, parameters: { scope: 'portfolio' }, provenance: { originPackKey: 'demo.occupancy', tier: 'Community' } },
}

function notFound(key: string): ReportsAdminError {
  return new ReportsAdminError(404, `No report definition '${key}' for this tenant.`)
}

export function createFixtureReportsAdminClient(): ReportsAdminClient {
  return {
    async listDefinitions() {
      return DEFINITIONS.map(row => ({ ...row }))
    },
    async getDefinition(key) {
      const detail = DETAILS[key]
      if (!detail) throw notFound(key)
      return { ...detail, parameters: structuredClone(detail.parameters), provenance: structuredClone(detail.provenance) }
    },
    async listVersions(key) {
      const list = VERSION_LISTS[key]
      if (!list) throw notFound(key)
      return { ordering: list.ordering, versions: list.versions.map(row => ({ ...row })) }
    },
  }
}
