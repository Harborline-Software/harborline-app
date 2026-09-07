export interface ViewDefinitionSummary {
  readonly key: string
  readonly version: string
  readonly title: string
  readonly viewKind: string
  readonly cascadeLayer: string
}

export interface ViewDefinitionDetail extends ViewDefinitionSummary {
  readonly schemaVersion: number
  readonly parameters: unknown
  readonly provenance: unknown
}

export interface ViewVersionList {
  readonly ordering: 'semver' | 'ordinal' // 'semver' when every retained version parses as semver; 'ordinal' fallback otherwise
  readonly versions: readonly ViewDefinitionSummary[]
}

export class ViewsAdminError extends Error {
  constructor(public readonly status: number, message: string) { super(message); this.name = 'ViewsAdminError' }
}

export interface ViewsAdminClient {
  listDefinitions(signal?: AbortSignal): Promise<readonly ViewDefinitionSummary[]>
  getDefinition(key: string, signal?: AbortSignal): Promise<ViewDefinitionDetail>
  listVersions(key: string, signal?: AbortSignal): Promise<ViewVersionList>
}
