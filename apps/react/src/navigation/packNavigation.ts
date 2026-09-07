import type { PackNavigationDeclaration } from '@harborline-software/ui-react'

/** Wire envelope from the active tenant's ordinary pack composition. */
export interface PackNavigationResponse {
  readonly configured: boolean
  readonly pack: PackNavigationDeclaration | null
}

export async function readPackNavigation(signal?: AbortSignal): Promise<PackNavigationDeclaration | null> {
  const origin = import.meta.env.VITE_AUTHORIZATION_API_ORIGIN
  if (!origin) {
    const fixture = import.meta.env.VITE_AUTHORIZATION_FIXTURE
    if (fixture === '1' || fixture === 'true') return null
    throw new Error('Pack navigation requires the configured authorization service.')
  }
  const response = await fetch(`${import.meta.env.DEV ? '' : origin}/api/local-node/navigation/workspaces`, { signal })
  if (!response.ok) throw new Error('Unable to load application navigation. Retry the request.')
  const result = await response.json() as PackNavigationResponse
  if (result.configured && result.pack === null) throw new Error('The navigation service returned no declaration.')
  return result.configured ? result.pack : null
}
