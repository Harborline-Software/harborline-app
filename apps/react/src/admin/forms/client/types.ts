export interface InternationalizedText {
  readonly defaultLocale: string
  readonly values: Readonly<Record<string, string>>
}

export interface FormDefinitionSummary {
  readonly formId: string
  readonly version: string                 // "major.minor.patch"
  readonly title: InternationalizedText | null
  readonly updatedAt: string               // ISO-8601 round-trip, UTC
  readonly cascadeLayer: string | null     // server-supplied envelope layer; absent key -> null
}

export interface FormVersionSummary {
  readonly formId: string
  readonly version: string
  readonly status: 'Draft' | 'Published' | 'Deprecated' | 'Withdrawn'
  readonly owner: string | null            // IdentityRef.ToString(); absent key -> null
  readonly createdAt: string
  readonly updatedAt: string
  readonly derivedFrom: string | null      // restore provenance (source version)
  readonly syncsToPeers: boolean           // wire: hard-coded true
  readonly safeForStaging: boolean         // wire: hard-coded false
}

export interface RestoreResult { readonly formId: string; readonly version: string }

/** Named detail fields the api attaches to a code (`header`, `maxLength`, `formId`, `field`, ...). */
export type FormsErrorDetail = Readonly<Record<string, unknown>>

/**
 * Ticket 094: the api answers `{ code, detail? }` — a stable machine code the client localizes,
 * plus named parameters. `message` is the human-readable rendering of that pair.
 */
export class FormsAdminError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly code: string | null = null,
    public readonly detail: FormsErrorDetail | null = null,
  ) { super(message); this.name = 'FormsAdminError' }
}

export interface FormsAdminClient {
  listDefinitions(signal?: AbortSignal): Promise<readonly FormDefinitionSummary[]>
  listVersions(formId: string, signal?: AbortSignal): Promise<readonly FormVersionSummary[]>
  restoreVersion(formId: string, version: string, signal?: AbortSignal): Promise<RestoreResult>
}
