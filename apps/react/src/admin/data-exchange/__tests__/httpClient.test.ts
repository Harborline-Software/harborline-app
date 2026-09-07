import { describe, expect, it, vi } from 'vitest'
import { createHttpDataExchangeAdminClient } from '../client/httpClient'
import { DataExchangeAdminError } from '../client/types'

function jsonResponse(body: unknown, init?: ResponseInit): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' }, ...init })
}

describe('createHttpDataExchangeAdminClient', () => {
  it('calls and maps the definitions route', async () => {
    const row = { key: 'bank-feed-import', version: '2.1.0', title: 'Bank feed import', exchangeKind: 'import', cascadeLayer: 'Tenant' }
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse([row]))
    const client = createHttpDataExchangeAdminClient({ baseUrl: 'https://node.test', headers: { 'x-test': 'yes' }, fetchImpl })

    await expect(client.listDefinitions()).resolves.toEqual([row])
    expect(fetchImpl).toHaveBeenCalledWith('https://node.test/api/local-node/data-exchange/definitions', { method: 'GET', headers: { 'x-test': 'yes' }, signal: undefined })
  })

  it('URL-encodes the key and maps definition detail', async () => {
    const detail = { key: 'trial balance/a', version: '2.1.0', title: 'Bank feed import', exchangeKind: 'import', cascadeLayer: 'Tenant', schemaVersion: 1, settings: { mapping: 'csv-standard' }, provenance: { tier: 'Vendor' } }
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(detail))
    const client = createHttpDataExchangeAdminClient({ fetchImpl })

    await expect(client.getDefinition('trial balance/a')).resolves.toEqual(detail)
    expect(fetchImpl).toHaveBeenCalledWith('/api/local-node/data-exchange/definitions/trial%20balance%2Fa', { method: 'GET', headers: undefined, signal: undefined })
  })

  it('calls and maps the versions route', async () => {
    const body = { ordering: 'semver', versions: [{ key: 'bank-feed-import', version: '2.1.0', title: 'Bank feed import', exchangeKind: 'import', cascadeLayer: 'Tenant' }] }
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(body))
    const client = createHttpDataExchangeAdminClient({ fetchImpl })

    await expect(client.listVersions('bank-feed-import')).resolves.toEqual(body)
    expect(fetchImpl).toHaveBeenCalledWith('/api/local-node/data-exchange/definitions/bank-feed-import/versions', { method: 'GET', headers: undefined, signal: undefined })
  })

  it('maps a machine-code error body to DataExchangeAdminError', async () => {
    // Ticket 092: the wire carries a code, never a sentence. This test used to assert
    // an English message, which is how three families shipped prose nobody could localize.
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ code: 'data_exchange_definition.not_found' }, { status: 404, statusText: 'Not Found' }))
    const rejection = createHttpDataExchangeAdminClient({ fetchImpl }).getDefinition('nope')
    await expect(rejection).rejects.toBeInstanceOf(DataExchangeAdminError)
    await expect(rejection).rejects.toMatchObject({ status: 404, message: 'data_exchange_definition.not_found' })
  })

  it('falls back to statusText when an error body is unparseable', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(new Response('not json', { status: 500, statusText: 'Server Error' }))
    await expect(createHttpDataExchangeAdminClient({ fetchImpl }).listDefinitions()).rejects.toMatchObject({ status: 500, message: 'Server Error' })
  })
})
