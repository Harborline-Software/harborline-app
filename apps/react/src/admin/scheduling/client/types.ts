import type { AdminErrorDetail } from '../../adminErrorEnvelope'
export interface SchedulingDefinitionSummary {
  readonly id: string
  readonly revision: number                // integer dialect — no semver, no status (ticket 088)
  readonly title: string
  readonly updatedAt: string               // ISO-8601 round-trip, UTC
  readonly updatedBy: string
}

export interface SchedulingDefinitionView {
  readonly id: string
  readonly revision: number
  readonly definition: unknown             // raw definition JSON body
  readonly updatedAt: string
  readonly updatedBy: string
}

export interface RestoreResult {
  readonly definitionId: string
  readonly revision: number                // the minted head revision
  readonly restoredFrom: number
}

/**
 * Ticket 094: the api answers `{ code, detail? }` — a stable machine code the client localizes, plus
 * named parameters. `message` is the human-readable rendering of that pair.
 */
export class SchedulingAdminError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly code: string | null = null,
    public readonly detail: AdminErrorDetail | null = null,
  ) { super(message); this.name = 'SchedulingAdminError' }
}

export interface SchedulingAdminClient {
  listDefinitions(signal?: AbortSignal): Promise<readonly SchedulingDefinitionSummary[]>
  getDefinition(definitionId: string, signal?: AbortSignal): Promise<SchedulingDefinitionView>
  listVersions(definitionId: string, signal?: AbortSignal): Promise<readonly SchedulingDefinitionSummary[]>
  restoreRevision(definitionId: string, revision: number, signal?: AbortSignal): Promise<RestoreResult>
}
