import { createFixtureDataExchangeAdminClient } from './fixtureClient'
import { createHttpDataExchangeAdminClient } from './httpClient'
import type { DataExchangeAdminClient } from './types'

export type {
  DataExchangeDefinitionDetail,
  DataExchangeDefinitionSummary,
  DataExchangeAdminClient,
  DataExchangeVersionList,
} from './types'
export { DataExchangeAdminError } from './types'
export { createFixtureDataExchangeAdminClient } from './fixtureClient'
export { createHttpDataExchangeAdminClient } from './httpClient'
export type { HttpDataExchangeAdminClientOptions } from './httpClient'

export function createDataExchangeAdminClient(): DataExchangeAdminClient {
  return import.meta.env.VITE_DATA_EXCHANGE_API_ORIGIN
    ? createHttpDataExchangeAdminClient({ baseUrl: '' })
    : createFixtureDataExchangeAdminClient()
}
