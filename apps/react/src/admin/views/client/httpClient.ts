import { ViewsAdminError } from './types'
import type { ViewDefinitionDetail, ViewDefinitionSummary, ViewsAdminClient, ViewVersionList } from './types'

interface WireViewDefinitionSummary {
  readonly key: string
  readonly version: string
  readonly title: string
  readonly viewKind: string
  readonly cascadeLayer: string
}

interface WireViewDefinitionDetail extends WireViewDefinitionSummary {
  readonly schemaVersion: number
  readonly parameters: unknown
  readonly provenance: unknown
}

interface WireViewVersionList {
  readonly ordering: ViewVersionList['ordering']
  readonly versions: readonly WireViewDefinitionSummary[]
}

export interface HttpViewsAdminClientOptions {
  baseUrl?: string
  headers?: Record<string, string>
  fetchImpl?: typeof fetch
}

function mapSummary(row: WireViewDefinitionSummary): ViewDefinitionSummary {
  return { key: row.key, version: row.version, title: row.title, viewKind: row.viewKind, cascadeLayer: row.cascadeLayer }
}

async function parseError(response: Response): Promise<ViewsAdminError> {
  try {
    // Ticket 092: the wire carries a machine code, never a sentence. The client owns
    // presentation; a body without a code falls back to the HTTP reason phrase.
    const body = await response.json() as { code?: unknown }
    return new ViewsAdminError(response.status, typeof body.code === 'string' ? body.code : response.statusText)
  } catch {
    return new ViewsAdminError(response.status, response.statusText)
  }
}

export function createHttpViewsAdminClient(opts?: HttpViewsAdminClientOptions): ViewsAdminClient {
  const baseUrl = opts?.baseUrl ?? ''
  const request: typeof fetch = (input, init) => (opts?.fetchImpl ?? globalThis.fetch)(input, init)

  async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
    const response = await request(`${baseUrl}${path}`, { method: 'GET', headers: opts?.headers, signal })
    if (!response.ok) throw await parseError(response)
    return await response.json() as T
  }

  return {
    async listDefinitions(signal) {
      const rows = await getJson<readonly WireViewDefinitionSummary[]>('/api/local-node/views/definitions', signal)
      return rows.map(mapSummary)
    },
    async getDefinition(key, signal) {
      const row = await getJson<WireViewDefinitionDetail>(`/api/local-node/views/definitions/${encodeURIComponent(key)}`, signal)
      return { ...mapSummary(row), schemaVersion: row.schemaVersion, parameters: row.parameters, provenance: row.provenance } satisfies ViewDefinitionDetail
    },
    async listVersions(key, signal) {
      const list = await getJson<WireViewVersionList>(`/api/local-node/views/definitions/${encodeURIComponent(key)}/versions`, signal)
      return { ordering: list.ordering, versions: list.versions.map(mapSummary) }
    },
  }
}
