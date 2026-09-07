import { createFixtureAuthorizationAdminClient } from './fixtureClient'
import { createHttpAuthorizationAdminClient } from './httpClient'
import type { AuthorizationAdminClient } from './types'

export * from './types'
export { createFixtureAuthorizationAdminClient } from './fixtureClient'
export { createHttpAuthorizationAdminClient } from './httpClient'
export type { HttpAuthorizationAdminClientOptions } from './httpClient'

export function createAuthorizationAdminClient(): AuthorizationAdminClient {
  const origin = import.meta.env.VITE_AUTHORIZATION_API_ORIGIN
  if (origin) return createHttpAuthorizationAdminClient({ baseUrl: import.meta.env.DEV ? '' : origin })
  const fixture = import.meta.env.VITE_AUTHORIZATION_FIXTURE
  if (fixture === '1' || fixture === 'true') return createFixtureAuthorizationAdminClient()
  throw new Error(
    'Authorization admin client is not configured: set VITE_AUTHORIZATION_API_ORIGIN to the local node origin, '
    + 'or set VITE_AUTHORIZATION_FIXTURE="1" (or "true") to explicitly opt in to the serverless fixture client.',
  )
}
