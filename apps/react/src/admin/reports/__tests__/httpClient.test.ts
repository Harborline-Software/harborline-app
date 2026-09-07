import { describe, expect, it, vi } from 'vitest'
import { createHttpReportsAdminClient } from '../client/httpClient'
import { ReportsAdminError } from '../client/types'

function jsonResponse(body: unknown, init?: ResponseInit): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' }, ...init })
}

describe('createHttpReportsAdminClient', () => {
  it('calls and maps the definitions route', async () => {
    const row = { key: 'trial-balance', version: '2.1.0', title: 'Trial balance', reportKind: 'standard', cascadeLayer: 'Tenant' }
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse([row]))
    const client = createHttpReportsAdminClient({ baseUrl: 'https://node.test', headers: { 'x-test': 'yes' }, fetchImpl })

    await expect(client.listDefinitions()).resolves.toEqual([row])
    expect(fetchImpl).toHaveBeenCalledWith('https://node.test/api/local-node/reports/definitions', { method: 'GET', headers: { 'x-test': 'yes' }, signal: undefined })
  })

  it('URL-encodes the key and maps definition detail', async () => {
    const detail = { key: 'trial balance/a', version: '2.1.0', title: 'Trial balance', reportKind: 'standard', cascadeLayer: 'Tenant', schemaVersion: 1, parameters: { chartId: 'chart-7' }, provenance: { tier: 'Vendor' } }
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(detail))
    const client = createHttpReportsAdminClient({ fetchImpl })

    await expect(client.getDefinition('trial balance/a')).resolves.toEqual(detail)
    expect(fetchImpl).toHaveBeenCalledWith('/api/local-node/reports/definitions/trial%20balance%2Fa', { method: 'GET', headers: undefined, signal: undefined })
  })

  it('calls and maps the versions route', async () => {
    const body = { ordering: 'semver', versions: [{ key: 'trial-balance', version: '2.1.0', title: 'Trial balance', reportKind: 'standard', cascadeLayer: 'Tenant' }] }
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(body))
    const client = createHttpReportsAdminClient({ fetchImpl })

    await expect(client.listVersions('trial-balance')).resolves.toEqual(body)
    expect(fetchImpl).toHaveBeenCalledWith('/api/local-node/reports/definitions/trial-balance/versions', { method: 'GET', headers: undefined, signal: undefined })
  })

  it('maps a machine-code error body to ReportsAdminError', async () => {
    // Ticket 092: the wire carries a code, never a sentence. This test used to assert
    // an English message, which is how three families shipped prose nobody could localize.
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ code: 'report_definition.not_found' }, { status: 404, statusText: 'Not Found' }))
    const rejection = createHttpReportsAdminClient({ fetchImpl }).getDefinition('nope')
    await expect(rejection).rejects.toBeInstanceOf(ReportsAdminError)
    await expect(rejection).rejects.toMatchObject({ status: 404, message: 'report_definition.not_found' })
  })

  it('falls back to statusText when an error body is unparseable', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(new Response('not json', { status: 500, statusText: 'Server Error' }))
    await expect(createHttpReportsAdminClient({ fetchImpl }).listDefinitions()).rejects.toMatchObject({ status: 500, message: 'Server Error' })
  })
})
