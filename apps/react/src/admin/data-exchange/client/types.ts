export interface DataExchangeDefinitionSummary {
  readonly key: string
  readonly version: string
  readonly title: string
  readonly exchangeKind: string
  readonly cascadeLayer: string
}

export interface DataExchangeDefinitionDetail extends DataExchangeDefinitionSummary {
  readonly schemaVersion: number
  readonly settings: unknown
  readonly provenance: unknown
}

export interface DataExchangeVersionList {
  readonly ordering: 'semver' | 'ordinal' // 'semver' when every retained version parses as semver; 'ordinal' fallback otherwise
  readonly versions: readonly DataExchangeDefinitionSummary[]
}

export class DataExchangeAdminError extends Error {
  constructor(public readonly status: number, message: string) { super(message); this.name = 'DataExchangeAdminError' }
}

export interface DataExchangeAdminClient {
  listDefinitions(signal?: AbortSignal): Promise<readonly DataExchangeDefinitionSummary[]>
  getDefinition(key: string, signal?: AbortSignal): Promise<DataExchangeDefinitionDetail>
  listVersions(key: string, signal?: AbortSignal): Promise<DataExchangeVersionList>
}
