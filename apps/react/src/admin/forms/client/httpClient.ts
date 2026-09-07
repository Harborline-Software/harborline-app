import { readAdminError } from '../../adminErrorEnvelope'
import { FormsAdminError } from './types'
import type { FormDefinitionSummary, FormsAdminClient, FormVersionSummary, InternationalizedText, RestoreResult } from './types'

interface WireInternationalizedText {
  readonly defaultLocale: string
  readonly values: Record<string, string>
}

interface WireFormDefinitionSummary {
  readonly formId: string
  readonly version: string
  readonly title?: WireInternationalizedText | null
  readonly updatedAt: string
  readonly cascadeLayer?: string | null
}

interface WireFormVersionSummary {
  readonly formId: string
  readonly version: string
  readonly status: FormVersionSummary['status']
  readonly owner?: string | null
  readonly createdAt: string
  readonly updatedAt: string
  readonly derivedFrom?: string | null
  readonly syncsToPeers: boolean
  readonly safeForStaging: boolean
}

interface WireRestoreResult {
  readonly formId: string
  readonly version: string
}

export interface HttpFormsAdminClientOptions {
  baseUrl?: string
  headers?: Record<string, string>
  fetchImpl?: typeof fetch
}

function mapText(text: WireInternationalizedText | null | undefined): InternationalizedText | null {
  return text ? { defaultLocale: text.defaultLocale, values: text.values } : null
}

/**
 * Ticket 094: the Forms api answers `{ code, detail? }` on every error (api 2b184725), read here by
 * the parse every admin family shares.
 */
async function parseError(response: Response): Promise<FormsAdminError> {
  const error = await readAdminError(response)
  return new FormsAdminError(response.status, error.message, error.code, error.detail)
}

export function createHttpFormsAdminClient(opts?: HttpFormsAdminClientOptions): FormsAdminClient {
  const baseUrl = opts?.baseUrl ?? ''
  const request: typeof fetch = (input, init) => (opts?.fetchImpl ?? globalThis.fetch)(input, init)

  async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
    const response = await request(`${baseUrl}${path}`, { method: 'GET', headers: opts?.headers, signal })
    if (!response.ok) throw await parseError(response)
    return await response.json() as T
  }

  return {
    async listDefinitions(signal) {
      const rows = await getJson<readonly WireFormDefinitionSummary[]>('/api/local-node/forms/definitions', signal)
      return rows.map((row): FormDefinitionSummary => ({
        formId: row.formId,
        version: row.version,
        title: mapText(row.title),
        updatedAt: row.updatedAt,
        cascadeLayer: row.cascadeLayer ?? null,
      }))
    },
    async listVersions(formId, signal) {
      const rows = await getJson<readonly WireFormVersionSummary[]>(`/api/local-node/forms/definitions/${encodeURIComponent(formId)}/versions`, signal)
      return rows.map((row): FormVersionSummary => ({
        formId: row.formId,
        version: row.version,
        status: row.status,
        owner: row.owner ?? null,
        createdAt: row.createdAt,
        updatedAt: row.updatedAt,
        derivedFrom: row.derivedFrom ?? null,
        syncsToPeers: row.syncsToPeers,
        safeForStaging: row.safeForStaging,
      }))
    },
    async restoreVersion(formId, version, signal) {
      const response = await request(`${baseUrl}/api/local-node/forms/definitions/${encodeURIComponent(formId)}/restore`, {
        method: 'POST',
        headers: { ...opts?.headers, 'content-type': 'application/json' },
        body: JSON.stringify({ version }),
        signal,
      })
      if (!response.ok) throw await parseError(response)
      const result = await response.json() as WireRestoreResult
      return { formId: result.formId, version: result.version } satisfies RestoreResult
    },
  }
}
