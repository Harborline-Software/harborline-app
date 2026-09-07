import { FormsAdminError } from './types'
import type { FormDefinitionSummary, FormsAdminClient, FormVersionSummary } from './types'

const DEFINITIONS: readonly FormDefinitionSummary[] = [
  { formId: 'incident-intake', version: '1.0.3', title: { defaultLocale: 'en', values: { en: 'Incident intake' } }, updatedAt: '2026-08-05T12:00:00Z', cascadeLayer: 'Tenant' },
  { formId: 'vessel-registration', version: '2.1.4', title: { defaultLocale: 'en', values: { en: 'Vessel registration' } }, updatedAt: '2026-08-10T15:45:00Z', cascadeLayer: 'Tenant' },
  // 'Pack' on purpose (review P2-12): fixture-backed screens must exercise the non-'Tenant'
  // cascade-layer render path too, not fabricate 'Tenant' on every row.
  { formId: 'crew-manifest', version: '1.0.0', title: null, updatedAt: '2026-08-03T08:00:00Z', cascadeLayer: 'Pack' },
]

const version = (
  formId: string,
  value: string,
  status: FormVersionSummary['status'],
  owner: string | null,
  derivedFrom: string | null,
  timestamp: string,
): FormVersionSummary => ({
  formId,
  version: value,
  status,
  owner,
  createdAt: timestamp,
  updatedAt: timestamp,
  derivedFrom,
  syncsToPeers: true,
  safeForStaging: false,
})

const INITIAL_VERSIONS: Readonly<Record<string, readonly FormVersionSummary[]>> = {
  'incident-intake': [
    version('incident-intake', '1.0.3', 'Published', 'user:fixture-admin', '1.0.2', '2026-08-05T12:00:00Z'),
    version('incident-intake', '1.0.2', 'Draft', 'user:fixture-admin', '1.0.0', '2026-08-04T09:30:00Z'),
    version('incident-intake', '1.0.1', 'Published', 'user:fixture-operator', null, '2026-08-02T10:00:00Z'),
    version('incident-intake', '1.0.0', 'Published', 'user:fixture-operator', null, '2026-08-01T08:00:00Z'),
  ],
  'vessel-registration': [
    version('vessel-registration', '2.1.4', 'Published', null, null, '2026-08-10T15:45:00Z'),
    version('vessel-registration', '2.1.3', 'Deprecated', null, null, '2026-08-09T15:45:00Z'),
  ],
  'crew-manifest': [
    version('crew-manifest', '1.0.0', 'Published', null, null, '2026-08-03T08:00:00Z'),
  ],
}

function cloneVersion(row: FormVersionSummary): FormVersionSummary {
  return { ...row }
}

export function createFixtureFormsAdminClient(): FormsAdminClient {
  const definitions = DEFINITIONS.map(row => ({
    ...row,
    title: row.title ? { ...row.title, values: { ...row.title.values } } : null,
  }))
  const versions = new Map(
    Object.entries(INITIAL_VERSIONS).map(([formId, rows]) => [formId, rows.map(cloneVersion)]),
  )
  let restoreCounter = 0

  return {
    async listDefinitions() {
      return definitions.map(row => ({ ...row, title: row.title ? { ...row.title, values: { ...row.title.values } } : null }))
    },
    async listVersions(formId) {
      return (versions.get(formId) ?? []).map(cloneVersion)
    },
    async restoreVersion(formId, requestedVersion) {
      const rows = versions.get(formId)
      if (!rows?.some(row => row.version === requestedVersion)) {
        throw new FormsAdminError(404, `No revision '${requestedVersion}' of form '${formId}' to restore.`)
      }

      const parsed = rows.map(row => row.version.split('.').map(Number) as [number, number, number])
      const max = parsed.reduce((left, right) => {
        for (let index = 0; index < 3; index += 1) {
          if (left[index] !== right[index]) return left[index] > right[index] ? left : right
        }
        return left
      })
      const minted = `${max[0]}.${max[1]}.${max[2] + 1}`
      const timestamp = new Date(Date.UTC(2026, 7, 15, 0, restoreCounter)).toISOString().replace('.000Z', 'Z')
      restoreCounter += 1
      rows.unshift(version(formId, minted, 'Draft', 'user:fixture-operator', requestedVersion, timestamp))
      return { formId, version: minted }
    },
  }
}
