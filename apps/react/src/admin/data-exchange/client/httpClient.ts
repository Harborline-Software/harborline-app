import { DataExchangeAdminError } from './types'
import type { DataExchangeDefinitionDetail, DataExchangeDefinitionSummary, DataExchangeAdminClient, DataExchangeVersionList } from './types'

interface WireDataExchangeDefinitionSummary {
  readonly key: string
  readonly version: string
  readonly title: string
  readonly exchangeKind: string
  readonly cascadeLayer: string
}

interface WireDataExchangeDefinitionDetail extends WireDataExchangeDefinitionSummary {
  readonly schemaVersion: number
  readonly settings: unknown
  readonly provenance: unknown
}

interface WireDataExchangeVersionList {
  readonly ordering: DataExchangeVersionList['ordering']
  readonly versions: readonly WireDataExchangeDefinitionSummary[]
}

export interface HttpDataExchangeAdminClientOptions {
  baseUrl?: string
  headers?: Record<string, string>
  fetchImpl?: typeof fetch
}

function mapSummary(row: WireDataExchangeDefinitionSummary): DataExchangeDefinitionSummary {
  return { key: row.key, version: row.version, title: row.title, exchangeKind: row.exchangeKind, cascadeLayer: row.cascadeLayer }
}

async function parseError(response: Response): Promise<DataExchangeAdminError> {
  try {
    // Ticket 092: the wire carries a machine code, never a sentence. The client owns
    // presentation; a body without a code falls back to the HTTP reason phrase.
    const body = await response.json() as { code?: unknown }
    return new DataExchangeAdminError(response.status, typeof body.code === 'string' ? body.code : response.statusText)
  } catch {
    return new DataExchangeAdminError(response.status, response.statusText)
  }
}

export function createHttpDataExchangeAdminClient(opts?: HttpDataExchangeAdminClientOptions): DataExchangeAdminClient {
  const baseUrl = opts?.baseUrl ?? ''
  const request: typeof fetch = (input, init) => (opts?.fetchImpl ?? globalThis.fetch)(input, init)

  async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
    const response = await request(`${baseUrl}${path}`, { method: 'GET', headers: opts?.headers, signal })
    if (!response.ok) throw await parseError(response)
    return await response.json() as T
  }

  return {
    async listDefinitions(signal) {
      const rows = await getJson<readonly WireDataExchangeDefinitionSummary[]>('/api/local-node/data-exchange/definitions', signal)
      return rows.map(mapSummary)
    },
    async getDefinition(key, signal) {
      const row = await getJson<WireDataExchangeDefinitionDetail>(`/api/local-node/data-exchange/definitions/${encodeURIComponent(key)}`, signal)
      return { ...mapSummary(row), schemaVersion: row.schemaVersion, settings: row.settings, provenance: row.provenance } satisfies DataExchangeDefinitionDetail
    },
    async listVersions(key, signal) {
      const list = await getJson<WireDataExchangeVersionList>(`/api/local-node/data-exchange/definitions/${encodeURIComponent(key)}/versions`, signal)
      return { ordering: list.ordering, versions: list.versions.map(mapSummary) }
    },
  }
}
