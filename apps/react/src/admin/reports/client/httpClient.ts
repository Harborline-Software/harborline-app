import { ReportsAdminError } from './types'
import type { ReportDefinitionDetail, ReportDefinitionSummary, ReportsAdminClient, ReportVersionList } from './types'

interface WireReportDefinitionSummary {
  readonly key: string
  readonly version: string
  readonly title: string
  readonly reportKind: string
  readonly cascadeLayer: string
}

interface WireReportDefinitionDetail extends WireReportDefinitionSummary {
  readonly schemaVersion: number
  readonly parameters: unknown
  readonly provenance: unknown
}

interface WireReportVersionList {
  readonly ordering: ReportVersionList['ordering']
  readonly versions: readonly WireReportDefinitionSummary[]
}

export interface HttpReportsAdminClientOptions {
  baseUrl?: string
  headers?: Record<string, string>
  fetchImpl?: typeof fetch
}

function mapSummary(row: WireReportDefinitionSummary): ReportDefinitionSummary {
  return { key: row.key, version: row.version, title: row.title, reportKind: row.reportKind, cascadeLayer: row.cascadeLayer }
}

async function parseError(response: Response): Promise<ReportsAdminError> {
  try {
    // Ticket 092: the wire carries a machine code, never a sentence. The client owns
    // presentation; a body without a code falls back to the HTTP reason phrase.
    const body = await response.json() as { code?: unknown }
    return new ReportsAdminError(response.status, typeof body.code === 'string' ? body.code : response.statusText)
  } catch {
    return new ReportsAdminError(response.status, response.statusText)
  }
}

export function createHttpReportsAdminClient(opts?: HttpReportsAdminClientOptions): ReportsAdminClient {
  const baseUrl = opts?.baseUrl ?? ''
  const request: typeof fetch = (input, init) => (opts?.fetchImpl ?? globalThis.fetch)(input, init)

  async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
    const response = await request(`${baseUrl}${path}`, { method: 'GET', headers: opts?.headers, signal })
    if (!response.ok) throw await parseError(response)
    return await response.json() as T
  }

  return {
    async listDefinitions(signal) {
      const rows = await getJson<readonly WireReportDefinitionSummary[]>('/api/local-node/reports/definitions', signal)
      return rows.map(mapSummary)
    },
    async getDefinition(key, signal) {
      const row = await getJson<WireReportDefinitionDetail>(`/api/local-node/reports/definitions/${encodeURIComponent(key)}`, signal)
      return { ...mapSummary(row), schemaVersion: row.schemaVersion, parameters: row.parameters, provenance: row.provenance } satisfies ReportDefinitionDetail
    },
    async listVersions(key, signal) {
      const list = await getJson<WireReportVersionList>(`/api/local-node/reports/definitions/${encodeURIComponent(key)}/versions`, signal)
      return { ordering: list.ordering, versions: list.versions.map(mapSummary) }
    },
  }
}
