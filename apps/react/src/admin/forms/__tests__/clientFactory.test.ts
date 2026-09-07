import { afterEach, describe, expect, it, vi } from 'vitest'
import { createFormsAdminClient } from '../client'

// Ticket 153 (L1351): the fixture client is an explicit opt-in, never a silent fallback.
describe('createFormsAdminClient', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
    vi.unstubAllGlobals()
  })

  const okJson = () =>
    vi.fn().mockResolvedValue(
      new Response('[]', { status: 200, headers: { 'content-type': 'application/json' } }),
    )

  it('passes the configured origin through to the HTTP client in a production bundle', async () => {
    vi.stubEnv('VITE_FORMS_API_ORIGIN', 'http://127.0.0.1:5199')
    vi.stubEnv('VITE_FORMS_FIXTURE', '')
    vi.stubEnv('DEV', false) // a built bundle: no dev proxy — the origin itself must be called
    const fetchSpy = okJson()
    vi.stubGlobal('fetch', fetchSpy)

    const client = createFormsAdminClient()
    await client.listDefinitions()

    // The fetch call is the http client's distinguishing marker (the fixture never fetches),
    // and its URL proves the origin was not discarded (review fix: baseUrl '' made a
    // "configured" production bundle fetch its own origin).
    expect(fetchSpy).toHaveBeenCalledWith(
      'http://127.0.0.1:5199/api/local-node/forms/definitions',
      expect.objectContaining({ method: 'GET' }),
    )
  })

  it('keeps dev-mode requests same-origin so the vite proxy (and its node credential) stays in the path', async () => {
    vi.stubEnv('VITE_FORMS_API_ORIGIN', 'http://127.0.0.1:5199')
    vi.stubEnv('VITE_FORMS_FIXTURE', '')
    vi.stubEnv('DEV', true)
    const fetchSpy = okJson()
    vi.stubGlobal('fetch', fetchSpy)

    const client = createFormsAdminClient()
    await client.listDefinitions()

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/local-node/forms/definitions',
      expect.objectContaining({ method: 'GET' }),
    )
  })

  it('returns the fixture client only on the explicit VITE_FORMS_FIXTURE="1" opt-in', async () => {
    vi.stubEnv('VITE_FORMS_API_ORIGIN', '')
    vi.stubEnv('VITE_FORMS_FIXTURE', '1')

    const client = createFormsAdminClient()
    const rows = await client.listDefinitions()
    expect(rows.map(row => row.formId)).toContain('incident-intake')
  })

  it('honors VITE_FORMS_FIXTURE="true" too (symmetry with the Blazor lane\'s FormsAdmin:UseFixture)', async () => {
    vi.stubEnv('VITE_FORMS_API_ORIGIN', '')
    vi.stubEnv('VITE_FORMS_FIXTURE', 'true')

    const client = createFormsAdminClient()
    const rows = await client.listDefinitions()
    expect(rows.map(row => row.formId)).toContain('incident-intake')
  })

  it('throws a loud configuration error when neither origin nor fixture opt-in is set', () => {
    vi.stubEnv('VITE_FORMS_API_ORIGIN', '')
    vi.stubEnv('VITE_FORMS_FIXTURE', '')

    expect(() => createFormsAdminClient()).toThrowError(/VITE_FORMS_API_ORIGIN|VITE_FORMS_FIXTURE/)
  })
})
