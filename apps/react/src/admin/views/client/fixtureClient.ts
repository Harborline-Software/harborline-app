import { ViewsAdminError } from './types'
import type { ViewDefinitionDetail, ViewDefinitionSummary, ViewsAdminClient, ViewVersionList } from './types'

const DEFINITIONS: readonly ViewDefinitionSummary[] = [
  { key: 'work-orders-table', version: '2.1.0', title: 'Work orders table', viewKind: 'table', cascadeLayer: 'Tenant' },
  { key: 'tenant-directory', version: '1.4.2', title: 'Tenant directory', viewKind: 'table', cascadeLayer: 'Tenant' },
  { key: 'occupancy-board', version: '0.3.0', title: 'Occupancy board', viewKind: 'board', cascadeLayer: 'Tenant' },
]

const summary = (head: ViewDefinitionSummary, version: string): ViewDefinitionSummary => ({ ...head, version })

const VERSION_LISTS: Readonly<Record<string, ViewVersionList>> = {
  'work-orders-table': { ordering: 'semver', versions: ['2.1.0', '2.0.0', '1.9.0'].map(value => summary(DEFINITIONS[0], value)) },
  'tenant-directory': { ordering: 'semver', versions: ['1.4.2', '1.4.1'].map(value => summary(DEFINITIONS[1], value)) },
  'occupancy-board': { ordering: 'ordinal', versions: ['2024-legacy', '0.3.0'].map(value => summary(DEFINITIONS[2], value)) },
}

const DETAILS: Readonly<Record<string, ViewDefinitionDetail>> = {
  'work-orders-table': { ...DEFINITIONS[0], schemaVersion: 1, parameters: { entityType: 'work-order' }, provenance: { originPackKey: 'harborline.core-views', tier: 'Vendor' } },
  'tenant-directory': { ...DEFINITIONS[1], schemaVersion: 1, parameters: { entityType: 'work-order' }, provenance: { originPackKey: 'harborline.core-views', tier: 'Vendor' } },
  'occupancy-board': { ...DEFINITIONS[2], schemaVersion: 1, parameters: { scope: 'portfolio' }, provenance: { originPackKey: 'demo.views', tier: 'Community' } },
}

function notFound(key: string): ViewsAdminError {
  return new ViewsAdminError(404, `No view definition '${key}' for this tenant.`)
}

export function createFixtureViewsAdminClient(): ViewsAdminClient {
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
