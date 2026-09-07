import { createFixtureSchedulingAdminClient } from './fixtureClient'
import { createHttpSchedulingAdminClient } from './httpClient'
import type { SchedulingAdminClient } from './types'

export type {
  RestoreResult,
  SchedulingAdminClient,
  SchedulingDefinitionSummary,
  SchedulingDefinitionView,
} from './types'
export { SchedulingAdminError } from './types'
export { createFixtureSchedulingAdminClient } from './fixtureClient'
export { createHttpSchedulingAdminClient } from './httpClient'
export type { HttpSchedulingAdminClientOptions } from './httpClient'

export function createSchedulingAdminClient(): SchedulingAdminClient {
  return import.meta.env.VITE_SCHEDULING_API_ORIGIN
    ? createHttpSchedulingAdminClient({ baseUrl: '' })
    : createFixtureSchedulingAdminClient()
}
