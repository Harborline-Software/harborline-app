import { AuthorizationAdminError } from './types'
import type {
  AuthorizationAdminClient,
  AuthorizationBinding,
  AuthorizationCapabilityDefinition,
  NarrowAuthorizationBindingResult,
  RoleDefinition,
  RoleReference,
  StandingDefinition,
} from './types'

export interface HttpAuthorizationAdminClientOptions {
  readonly baseUrl?: string
  readonly headers?: Record<string, string>
  readonly fetchImpl?: typeof fetch
  readonly createIdempotencyKey?: () => string
}

async function parseError(response: Response): Promise<AuthorizationAdminError> {
  try {
    const body = await response.json() as { code?: unknown; auditId?: unknown }
    return new AuthorizationAdminError(response.status, typeof body.code === 'string' ? body.code : `http.${response.status}`,
      typeof body.auditId === 'string' ? body.auditId : undefined)
  } catch {
    return new AuthorizationAdminError(response.status, `http.${response.status}`)
  }
}

export function createHttpAuthorizationAdminClient(options: HttpAuthorizationAdminClientOptions = {}): AuthorizationAdminClient {
  const baseUrl = options.baseUrl ?? ''
  const request = options.fetchImpl ?? globalThis.fetch
  const createIdempotencyKey = options.createIdempotencyKey ?? (() => crypto.randomUUID())

  async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
    const response = await request(`${baseUrl}${path}`, { method: 'GET', headers: options.headers, signal })
    if (!response.ok) throw await parseError(response)
    return await response.json() as T
  }

  return {
    readTrace: auditId => getJson(`/api/local-node/authorization/traces/${encodeURIComponent(auditId)}`),
    listHolders: signal => getJson('/api/local-node/authorization/holders', signal),
    listRoleVocabulary: signal => getJson<readonly RoleDefinition[]>('/api/local-node/authorization/role-vocabulary', signal),
    listCapabilityDefinitions: signal => getJson<readonly AuthorizationCapabilityDefinition[]>('/api/local-node/authorization/capability-definitions', signal),
    getEffectiveBinding: (definitionId, signal) => getJson<AuthorizationBinding>(
      `/api/local-node/authorization/capability-definitions/${encodeURIComponent(definitionId)}/binding`, signal),
    async narrowCapabilityBinding(definitionId, selectedRoles, reason, signal) {
      const response = await request(
        `${baseUrl}/api/local-node/authorization/capability-definitions/${encodeURIComponent(definitionId)}/binding`,
        {
          method: 'POST',
          headers: {
            ...options.headers,
            'Content-Type': 'application/json',
            'Idempotency-Key': createIdempotencyKey(),
          },
          body: JSON.stringify({ selectedRoles, reason }),
          signal,
        },
      )
      if (!response.ok) throw await parseError(response)
      return await response.json() as NarrowAuthorizationBindingResult
    },
    listStandingCatalogue: signal => getJson<readonly StandingDefinition[]>('/api/local-node/authorization/standing-catalogue', signal),
  }
}
