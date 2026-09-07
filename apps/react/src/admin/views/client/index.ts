import { createFixtureViewsAdminClient } from './fixtureClient'
import { createHttpViewsAdminClient } from './httpClient'
import type { ViewsAdminClient } from './types'

export type {
  ViewDefinitionDetail,
  ViewDefinitionSummary,
  ViewsAdminClient,
  ViewVersionList,
} from './types'
export { ViewsAdminError } from './types'
export { createFixtureViewsAdminClient } from './fixtureClient'
export { createHttpViewsAdminClient } from './httpClient'
export type { HttpViewsAdminClientOptions } from './httpClient'

export function createViewsAdminClient(): ViewsAdminClient {
  return import.meta.env.VITE_VIEWS_API_ORIGIN
    ? createHttpViewsAdminClient({ baseUrl: '' })
    : createFixtureViewsAdminClient()
}
