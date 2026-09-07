import { SchedulingAdminError } from './types'
import type { SchedulingAdminClient, SchedulingDefinitionSummary, SchedulingDefinitionView } from './types'

const DEFINITIONS: readonly SchedulingDefinitionSummary[] = [
  { id: 'inspection-protocol', revision: 3, title: 'Inspection protocol', updatedAt: '2026-08-18T09:00:00Z', updatedBy: 'user:fixture-scheduler' },
  { id: 'move-in-checklist', revision: 1, title: 'Move-in checklist', updatedAt: '2026-08-10T14:30:00Z', updatedBy: 'user:fixture-scheduler' },
  { id: 'turnover-schedule', revision: 2, title: 'Turnover schedule', updatedAt: '2026-08-15T11:15:00Z', updatedBy: 'user:fixture-admin' },
]

const summary = (id: string, revision: number, title: string, updatedAt: string, updatedBy = 'user:fixture-scheduler'): SchedulingDefinitionSummary => ({
  id,
  revision,
  title,
  updatedAt,
  updatedBy,
})

const INITIAL_VERSIONS: Readonly<Record<string, readonly SchedulingDefinitionSummary[]>> = {
  'inspection-protocol': [
    summary('inspection-protocol', 3, 'Inspection protocol', '2026-08-18T09:00:00Z'),
    summary('inspection-protocol', 2, 'Inspection protocol', '2026-08-12T10:00:00Z'),
    summary('inspection-protocol', 1, 'Inspection protocol', '2026-08-05T08:00:00Z'),
  ],
  'move-in-checklist': [
    summary('move-in-checklist', 1, 'Move-in checklist', '2026-08-10T14:30:00Z'),
  ],
  'turnover-schedule': [
    summary('turnover-schedule', 2, 'Turnover schedule', '2026-08-15T11:15:00Z', 'user:fixture-admin'),
    summary('turnover-schedule', 1, 'Turnover schedule', '2026-08-09T16:45:00Z'),
  ],
}

interface FixtureRevision {
  readonly summary: SchedulingDefinitionSummary
  readonly definition: { readonly title: string, readonly cadence: string }
}

function cloneSummary(row: SchedulingDefinitionSummary): SchedulingDefinitionSummary {
  return { ...row }
}

function cloneView(row: FixtureRevision): SchedulingDefinitionView {
  return {
    id: row.summary.id,
    revision: row.summary.revision,
    definition: { ...row.definition },
    updatedAt: row.summary.updatedAt,
    updatedBy: row.summary.updatedBy,
  }
}

export function createFixtureSchedulingAdminClient(): SchedulingAdminClient {
  const definitions = DEFINITIONS.map(cloneSummary)
  const versions = new Map(
    Object.entries(INITIAL_VERSIONS).map(([id, rows]) => [id, rows.map(row => ({
      summary: cloneSummary(row),
      definition: { title: row.title, cadence: 'weekly' },
    }))]),
  )
  let restoreCounter = 0

  return {
    async listDefinitions() {
      return definitions.map(cloneSummary)
    },
    async getDefinition(definitionId) {
      const head = versions.get(definitionId)?.[0]
      if (!head) throw new SchedulingAdminError(404, 'scheduling.draft.not_found')
      return cloneView(head)
    },
    async listVersions(definitionId) {
      const rows = versions.get(definitionId)
      if (!rows) throw new SchedulingAdminError(404, 'scheduling.draft.not_found')
      return rows.map(row => cloneSummary(row.summary))
    },
    async restoreRevision(definitionId, revision) {
      const rows = versions.get(definitionId)
      const source = rows?.find(row => row.summary.revision === revision)
      if (!rows || !source) throw new SchedulingAdminError(404, 'scheduling.draft.revision_not_found')

      const newRevision = rows[0].summary.revision + 1
      const updatedAt = new Date(Date.UTC(2026, 7, 20, 0, restoreCounter)).toISOString().replace('.000Z', 'Z')
      restoreCounter += 1
      const restored: FixtureRevision = {
        summary: summary(definitionId, newRevision, source.definition.title, updatedAt, 'user:fixture-operator'),
        definition: { ...source.definition },
      }
      rows.unshift(restored)

      // No Draft/Published split here: the restored revision IS the new head immediately, so the
      // definitions list moves with it — a deliberate divergence from Forms.
      const definitionIndex = definitions.findIndex(row => row.id === definitionId)
      definitions[definitionIndex] = cloneSummary(restored.summary)
      return { definitionId, revision: newRevision, restoredFrom: revision }
    },
  }
}
