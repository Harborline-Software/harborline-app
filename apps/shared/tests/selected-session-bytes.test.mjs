import assert from 'node:assert/strict'
import { test } from 'node:test'
import { createSelectedSessionTransport } from '../selected-session-transport.mjs'

test('binary export preserves every byte including invalid UTF-8 and server audit headers', async () => {
  const bytes = Uint8Array.from({ length: 256 }, (_, index) => index)
  const transport = createSelectedSessionTransport(async () => new Response(bytes, {
    headers: { 'X-Harborline-Audit-Id': 'export-audit', 'X-Harborline-Audit-Correlation': 'export-correlation' },
  }))
  assert.equal(typeof transport.sendBytes, 'function')
  const result = await transport.sendBytes('/api/local-node/packs/export')
  assert.equal(result.status, 200)
  assert.equal(result.body, '')
  assert.deepEqual(result.bytes, bytes)
  assert.equal(result.auditId, 'export-audit')
  assert.equal(result.correlationId, 'export-correlation')
})

test('binary and text mutations share the same antiforgery rotation queue', async () => {
  const sent = []
  let token = 0, release, started
  const entered = new Promise(resolve => { started = resolve })
  const paused = new Promise(resolve => { release = resolve })
  const transport = createSelectedSessionTransport(async (path, options) => {
    sent.push(path)
    assert.equal(options.credentials, 'same-origin')
    assert.equal(options.headers.Authorization, undefined)
    if (path.endsWith('/antiforgery'))
      return new Response(null, { headers: { 'X-Harborline-Antiforgery': `token-${++token}` } })
    if (path.endsWith('/packs/export')) { started(); await paused }
    assert.equal(options.headers['X-Harborline-Antiforgery'], `token-${token}`)
    return new Response(path.endsWith('/packs/export') ? new Uint8Array([0, 255, 128]) : '{}')
  })
  assert.equal(typeof transport.sendBytes, 'function')
  const binary = transport.sendBytes('/api/local-node/packs/export', 'POST', '{}')
  await entered
  const text = transport.send('/api/session/actions/apply', 'POST', '{}')
  try {
    await new Promise(resolve => setImmediate(resolve))
    assert.deepEqual(sent, ['/api/selected-node/session/antiforgery', '/api/selected-node/local-node/packs/export'])
  } finally { release(); await Promise.allSettled([binary, text]) }
  assert.deepEqual((await binary).bytes, new Uint8Array([0, 255, 128]))
  assert.equal((await text).body, '{}')
  assert.deepEqual(sent, ['/api/selected-node/session/antiforgery', '/api/selected-node/local-node/packs/export',
    '/api/selected-node/session/antiforgery', '/api/selected-node/session/actions/apply'])
})

for (const stage of ['issuance', 'export']) test(`binary ${stage} refusal keeps its exact body and never retries or falls back`, async () => {
  const sent = []
  const refusal = ' {"code":"authorization.permission_required","auditId":"deny-1"}\n'
  const transport = createSelectedSessionTransport(async path => {
    sent.push(path)
    if (stage === 'export' && path.endsWith('/antiforgery'))
      return new Response(null, { headers: { 'X-Harborline-Antiforgery': 'token' } })
    return new Response(refusal, { status: 403, headers: { 'X-Harborline-Audit-Id': 'deny-1' } })
  })
  assert.equal(typeof transport.sendBytes, 'function')
  assert.deepEqual(await transport.sendBytes('/api/local-node/packs/export', 'POST', '{}'), {
    status: 403, body: refusal, auditId: 'deny-1', correlationId: null, bytes: null,
  })
  assert.deepEqual(sent, stage === 'issuance' ? ['/api/selected-node/session/antiforgery']
    : ['/api/selected-node/session/antiforgery', '/api/selected-node/local-node/packs/export'])
})

test('binary transport uses the same path and credential refusal before any request', async () => {
  const transport = createSelectedSessionTransport(() => assert.fail('Invalid binary dispatch must be inert.'))
  assert.equal(typeof transport.sendBytes, 'function')
  await assert.rejects(transport.sendBytes('/api/local-node/../session'), /request_invalid/)
  await assert.rejects(transport.sendBytes('/api/local-node/packs/export', 'POST', '{}', 'application/json',
    { Authorization: 'bootstrap' }), /headers_invalid/)
})
