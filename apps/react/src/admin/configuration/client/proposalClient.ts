/**
 * T-461. Transport over the api's propose, save and release routes.
 *
 * The api binds the platform's RELEASED Proposed change / Saved version / Released package
 * vocabulary itself and ships it as `detail` on every one of these answers. Nothing here derives,
 * maps or defaults a step label, and nothing here recomputes a digest: the released digest this
 * surface shows is the one the api derived from the artifact's own bytes.
 */

/** The bound released detail: field name to its already-localized value. */
export type ConfigurationProposalDetail = Readonly<Record<string, string>>

export interface ProposedEdit {
  readonly definitionKey: string
  readonly packageKey: string
}

export interface SavedVersionSummary {
  readonly ordinal: number
  readonly digest: string
  readonly author: string
  readonly rationale: string
  readonly savedAt: string
}

export interface RecordedCheck {
  readonly receiptId: string
  readonly checkedDigest: string
  /** False once the proposed change has been edited since the check ran. */
  readonly isCurrent: boolean
}

export interface ReleasedPackageSummary {
  readonly digest: string
  readonly packageKey: string
  readonly revision: string
  readonly proposalId: string
  readonly savedVersionDigest: string
  readonly baselineDigest: string
  readonly releasedBy: string
  readonly releasedAt: string
  readonly signature: string
}

export interface ProposalRefusal {
  readonly code: string
  readonly target: string
  readonly message: string
}

/** One Proposed change as the api reports it. `detail` is the released binding the surface renders. */
export interface ProposedChange {
  readonly status: string
  readonly tenantKey: string
  readonly proposalId: string
  readonly baselineDigest: string
  readonly effectiveDigest: string
  readonly workingDigest: string
  readonly edits: readonly ProposedEdit[]
  readonly savedVersionCount: number
  readonly savedVersion?: SavedVersionSummary | null
  readonly check?: RecordedCheck | null
  readonly releasedPackage?: ReleasedPackageSummary | null
  readonly refusals: readonly ProposalRefusal[]
  readonly detail: ConfigurationProposalDetail
}

export interface ConfigurationProposalClient {
  start(proposalId: string, signal?: AbortSignal): Promise<ProposedChange>
  read(proposalId: string, signal?: AbortSignal): Promise<ProposedChange>
  autosave(proposalId: string, edit: { definitionKey: string; packageKey: string; bodyJson: string }, signal?: AbortSignal): Promise<ProposedChange>
  saveVersion(proposalId: string, rationale: string, signal?: AbortSignal): Promise<ProposedChange>
  recordCheck(proposalId: string, receiptId: string, signal?: AbortSignal): Promise<ProposedChange>
  release(proposalId: string, ordinal: number, packageKey: string, revision: string, signal?: AbortSignal): Promise<ProposedChange>
  offered(signal?: AbortSignal): Promise<readonly ReleasedPackageSummary[]>
}

export class ConfigurationProposalError extends Error {
  constructor(readonly status: number, readonly code: string) {
    super(code)
    this.name = 'ConfigurationProposalError'
  }
}

export interface HttpConfigurationProposalClientOptions {
  readonly baseUrl?: string
  readonly headers?: Record<string, string>
  readonly fetchImpl?: typeof fetch
}

async function parseError(response: Response): Promise<ConfigurationProposalError> {
  try {
    const body = await response.json() as { code?: unknown; error?: unknown }
    const code = typeof body.code === 'string' ? body.code : typeof body.error === 'string' ? body.error : `http.${response.status}`
    return new ConfigurationProposalError(response.status, code)
  } catch {
    return new ConfigurationProposalError(response.status, `http.${response.status}`)
  }
}

export function createHttpConfigurationProposalClient(
  options: HttpConfigurationProposalClientOptions = {},
): ConfigurationProposalClient {
  const baseUrl = options.baseUrl ?? ''
  const request = options.fetchImpl ?? globalThis.fetch
  const root = `${baseUrl}/api/local-node/configuration`

  // 422 is how the api reports a release refusal, and its body IS the released detail carrying the
  // refusal. Treating it as a transport failure would drop the one outcome the author most needs to
  // see, so an unprocessable response is read, not thrown.
  async function send<T>(method: string, path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
    const response = await request(`${root}${path}`, {
      method,
      headers: body === undefined ? options.headers : { ...options.headers, 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    })
    if (!response.ok && response.status !== 422) throw await parseError(response)
    return await response.json() as T
  }

  return {
    start: (proposalId, signal) => send('POST', '/proposals', { proposalId }, signal),
    read: (proposalId, signal) => send('GET', `/proposals/${encodeURIComponent(proposalId)}`, undefined, signal),
    autosave: (proposalId, edit, signal) => send('PUT', `/proposals/${encodeURIComponent(proposalId)}/edits`, edit, signal),
    saveVersion: (proposalId, rationale, signal) => send('POST', `/proposals/${encodeURIComponent(proposalId)}/versions`, { rationale }, signal),
    recordCheck: (proposalId, receiptId, signal) => send('POST', `/proposals/${encodeURIComponent(proposalId)}/checks`, { receiptId }, signal),
    release: (proposalId, ordinal, packageKey, revision, signal) =>
      send('POST', `/proposals/${encodeURIComponent(proposalId)}/release`, { ordinal, packageKey, revision }, signal),
    offered: (signal) => send('GET', '/releases', undefined, signal),
  }
}
