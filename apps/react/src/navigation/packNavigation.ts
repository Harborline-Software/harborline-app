import type { PackNavigationDeclaration } from '@harborline-software/ui-react'
import { createSelectedSessionTransport } from '../../../shared/selected-session-transport.mjs'

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
  signal?.throwIfAborted()
  const transport = createSelectedSessionTransport((url, options) => globalThis.fetch(url, { ...options, signal }))
  const response = await transport.send('/api/local-node/navigation/workspaces')
  signal?.throwIfAborted()
  if (response.status < 200 || response.status >= 300) throw new Error('Unable to load application navigation. Retry the request.')
  const result = JSON.parse(response.body) as PackNavigationResponse
  if (result.configured && result.pack === null) throw new Error('The navigation service returned no declaration.')
  return result.configured ? result.pack : null
}
