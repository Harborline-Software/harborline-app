import { createFixtureFormsAdminClient } from './fixtureClient'
import { createHttpFormsAdminClient } from './httpClient'
import type { FormsAdminClient } from './types'

export type {
  FormDefinitionSummary,
  FormsAdminClient,
  FormsErrorDetail,
  FormVersionSummary,
  InternationalizedText,
  RestoreResult,
} from './types'
export { FormsAdminError } from './types'
export { createFixtureFormsAdminClient } from './fixtureClient'
export { createHttpFormsAdminClient } from './httpClient'
export type { HttpFormsAdminClientOptions } from './httpClient'

// Ticket 153 (L1351): the fixture is an EXPLICIT opt-in, never a silent fallback — a
// missing origin used to hand back a fixture that minted successful "writes" with no
// server. Set VITE_FORMS_API_ORIGIN for the live client, or VITE_FORMS_FIXTURE to opt in
// to the fixture (accepted values: exactly '1' or 'true' — mirroring the Blazor lane's
// FormsAdmin:UseFixture=true; apps/react/.env.development sets it for `npm run dev`).
export function createFormsAdminClient(): FormsAdminClient {
  const origin = import.meta.env.VITE_FORMS_API_ORIGIN
  if (origin) {
    // Review fix (ticket 153): the configured origin is PASSED THROUGH, never discarded — a
    // baseUrl of '' made a "configured" production bundle fetch its own origin. In dev the
    // vite proxy owns /api/local-node (and attaches the node credential — see vite.config.ts),
    // so dev requests stay same-origin and only a built bundle calls the origin directly.
    return createHttpFormsAdminClient({ baseUrl: import.meta.env.DEV ? '' : origin })
  }
  const fixture = import.meta.env.VITE_FORMS_FIXTURE
  if (fixture === '1' || fixture === 'true') return createFixtureFormsAdminClient()
  throw new Error(
    'Forms admin client is not configured: set VITE_FORMS_API_ORIGIN to the local node origin, '
    + 'or set VITE_FORMS_FIXTURE="1" (or "true") to explicitly opt in to the serverless fixture client.',
  )
}
