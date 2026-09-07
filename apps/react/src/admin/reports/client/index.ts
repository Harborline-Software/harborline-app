import { createFixtureReportsAdminClient } from './fixtureClient'
import { createHttpReportsAdminClient } from './httpClient'
import type { ReportsAdminClient } from './types'

export type {
  ReportDefinitionDetail,
  ReportDefinitionSummary,
  ReportsAdminClient,
  ReportVersionList,
} from './types'
export { ReportsAdminError } from './types'
export { createFixtureReportsAdminClient } from './fixtureClient'
export { createHttpReportsAdminClient } from './httpClient'
export type { HttpReportsAdminClientOptions } from './httpClient'

export function createReportsAdminClient(): ReportsAdminClient {
  return import.meta.env.VITE_REPORTS_API_ORIGIN
    ? createHttpReportsAdminClient({ baseUrl: '' })
    : createFixtureReportsAdminClient()
}
