import { readAdminError } from '../../adminErrorEnvelope'
import { SchedulingAdminError } from './types'
import type { RestoreResult, SchedulingAdminClient, SchedulingDefinitionSummary, SchedulingDefinitionView } from './types'

interface WireSchedulingDraftSummary {
  readonly id: string
  readonly revision: number
  readonly title: string
  readonly updatedAt: string
  readonly updatedBy: string
}

interface WireSchedulingDraftView {
  readonly id: string
  readonly revision: number
  readonly definition: unknown
  readonly updatedAt: string
  readonly updatedBy: string
}

interface WireRestoreResult {
  readonly definitionId: string
  readonly revision: number
  readonly restoredFrom: number
}

export interface HttpSchedulingAdminClientOptions {
  baseUrl?: string
  headers?: Record<string, string>
  fetchImpl?: typeof fetch
}

function mapSummary(row: WireSchedulingDraftSummary): SchedulingDefinitionSummary {
  return {
    id: row.id,
    revision: row.revision,
    title: row.title,
    updatedAt: row.updatedAt,
    updatedBy: row.updatedBy,
  }
}

/**
 * Ticket 094: scheduling answers the same `{ code, detail? }` envelope as the other four families,
 * read here by the parse they share.
 */
async function parseError(response: Response): Promise<SchedulingAdminError> {
  const error = await readAdminError(response)
  return new SchedulingAdminError(response.status, error.message, error.code, error.detail)
}

export function createHttpSchedulingAdminClient(opts?: HttpSchedulingAdminClientOptions): SchedulingAdminClient {
  const baseUrl = opts?.baseUrl ?? ''
  const request: typeof fetch = (input, init) => (opts?.fetchImpl ?? globalThis.fetch)(input, init)

  async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
    const response = await request(`${baseUrl}${path}`, { method: 'GET', headers: opts?.headers, signal })
    if (!response.ok) throw await parseError(response)
    return await response.json() as T
  }

  return {
    async listDefinitions(signal) {
      try {
        const rows = await getJson<readonly WireSchedulingDraftSummary[]>('/api/local-node/scheduling/definitions', signal)
        return rows.map(mapSummary)
      } catch (error) {
        // A 404 on the LIST route does not mean "no definitions" — an empty tenant answers 200 [].
        // It means the route is not mapped at all, because the node registers the scheduling family
        // only when LocalNode:SchedulingDogfood:Enabled is true (false in shipped config). Saying so
        // is the difference between an operator flipping a flag and an operator filing a bug.
        if (error instanceof SchedulingAdminError && error.status === 404) {
          throw new SchedulingAdminError(404, 'scheduling.family_not_enabled')
        }
        throw error
      }
    },
    async getDefinition(definitionId, signal) {
      const row = await getJson<WireSchedulingDraftView>(`/api/local-node/scheduling/definitions/${encodeURIComponent(definitionId)}`, signal)
      return {
        id: row.id,
        revision: row.revision,
        definition: row.definition,
        updatedAt: row.updatedAt,
        updatedBy: row.updatedBy,
      } satisfies SchedulingDefinitionView
    },
    async listVersions(definitionId, signal) {
      const rows = await getJson<readonly WireSchedulingDraftSummary[]>(`/api/local-node/scheduling/definitions/${encodeURIComponent(definitionId)}/versions`, signal)
      return rows.map(mapSummary)
    },
    async restoreRevision(definitionId, revision, signal) {
      const response = await request(`${baseUrl}/api/local-node/scheduling/definitions/${encodeURIComponent(definitionId)}/restore`, {
        method: 'POST',
        headers: { ...opts?.headers, 'content-type': 'application/json' },
        body: JSON.stringify({ revision }),
        signal,
      })
      if (!response.ok) throw await parseError(response)
      const result = await response.json() as WireRestoreResult
      return { definitionId: result.definitionId, revision: result.revision, restoredFrom: result.restoredFrom } satisfies RestoreResult
    },
  }
}
