import { describe, expect, it, vi } from 'vitest'
import { createHttpFormsAdminClient } from '../client/httpClient'
import { FormsAdminError } from '../client/types'

function jsonResponse(body: unknown, init?: ResponseInit): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'content-type': 'application/json' },
    ...init,
  })
}

describe('createHttpFormsAdminClient', () => {
  it('calls and maps the definitions route, reading cascadeLayer off the wire', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse([{
      formId: 'incident-intake',
      version: '1.0.3',
      title: { defaultLocale: 'en', values: { en: 'Incident intake' } },
      updatedAt: '2026-08-05T12:00:00Z',
      cascadeLayer: 'Pack',
    }]))
    const client = createHttpFormsAdminClient({ baseUrl: 'https://node.test', headers: { 'x-test': 'yes' }, fetchImpl })

    await expect(client.listDefinitions()).resolves.toEqual([{
      formId: 'incident-intake',
      version: '1.0.3',
      title: { defaultLocale: 'en', values: { en: 'Incident intake' } },
      updatedAt: '2026-08-05T12:00:00Z',
      // Server-supplied — rendered, never fabricated (ticket 153).
      cascadeLayer: 'Pack',
    }])
    expect(fetchImpl).toHaveBeenCalledWith(
      'https://node.test/api/local-node/forms/definitions',
      { method: 'GET', headers: { 'x-test': 'yes' }, signal: undefined },
    )
  })

  it('maps an absent cascadeLayer to null instead of inventing one', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse([{
      formId: 'crew-manifest',
      version: '1.0.0',
      updatedAt: '2026-08-03T08:00:00Z',
    }]))
    const client = createHttpFormsAdminClient({ fetchImpl })

    const rows = await client.listDefinitions()
    expect(rows[0]).toMatchObject({ cascadeLayer: null, title: null })
  })

  it('URL-encodes the form id on the versions route and maps absent keys to null', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse([{
      formId: 'incident intake/a',
      version: '1.0.0',
      status: 'Published',
      createdAt: '2026-08-01T08:00:00Z',
      updatedAt: '2026-08-01T08:00:00Z',
      syncsToPeers: true,
      safeForStaging: false,
    }]))
    const client = createHttpFormsAdminClient({ fetchImpl })

    const rows = await client.listVersions('incident intake/a')
    expect(fetchImpl).toHaveBeenCalledWith(
      '/api/local-node/forms/definitions/incident%20intake%2Fa/versions',
      { method: 'GET', headers: undefined, signal: undefined },
    )
    expect(rows[0]).toMatchObject({ owner: null, derivedFrom: null })
  })

  it('posts the exact restore route and JSON body', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ formId: 'a/b', version: '1.0.1' }))
    const client = createHttpFormsAdminClient({ baseUrl: 'https://node.test', headers: { 'x-test': 'yes' }, fetchImpl })

    await expect(client.restoreVersion('a/b', '1.0.0')).resolves.toEqual({ formId: 'a/b', version: '1.0.1' })
    expect(fetchImpl).toHaveBeenCalledWith(
      'https://node.test/api/local-node/forms/definitions/a%2Fb/restore',
      {
        method: 'POST',
        headers: { 'x-test': 'yes', 'content-type': 'application/json' },
        body: '{"version":"1.0.0"}',
        signal: undefined,
      },
    )
  })

  // Ticket 094: the api answers `{ code, detail? }` on every Forms error (api 2b184725).
  it('maps a plain code envelope to FormsAdminError', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(
      { code: 'form_definition.revision_conflict' },
      { status: 409, statusText: 'Conflict' },
    ))
    const client = createHttpFormsAdminClient({ fetchImpl })

    const rejection = client.restoreVersion('incident-intake', '1.0.0')
    await expect(rejection).rejects.toBeInstanceOf(FormsAdminError)
    await expect(rejection).rejects.toMatchObject({
      status: 409,
      code: 'form_definition.revision_conflict',
      detail: null,
      message: 'form_definition.revision_conflict',
    })
  })

  it('carries the named detail fields of a parameterized (header) diagnostic', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(
      { code: 'forms.idempotency_key_too_long', detail: { header: 'Idempotency-Key', maxLength: 200 } },
      { status: 400, statusText: 'Bad Request' },
    ))
    const client = createHttpFormsAdminClient({ fetchImpl })

    const rejection = client.restoreVersion('incident-intake', '1.0.0')
    await expect(rejection).rejects.toMatchObject({
      status: 400,
      code: 'forms.idempotency_key_too_long',
      detail: { header: 'Idempotency-Key', maxLength: 200 },
      message: 'forms.idempotency_key_too_long (header=Idempotency-Key, maxLength=200)',
    })
  })

  it('falls back to statusText when the body carries no code', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(
      { error: 'Revision cannot be restored.' },
      { status: 409, statusText: 'Conflict' },
    ))
    const client = createHttpFormsAdminClient({ fetchImpl })

    const rejection = client.restoreVersion('incident-intake', '1.0.0')
    await expect(rejection).rejects.toMatchObject({ status: 409, code: null, message: 'Conflict' })
  })

  it('falls back to statusText when an error body is unparseable', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(new Response('not json', { status: 500, statusText: 'Server Error' }))
    const client = createHttpFormsAdminClient({ fetchImpl })

    await expect(client.listDefinitions()).rejects.toMatchObject({ status: 500, message: 'Server Error' })
  })
})
