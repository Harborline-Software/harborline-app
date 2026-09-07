import { describe, expect, it, vi } from 'vitest'
import { createHttpViewsAdminClient } from '../client/httpClient'
import { ViewsAdminError } from '../client/types'

function jsonResponse(body: unknown, init?: ResponseInit): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' }, ...init })
}

describe('createHttpViewsAdminClient', () => {
  it('calls and maps the definitions route', async () => {
    const row = { key: 'work-orders-table', version: '2.1.0', title: 'Work orders table', viewKind: 'table', cascadeLayer: 'Tenant' }
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse([row]))
    const client = createHttpViewsAdminClient({ baseUrl: 'https://node.test', headers: { 'x-test': 'yes' }, fetchImpl })

    await expect(client.listDefinitions()).resolves.toEqual([row])
    expect(fetchImpl).toHaveBeenCalledWith('https://node.test/api/local-node/views/definitions', { method: 'GET', headers: { 'x-test': 'yes' }, signal: undefined })
  })

  it('URL-encodes the key and maps definition detail', async () => {
    const detail = { key: 'trial balance/a', version: '2.1.0', title: 'Work orders table', viewKind: 'table', cascadeLayer: 'Tenant', schemaVersion: 1, parameters: { entityType: 'work-order' }, provenance: { tier: 'Vendor' } }
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(detail))
    const client = createHttpViewsAdminClient({ fetchImpl })

    await expect(client.getDefinition('trial balance/a')).resolves.toEqual(detail)
    expect(fetchImpl).toHaveBeenCalledWith('/api/local-node/views/definitions/trial%20balance%2Fa', { method: 'GET', headers: undefined, signal: undefined })
  })

  it('calls and maps the versions route', async () => {
    const body = { ordering: 'semver', versions: [{ key: 'work-orders-table', version: '2.1.0', title: 'Work orders table', viewKind: 'table', cascadeLayer: 'Tenant' }] }
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(body))
    const client = createHttpViewsAdminClient({ fetchImpl })

    await expect(client.listVersions('work-orders-table')).resolves.toEqual(body)
    expect(fetchImpl).toHaveBeenCalledWith('/api/local-node/views/definitions/work-orders-table/versions', { method: 'GET', headers: undefined, signal: undefined })
  })

  it('maps a machine-code error body to ViewsAdminError', async () => {
    // Ticket 092: the wire carries a code, never a sentence. This test used to assert
    // an English message, which is how three families shipped prose nobody could localize.
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ code: 'view_definition.not_found' }, { status: 404, statusText: 'Not Found' }))
    const rejection = createHttpViewsAdminClient({ fetchImpl }).getDefinition('nope')
    await expect(rejection).rejects.toBeInstanceOf(ViewsAdminError)
    await expect(rejection).rejects.toMatchObject({ status: 404, message: 'view_definition.not_found' })
  })

  it('falls back to statusText when an error body is unparseable', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(new Response('not json', { status: 500, statusText: 'Server Error' }))
    await expect(createHttpViewsAdminClient({ fetchImpl }).listDefinitions()).rejects.toMatchObject({ status: 500, message: 'Server Error' })
  })
})
