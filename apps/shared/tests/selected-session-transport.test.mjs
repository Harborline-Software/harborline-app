import assert from 'node:assert/strict'
import { test } from 'node:test'
import { createSelectedSessionTransport } from '../selected-session-transport.mjs'

test('forwards only the declared safe idempotency header and refuses credential overrides before issuance', async () => {
  let calls = 0
  const transport = createSelectedSessionTransport(async (path, options) => {
    calls++
    if (path.endsWith('/antiforgery')) return new Response(null, { status: 204,
      headers: { 'X-Harborline-Antiforgery': 'server-token' } })
    assert.equal(options.headers['Idempotency-Key'], 'example-submit-v1')
    return new Response('{}')
  })
  for (const header of ['Authorization', 'Cookie', 'X-Harborline-Antiforgery', 'X-Forwarded-Host', 'idempotency-key']) {
    await assert.rejects(transport.send('/api/session/actions/apply', 'POST', '{}', 'application/json', { [header]: 'unsafe' }),
      /selected_session_headers_invalid/)
  }
  assert.equal(calls, 0)
  await transport.send('/api/session/actions/apply', 'POST', '{}', 'application/json', { 'Idempotency-Key': 'example-submit-v1' })
  assert.equal(calls, 2)
})

test('serializes one-time antiforgery issuance and mutations, including a denial', async () => {
  const sent = []
  let token = 0
  const transport = createSelectedSessionTransport(async (path, options) => {
    sent.push({ path, options })
    assert.equal(options.credentials, 'same-origin')
    assert.equal(options.redirect, 'error')
    assert.equal(options.headers.Authorization, undefined)
    if (path.endsWith('/antiforgery')) {
      token++
      return new Response(null, { status: 204, headers: { 'X-Harborline-Antiforgery': `token-${token}` } })
    }
    assert.equal(options.headers['X-Harborline-Antiforgery'], `token-${token}`)
    return new Response('{"code":"authorization.permission_required","auditId":"server-42"}', {
      status: 403, headers: { 'X-Harborline-Audit-Id': 'server-42' },
    })
  })
  const results = await Promise.all([
    transport.send('/api/session/actions/first', 'POST', '{}'),
    transport.send('/api/session/actions/second', 'POST', '{}'),
  ])
  assert.deepEqual(sent.map(request => request.path), [
    '/api/selected-node/session/antiforgery', '/api/selected-node/session/actions/first',
    '/api/selected-node/session/antiforgery', '/api/selected-node/session/actions/second',
  ])
  assert.deepEqual(results, Array(2).fill({
    status: 403, body: '{"code":"authorization.permission_required","auditId":"server-42"}', auditId: 'server-42', correlationId: null,
  }))
})

test('an expired selected session never attempts a mutation or retries it as bootstrap', async () => {
  const sent = []
  const transport = createSelectedSessionTransport(async path => {
    sent.push(path)
    return new Response('{"error":"selected_session_required"}', { status: 401 })
  })
  assert.equal((await transport.send('/api/session/actions/example', 'POST', '{}')).status, 401)
  assert.deepEqual(sent, ['/api/selected-node/session/antiforgery'])
})

test('reads use the browser selected session without minting or retaining a token', async () => {
  const sent = []
  const transport = createSelectedSessionTransport(async (path, options) => {
    sent.push({ path, options })
    return new Response('{"rows":[]}', { status: 200 })
  })
  assert.equal((await transport.send('/api/local-node/records/entities/example')).status, 200)
  assert.equal(sent.length, 1)
  assert.equal(sent[0].path, '/api/selected-node/local-node/records/entities/example')
  assert.equal(sent[0].options.headers['X-Harborline-Antiforgery'], undefined)
})

test('unknown methods and arbitrary targets are inert before fetch', async () => {
  const transport = createSelectedSessionTransport(() => assert.fail('Invalid dispatch must be inert.'))
  for (const path of ['https://hostile.example', '//hostile.example', '/api/local-node/../session', '/api/local-node/%2e%2e/session'])
    await assert.rejects(transport.send(path), /invalid/)
  await assert.rejects(transport.send('/api/session/actions/example', 'CONNECT'), /invalid/)
})

test('safe correlation survives request and server receipt without accepting malformed or duplicate values', async () => {
  const id = '43300000-0000-4000-8000-000000000010'
  let calls = 0
  const transport = createSelectedSessionTransport(async (_path, options) => {
    calls++
    assert.equal(options.headers['X-Correlation-ID'], id)
    return new Response('{}', { headers: { 'X-Harborline-Audit-Correlation': id } })
  })
  for (const value of ['bad', '00000000-0000-0000-0000-000000000000', `${id}, ${id}`])
    await assert.rejects(transport.send('/api/session/example', 'GET', null, 'application/json', { 'X-Correlation-ID': value }), /headers_invalid/)
  assert.equal(calls, 0)
  assert.equal((await transport.send('/api/session/example', 'GET', null, 'application/json', { 'X-Correlation-ID': id })).correlationId, id)
})
