/**
 * T-460. Transport over the api's atomic configuration activation routes.
 *
 * The api binds the platform's RELEASED status vocabulary itself and ships it as `detail` on
 * prepare and activate (ConfigurationActivationDetail.Bind). Nothing here derives, maps or
 * defaults a status: a refusal is reported as the api reported it, which is the only reason a
 * refused projection cannot read as effective on this surface.
 */

/** The bound released detail: field name to its already-localized value. */
export type ConfigurationActivationDetail = Readonly<Record<string, string>>

/** The effective generation. This read carries identity only, never a bound detail. */
export interface EffectiveGeneration {
  readonly digest: string
  readonly algorithm: string
  readonly references: unknown
}

export interface ConfigurationRefusal {
  readonly code: string
  readonly target: string
  readonly message: string
}

/** A prepare or activate outcome. `detail` is the released binding the surface renders. */
export interface ConfigurationActivationOutcome {
  readonly status: string
  readonly tenantKey: string
  readonly candidateDigest: string
  readonly expectedBaselineDigest: string
  readonly effectiveDigest: string
  readonly refusals: readonly ConfigurationRefusal[]
  readonly detail?: ConfigurationActivationDetail | null
  readonly acknowledged?: boolean
}

export interface ConfigurationOwnership {
  readonly definitionKey: string
  readonly packageKey: string
}

export interface PrepareConfigurationRequest {
  readonly expectedBaselineDigest: string
  readonly activePackageKeys: readonly string[]
  readonly ownership?: readonly ConfigurationOwnership[]
}

export interface ActivateConfigurationRequest {
  readonly expectedBaselineDigest: string
  readonly candidateDigest: string
  readonly evidenceIntent: { readonly id: string; readonly reason: string }
}

export interface ConfigurationActivationClient {
  readEffective(signal?: AbortSignal): Promise<EffectiveGeneration>
  prepare(request: PrepareConfigurationRequest, signal?: AbortSignal): Promise<ConfigurationActivationOutcome>
  activate(request: ActivateConfigurationRequest, signal?: AbortSignal): Promise<ConfigurationActivationOutcome>
}

export class ConfigurationActivationError extends Error {
  constructor(readonly status: number, readonly code: string) {
    super(code)
    this.name = 'ConfigurationActivationError'
  }
}

export interface HttpConfigurationActivationClientOptions {
  readonly baseUrl?: string
  readonly headers?: Record<string, string>
  readonly fetchImpl?: typeof fetch
}

async function parseError(response: Response): Promise<ConfigurationActivationError> {
  try {
    const body = await response.json() as { code?: unknown; error?: unknown }
    const code = typeof body.code === 'string' ? body.code : typeof body.error === 'string' ? body.error : `http.${response.status}`
    return new ConfigurationActivationError(response.status, code)
  } catch {
    return new ConfigurationActivationError(response.status, `http.${response.status}`)
  }
}

export function createHttpConfigurationActivationClient(
  options: HttpConfigurationActivationClientOptions = {},
): ConfigurationActivationClient {
  const baseUrl = options.baseUrl ?? ''
  const request = options.fetchImpl ?? globalThis.fetch

  // 422 is how the api reports a refusal, and its body IS the released detail. Treating it as a
  // transport failure would drop the one outcome this surface exists to distinguish, so an
  // unprocessable response is read, not thrown.
  async function post<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
    const response = await request(`${baseUrl}${path}`, {
      method: 'POST',
      headers: { ...options.headers, 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      signal,
    })
    if (!response.ok && response.status !== 422) throw await parseError(response)
    return await response.json() as T
  }

  return {
    async readEffective(signal) {
      const response = await request(`${baseUrl}/api/local-node/configuration/effective`, { method: 'GET', headers: options.headers, signal })
      if (!response.ok) throw await parseError(response)
      return await response.json() as EffectiveGeneration
    },
    prepare: (body, signal) => post('/api/local-node/configuration/prepare', body, signal),
    activate: (body, signal) => post('/api/local-node/configuration/activate', body, signal),
  }
}
