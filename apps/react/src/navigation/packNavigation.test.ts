import { afterEach, expect, it, vi } from 'vitest'
import { readPackNavigation } from './packNavigation'

afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs() })

it('uses only the selected-session navigation route with cancellable same-origin credentials', async () => {
  vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'http://node.example')
  const request = vi.fn(async () => Response.json({ configured: true, pack: { seedWorkspaces: [] } }))
  vi.stubGlobal('fetch', request)
  const cancellation = new AbortController()
  await expect(readPackNavigation(cancellation.signal)).resolves.toEqual({ seedWorkspaces: [] })
  expect(request).toHaveBeenCalledWith('/api/selected-node/local-node/navigation/workspaces', expect.objectContaining({
    credentials: 'same-origin', cache: 'no-store', redirect: 'error', signal: cancellation.signal,
  }))
})

it('does not retry a selected-session navigation denial as a desktop request', async () => {
  vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'http://node.example')
  const request = vi.fn(async () => Response.json({ code: 'session.required' }, { status: 403 }))
  vi.stubGlobal('fetch', request)
  await expect(readPackNavigation()).rejects.toThrow('Unable to load application navigation. Retry the request.')
  expect(request).toHaveBeenCalledTimes(1)
  expect(request).toHaveBeenCalledWith('/api/selected-node/local-node/navigation/workspaces', expect.any(Object))
})
