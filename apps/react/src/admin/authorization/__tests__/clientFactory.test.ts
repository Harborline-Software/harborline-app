import { afterEach, describe, expect, it, vi } from 'vitest'
import { createAuthorizationAdminClient } from '../client'

describe('authorization admin client factory', () => {
  afterEach(() => { vi.unstubAllEnvs(); vi.unstubAllGlobals() })

  it.each([true, false])('uses only selected-session same-origin reads with DEV=%s', async development => {
    vi.stubEnv('DEV', development)
    vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'https://node.example')
    const request = vi.fn<typeof fetch>(async () => Response.json([]))
    vi.stubGlobal('fetch', request)
    const signal = new AbortController().signal
    const client = createAuthorizationAdminClient()

    await client.listRoleVocabulary(signal)
    await client.listHolders(signal)
    await client.listCapabilityDefinitions(signal)
    await client.getEffectiveBinding('definition', signal)
    await client.listStandingCatalogue(signal)
    await client.readTrace('audit')

    expect(request.mock.calls.map(call => String(call[0]))).toEqual([
      '/api/selected-node/local-node/authorization/role-vocabulary',
      '/api/selected-node/local-node/authorization/holders',
      '/api/selected-node/local-node/authorization/capability-definitions',
      '/api/selected-node/local-node/authorization/capability-definitions/definition/binding',
      '/api/selected-node/local-node/authorization/standing-catalogue',
      '/api/selected-node/local-node/authorization/traces/audit',
    ])
    expect(request).toHaveBeenNthCalledWith(1, expect.any(String), expect.objectContaining({
      method: 'GET', credentials: 'same-origin', cache: 'no-store', redirect: 'error', signal,
    }))
  })

  it('uses the selected-session antiforgery flow for binding writes', async () => {
    vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'https://node.example')
    const request = vi.fn<typeof fetch>(async input => String(input).endsWith('/antiforgery')
      ? new Response(null, { headers: { 'X-Harborline-Antiforgery': 'selected-token' } })
      : Response.json({ revision: 2 }))
    vi.stubGlobal('fetch', request)

    await expect(createAuthorizationAdminClient().narrowCapabilityBinding('definition', [], 'narrow'))
      .resolves.toEqual({ revision: 2 })

    expect(request).toHaveBeenCalledTimes(2)
    expect(request).toHaveBeenNthCalledWith(1, '/api/selected-node/session/antiforgery', expect.any(Object))
    expect(request).toHaveBeenNthCalledWith(2, '/api/selected-node/local-node/authorization/capability-definitions/definition/binding',
      expect.objectContaining({ method: 'POST', credentials: 'same-origin',
        body: JSON.stringify({ selectedRoles: [], reason: 'narrow' }),
        headers: expect.objectContaining({ 'X-Harborline-Antiforgery': 'selected-token', 'Idempotency-Key': expect.any(String) }),
      }))
  })

  it('preserves selected-session denials without falling back to the node origin', async () => {
    vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'https://node.example')
    const request = vi.fn(async () => Response.json({ code: 'authorization.permission_required', auditId: 'audit' }, { status: 403 }))
    vi.stubGlobal('fetch', request)

    await expect(createAuthorizationAdminClient().listRoleVocabulary()).rejects.toMatchObject({ status: 403, auditId: 'audit' })
    expect(request).toHaveBeenCalledTimes(1)
    expect(request).toHaveBeenCalledWith('/api/selected-node/local-node/authorization/role-vocabulary', expect.any(Object))
  })

  it('fails closed when neither a live origin nor explicit fixture opt-in is configured', () => {
    vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', '')
    vi.stubEnv('VITE_AUTHORIZATION_FIXTURE', '')

    expect(() => createAuthorizationAdminClient()).toThrow(/VITE_AUTHORIZATION_API_ORIGIN|VITE_AUTHORIZATION_FIXTURE/)
  })

  it.each(['1', 'true'])('uses the fixture only for the explicit %s opt-in', async fixture => {
    vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', '')
    vi.stubEnv('VITE_AUTHORIZATION_FIXTURE', fixture)

    expect(await createAuthorizationAdminClient().listCapabilityDefinitions()).not.toHaveLength(0)
  })
})
