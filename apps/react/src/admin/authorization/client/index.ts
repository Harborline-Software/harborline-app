import { createFixtureAuthorizationAdminClient } from './fixtureClient'
import { createHttpAuthorizationAdminClient } from './httpClient'
import type { AuthorizationAdminClient } from './types'
import { send } from '../../../../../shared/selected-session-transport.mjs'

export * from './types'
export { createFixtureAuthorizationAdminClient } from './fixtureClient'
export { createHttpAuthorizationAdminClient } from './httpClient'
export type { HttpAuthorizationAdminClientOptions } from './httpClient'

export function createAuthorizationAdminClient(): AuthorizationAdminClient {
  const origin = import.meta.env.VITE_AUTHORIZATION_API_ORIGIN
  if (origin) return createHttpAuthorizationAdminClient({
    // The origin configures the host proxy; browser requests always use the selected session.
    fetchImpl: async (input, options) => {
      const headers = new Headers(options?.headers)
      const idempotencyKey = headers.get('Idempotency-Key')
      const response = await send(String(input), options?.method,
        typeof options?.body === 'string' ? options.body : null,
        headers.get('Content-Type') ?? 'application/json',
        idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {},
        { signal: options?.signal ?? undefined })
      return new Response(response.body, { status: response.status })
    },
  })
  const fixture = import.meta.env.VITE_AUTHORIZATION_FIXTURE
  if (fixture === '1' || fixture === 'true') return createFixtureAuthorizationAdminClient()
  throw new Error(
    'Authorization admin client is not configured: set VITE_AUTHORIZATION_API_ORIGIN to the local node origin, '
    + 'or set VITE_AUTHORIZATION_FIXTURE="1" (or "true") to explicitly opt in to the serverless fixture client.',
  )
}
