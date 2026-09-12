/**
 * The list routes already disclose the three facts needed for the first-run copy. A successful
 * response proves that the tenant has installed content and that this viewer has a permitting
 * grant; its row count tells us whether anything has been authored. A missing route or a refusal
 * carries the two other observable cases. No first-run API is required.
 */
export interface FirstRunObservation {
  readonly hasInstalledContent: boolean
  readonly hasAuthoredContent: boolean
  readonly viewerHasPermittingGrant: boolean
}

export type TenantFirstRunState =
  | 'installer'
  | 'platform-installed'
  | 'access-preloaded'
  | 'administrator-ready'
  | 'domain-installed'

const EMPTY_STATE_COPY: Readonly<Record<TenantFirstRunState, string>> = {
  installer: 'First-run state 1: nothing is installed yet. Use the installer to add the platform package.',
  'platform-installed': 'First-run state 2: you hold no grant that permits this list.',
  'access-preloaded': 'First-run state 3: access surfaces are preloaded, but you hold no grant that permits this list.',
  'administrator-ready': 'First-run state 4: nothing is authored yet. You can author the first definition.',
  'domain-installed': 'First-run state 5: a domain package is installed, but this list has no matching definitions yet.',
}

export function tenantFirstRunState(observation: FirstRunObservation): TenantFirstRunState {
  if (!observation.hasInstalledContent) return 'installer'
  if (!observation.hasAuthoredContent && !observation.viewerHasPermittingGrant) return 'platform-installed'
  if (observation.hasAuthoredContent && !observation.viewerHasPermittingGrant) return 'access-preloaded'
  if (!observation.hasAuthoredContent) return 'administrator-ready'
  return 'domain-installed'
}

export function firstRunEmptyStateCopy(observation: FirstRunObservation): string {
  return EMPTY_STATE_COPY[tenantFirstRunState(observation)]
}

export function firstRunObservationForList(rows: readonly unknown[]): FirstRunObservation {
  return { hasInstalledContent: true, hasAuthoredContent: rows.length > 0, viewerHasPermittingGrant: true }
}

export function firstRunObservationForListFailure(error: unknown): FirstRunObservation | null {
  const status = typeof error === 'object' && error !== null && 'status' in error && typeof error.status === 'number'
    ? error.status
    : null
  if (status === 404) return { hasInstalledContent: false, hasAuthoredContent: false, viewerHasPermittingGrant: false }
  if (status === 403) return { hasInstalledContent: true, hasAuthoredContent: false, viewerHasPermittingGrant: false }
  return null
}
