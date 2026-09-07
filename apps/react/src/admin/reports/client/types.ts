export interface ReportDefinitionSummary {
  readonly key: string
  readonly version: string
  readonly title: string
  readonly reportKind: string
  readonly cascadeLayer: string
}

export interface ReportDefinitionDetail extends ReportDefinitionSummary {
  readonly schemaVersion: number
  readonly parameters: unknown
  readonly provenance: unknown
}

export interface ReportVersionList {
  readonly ordering: 'semver' | 'ordinal' // 'semver' when every retained version parses as semver; 'ordinal' fallback otherwise
  readonly versions: readonly ReportDefinitionSummary[]
}

export class ReportsAdminError extends Error {
  constructor(public readonly status: number, message: string) { super(message); this.name = 'ReportsAdminError' }
}

export interface ReportsAdminClient {
  listDefinitions(signal?: AbortSignal): Promise<readonly ReportDefinitionSummary[]>
  getDefinition(key: string, signal?: AbortSignal): Promise<ReportDefinitionDetail>
  listVersions(key: string, signal?: AbortSignal): Promise<ReportVersionList>
}
