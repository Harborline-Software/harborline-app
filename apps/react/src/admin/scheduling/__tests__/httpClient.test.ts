import { describe, expect, it, vi } from 'vitest'
import { createHttpSchedulingAdminClient } from '../client/httpClient'
import { SchedulingAdminError } from '../client/types'

function jsonResponse(body: unknown, init?: ResponseInit): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'content-type': 'application/json' },
    ...init,
  })
}

describe('createHttpSchedulingAdminClient', () => {
  it('calls and maps the definitions route', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse([{
      id: 'inspection-protocol',
      revision: 3,
      title: 'Inspection protocol',
      updatedAt: '2026-08-18T09:00:00Z',
      updatedBy: 'user:fixture-scheduler',
    }]))
    const client = createHttpSchedulingAdminClient({ baseUrl: 'https://node.test', headers: { 'x-test': 'yes' }, fetchImpl })

    await expect(client.listDefinitions()).resolves.toEqual([{
      id: 'inspection-protocol',
      revision: 3,
      title: 'Inspection protocol',
      updatedAt: '2026-08-18T09:00:00Z',
      updatedBy: 'user:fixture-scheduler',
    }])
    expect(fetchImpl).toHaveBeenCalledWith(
      'https://node.test/api/local-node/scheduling/definitions',
      { method: 'GET', headers: { 'x-test': 'yes' }, signal: undefined },
    )
  })

  it('URL-encodes the definition id on the versions route', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse([]))
    const client = createHttpSchedulingAdminClient({ fetchImpl })

    await client.listVersions('inspection protocol/a')
    expect(fetchImpl).toHaveBeenCalledWith(
      '/api/local-node/scheduling/definitions/inspection%20protocol%2Fa/versions',
      { method: 'GET', headers: undefined, signal: undefined },
    )
  })

  it('calls and maps the single-definition route', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({
      id: 'inspection-protocol',
      revision: 3,
      definition: { title: 'Inspection protocol', cadence: 'weekly' },
      updatedAt: '2026-08-18T09:00:00Z',
      updatedBy: 'user:fixture-scheduler',
    }))
    const client = createHttpSchedulingAdminClient({ fetchImpl })

    await expect(client.getDefinition('inspection-protocol')).resolves.toEqual({
      id: 'inspection-protocol',
      revision: 3,
      definition: { title: 'Inspection protocol', cadence: 'weekly' },
      updatedAt: '2026-08-18T09:00:00Z',
      updatedBy: 'user:fixture-scheduler',
    })
    expect(fetchImpl).toHaveBeenCalledWith(
      '/api/local-node/scheduling/definitions/inspection-protocol',
      { method: 'GET', headers: undefined, signal: undefined },
    )
  })

  it('posts the exact restore route and JSON body', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ definitionId: 'a/b', revision: 4, restoredFrom: 1 }))
    const client = createHttpSchedulingAdminClient({ baseUrl: 'https://node.test', headers: { 'x-test': 'yes' }, fetchImpl })

    await expect(client.restoreRevision('a/b', 1)).resolves.toEqual({ definitionId: 'a/b', revision: 4, restoredFrom: 1 })
    expect(fetchImpl).toHaveBeenCalledWith(
      'https://node.test/api/local-node/scheduling/definitions/a%2Fb/restore',
      {
        method: 'POST',
        headers: { 'x-test': 'yes', 'content-type': 'application/json' },
        body: '{"revision":1}',
        signal: undefined,
      },
    )
  })

  it('maps a plain code envelope to the bare code', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(
      { code: 'scheduling.draft.not_found' },
      { status: 404, statusText: 'Not Found' },
    ))
    const client = createHttpSchedulingAdminClient({ fetchImpl })

    const rejection = client.getDefinition('nope')
    await expect(rejection).rejects.toBeInstanceOf(SchedulingAdminError)
    await expect(rejection).rejects.toMatchObject({
      status: 404,
      message: 'scheduling.draft.not_found',
      code: 'scheduling.draft.not_found',
      detail: null,
    })
  })

  it('carries the named detail fields of a parameterized diagnostic', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(
      { code: 'scheduling.definition.invalid', detail: { key: 'occupancy', maxLength: 200 } },
      { status: 400, statusText: 'Bad Request' },
    ))
    const client = createHttpSchedulingAdminClient({ fetchImpl })

    await expect(client.getDefinition('nope')).rejects.toMatchObject({
      status: 400,
      message: 'scheduling.definition.invalid (key=occupancy, maxLength=200)',
      code: 'scheduling.definition.invalid',
      detail: { key: 'occupancy', maxLength: 200 },
    })
  })

  it('falls back to statusText when the body carries no code', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(
      { error: 'Scheduling definition not found.' },
      { status: 404, statusText: 'Not Found' },
    ))
    const client = createHttpSchedulingAdminClient({ fetchImpl })

    await expect(client.getDefinition('nope')).rejects.toMatchObject({
      status: 404,
      message: 'Not Found',
      code: null,
      detail: null,
    })
  })

  it('reports an unmapped family rather than an anonymous 404 on the list route', async () => {
    // The node registers the scheduling family only behind LocalNode:SchedulingDogfood:Enabled,
    // false in shipped config — so this 404 means "flip the flag", not "no definitions".
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(
      new Response('', { status: 404, statusText: 'Not Found' }),
    )
    const client = createHttpSchedulingAdminClient({ fetchImpl })

    await expect(client.listDefinitions()).rejects.toMatchObject({
      status: 404,
      message: 'scheduling.family_not_enabled',
    })
  })

  it('leaves a 404 on a DETAIL route alone — only the list route means unmapped', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(
      { code: 'scheduling.draft.not_found' },
      { status: 404, statusText: 'Not Found' },
    ))
    const client = createHttpSchedulingAdminClient({ fetchImpl })

    await expect(client.getDefinition('nope')).rejects.toMatchObject({
      status: 404,
      message: 'scheduling.draft.not_found',
    })
  })

  it('falls back to statusText when an error body is unparseable', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(new Response('not json', { status: 500, statusText: 'Server Error' }))
    const client = createHttpSchedulingAdminClient({ fetchImpl })

    await expect(client.listDefinitions()).rejects.toMatchObject({ status: 500, message: 'Server Error' })
  })
})
