import { afterEach, expect, it, vi } from 'vitest'

afterEach(() => { vi.unstubAllGlobals(); vi.resetModules() })

it('cancels a detail projection during token issuance before the POST can dispatch', async () => {
  vi.resetModules()
  let started!: () => void, release!: (response: Response) => void
  const entered = new Promise<void>(resolve => { started = resolve })
  const paused = new Promise<Response>(resolve => { release = resolve })
  const fetcher = vi.fn(async (url: string) => {
    if (url.endsWith('/session/antiforgery')) { started(); return paused }
    return Response.json({ projected: true })
  })
  vi.stubGlobal('fetch', fetcher)
  const { readSelectedCatalogue } = await import('../selectedCatalogue')
  const cancellation = new AbortController()
  const pending = readSelectedCatalogue('/api/local-node/catalogue/details/platform.detail.form/1.0.0', cancellation.signal, [])
  await entered
  cancellation.abort()
  release(new Response(null, { headers: { 'X-Harborline-Antiforgery': 'token' } }))
  await expect(pending).rejects.toMatchObject({ name: 'AbortError' })
  expect(fetcher.mock.calls.map(([url]) => url)).toEqual(['/api/selected-node/session/antiforgery'])
})

it('serializes detail projection and pack actions through one antiforgery queue', async () => {
  vi.resetModules()
  let started!: () => void, release!: () => void
  const entered = new Promise<void>(resolve => { started = resolve })
  const paused = new Promise<void>(resolve => { release = resolve })
  let token = 0
  const fetcher = vi.fn(async (url: string, options: RequestInit) => {
    if (url.endsWith('/session/antiforgery'))
      return new Response(null, { headers: { 'X-Harborline-Antiforgery': `token-${++token}` } })
    if (url.includes('/catalogue/details/')) { started(); await paused }
    expect(new Headers(options.headers).get('X-Harborline-Antiforgery')).toBe(`token-${token}`)
    return Response.json({ projected: true })
  })
  vi.stubGlobal('fetch', fetcher)
  const { readSelectedCatalogue } = await import('../selectedCatalogue')
  const { send } = await import('../../../../shared/selected-session-transport.mjs')
  const detail = readSelectedCatalogue('/api/local-node/catalogue/details/platform.detail.form/1.0.0', undefined, [])
  await entered
  const action = send('/api/session/admin/grants/review', 'POST', '{}')
  try {
    await new Promise(resolve => setTimeout(resolve, 0))
    expect(fetcher.mock.calls.map(([url]) => url)).toEqual([
      '/api/selected-node/session/antiforgery',
      '/api/selected-node/local-node/catalogue/details/platform.detail.form/1.0.0',
    ])
  } finally { release(); await Promise.allSettled([detail, action]) }
  await expect(detail).resolves.toEqual({ projected: true })
  await expect(action).resolves.toMatchObject({ status: 200 })
  expect(token).toBe(2)
})

it('late-binds browser fetch without capturing an earlier session transport implementation', async () => {
  vi.resetModules()
  const oldFetch = vi.fn(async () => Response.json({ stale: true }))
  vi.stubGlobal('fetch', oldFetch)
  const { send } = await import('../../../../shared/selected-session-transport.mjs')
  const selectedFetch = vi.fn(async () => Response.json({ current: true }))
  vi.stubGlobal('fetch', selectedFetch)
  await expect(send('/api/session/whoami')).resolves.toMatchObject({ body: '{"current":true}' })
  expect(oldFetch).not.toHaveBeenCalled()
})
