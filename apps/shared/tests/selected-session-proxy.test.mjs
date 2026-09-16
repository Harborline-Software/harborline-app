import assert from 'node:assert/strict'
import { createServer } from 'node:http'
import { after, before, test } from 'node:test'
import { createSelectedSessionProxy } from '../selected-session-proxy.mjs'

let node, app, nodeOrigin, appOrigin
const received = []
before(async () => {
  node = createServer(async (request, response) => {
    received.push({ url: request.url, headers: request.headers })
    const principal = request.headers.authorization ? 'bootstrap-administrator' : request.headers.cookie
    await new Promise(resolve => setTimeout(resolve, principal?.includes('alice') ? 15 : 1))
    response.writeHead(principal?.includes('bob') ? 403 : 200, {
      'Content-Type': 'application/json',
      'X-Harborline-Audit-Id': 'server-audit-42',
      'Authorization': 'must-not-reach-browser',
    })
    response.end(JSON.stringify(request.url.endsWith('/navigation/workspaces')
      ? { configured: true, pack: { packId: principal, seedWorkspaces: [{ id: principal }] } }
      : { principal, auditId: 'server-audit-42' }))
  })
  await new Promise(resolve => node.listen(0, '127.0.0.1', resolve))
  nodeOrigin = `http://127.0.0.1:${node.address().port}`
  const proxy = createSelectedSessionProxy(nodeOrigin)
  app = createServer((request, response) => proxy(request, response, () => {
    response.writeHead(404).end()
  }))
  await new Promise(resolve => app.listen(0, '127.0.0.1', resolve))
  appOrigin = `http://127.0.0.1:${app.address().port}`
})
after(async () => {
  await Promise.all([node, app].filter(Boolean).map(server => new Promise(resolve => server.close(resolve))))
})

function request(cookie, options = {}) {
  return fetch(`${appOrigin}/api/selected-node/local-node/records/entities/example`, {
    ...options,
    headers: { Origin: appOrigin, Cookie: cookie, ...options.headers },
  })
}

test('two concurrent browser cookie jars remain isolated and cannot inherit a bootstrap principal', async () => {
  const responses = await Promise.all(Array.from({ length: 12 }, (_, index) => request(
    `__Host-hl-selected=${index % 2 ? 'bob' : 'alice'}; __Host-web_session=legacy-admin; unrelated=secret`,
    { headers: { Authorization: 'Bearer bootstrap-secret' } },
  )))
  for (const [index, response] of responses.entries()) {
    assert.equal(response.status, index % 2 ? 403 : 200)
    assert.deepEqual(await response.json(), {
      principal: `__Host-hl-selected=${index % 2 ? 'bob' : 'alice'}`, auditId: 'server-audit-42',
    })
    assert.equal(response.headers.get('x-harborline-audit-id'), 'server-audit-42')
    assert.equal(response.headers.get('authorization'), null)
  }
  assert.ok(received.every(row => row.headers.authorization === undefined))
  assert.ok(received.every(row => row.url === '/api/local-node/records/entities/example'))
})

test('missing selected cookie cannot fall back to a bearer or legacy administrator cookie', async () => {
  const count = received.length
  const response = await request('__Host-web_session=legacy-admin', { headers: { Authorization: 'Bearer bootstrap-secret' } })
  assert.equal(response.status, 401)
  assert.equal(received.length, count)
})

test('cross-origin requests and mutations without antiforgery never reach the node', async () => {
  const count = received.length
  for (const options of [
    { headers: { Origin: 'https://hostile.example' } },
    { method: 'POST' },
    { method: 'POST', headers: { Origin: 'null', 'X-Harborline-Antiforgery': 'token' } },
  ]) assert.equal((await request('__Host-hl-selected=alice', options)).status, 403)
  assert.equal(received.length, count)
})

test('mutation forwards only the selected cookie and the browser antiforgery token', async () => {
  const response = await request('__Host-hl-selected=alice; __Host-hl-install=installation-admin', {
    method: 'POST', body: '{}', headers: {
      'Content-Type': 'application/json', 'X-Harborline-Antiforgery': 'one-time-token',
      'Idempotency-Key': 'example-submit-v1',
      'X-Correlation-ID': '43300000-0000-4000-8000-000000000010',
      'X-Untrusted-Actor': 'administrator',
    },
  })
  assert.equal(response.status, 200)
  const last = received.at(-1)
  assert.equal(last.headers['x-harborline-antiforgery'], 'one-time-token')
  assert.equal(last.headers['idempotency-key'], 'example-submit-v1')
  assert.equal(last.headers['x-correlation-id'], '43300000-0000-4000-8000-000000000010')
  assert.equal(last.headers['x-untrusted-actor'], undefined)
  assert.equal(last.headers.cookie, '__Host-hl-selected=alice')
})

test('raw path escapes cannot select another upstream route or target', async () => {
  const proxy = createSelectedSessionProxy(nodeOrigin)
  for (const path of [
    '/api/selected-node/../elsewhere', '/api/selected-node/%2e%2e/elsewhere',
    '/api/selected-node/local-node%2fsecret', '/api/selected-node//hostile.example/path',
    '/api/selected-node/local-node\\secret', '/api/selected-node/https://hostile.example',
  ]) {
    let status
    await proxy({ url: path, method: 'GET', headers: { host: 'app.example', origin: 'http://app.example', cookie: '__Host-hl-selected=alice' }, socket: {} },
      { writeHead(code) { status = code; return this }, end() {} }, () => assert.fail('escaped proxy namespace'))
    assert.equal(status, 400, path)
  }
})

test('joined duplicate idempotency headers refuse before upstream', async () => {
  const count = received.length
  const response = await request('__Host-hl-selected=alice', {
    headers: { 'Idempotency-Key': 'first, second' },
  })
  assert.equal(response.status, 400)
  assert.equal(received.length, count)
})

test('malformed and duplicate correlation headers refuse before upstream', async () => {
  const count = received.length
  for (const value of ['bad', '00000000-0000-0000-0000-000000000000',
    '43300000-0000-4000-8000-000000000010, 43300000-0000-4000-8000-000000000010']) {
    const response = await request('__Host-hl-selected=alice', { headers: { 'X-Correlation-ID': value } })
    assert.equal(response.status, 400)
  }
  assert.equal(received.length, count)
})

test('navigation is tenant-isolated across concurrent selected sessions despite injected bootstrap bearer', async () => {
  const results = await Promise.all(['tenant-a', 'tenant-b'].map(async tenant => {
    const response = await fetch(`${appOrigin}/api/selected-node/local-node/navigation/workspaces`, { headers: {
      Origin: appOrigin, Cookie: `__Host-hl-selected=${tenant}`, Authorization: 'Bearer first-administrator',
    } })
    return response.json()
  }))
  assert.deepEqual(results.map(result => result.pack.packId), ['__Host-hl-selected=tenant-a', '__Host-hl-selected=tenant-b'])
  assert.ok(received.slice(-2).every(row => row.headers.authorization === undefined))
})
