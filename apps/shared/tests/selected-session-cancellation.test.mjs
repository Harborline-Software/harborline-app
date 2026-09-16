import assert from 'node:assert/strict'
import { test } from 'node:test'
import { createSelectedSessionTransport } from '../selected-session-transport.mjs'

const deferred = () => Promise.withResolvers()
const token = () => new Response(null, { status: 204, headers: { 'X-Harborline-Antiforgery': 'once' } })

for (const operation of ['send', 'sendBytes']) {
  test(`${operation}: cancelling a queued install prevents CSRF issuance and mutation without blocking the next request`, async () => {
    const started = deferred(), release = deferred(), paths = []
    const transport = createSelectedSessionTransport(async path => {
      paths.push(path)
      if (paths.length === 1) { started.resolve(); await release.promise }
      return path.endsWith('/antiforgery') ? token() : new Response('{}')
    })
    const first = transport.send('/api/local-node/catalogue/definitions')
    await started.promise
    const controller = new AbortController()
    const queued = transport[operation]('/api/local-node/packs/install', 'POST', new Uint8Array([0, 255]), 'application/octet-stream', {}, { signal: controller.signal })
    controller.abort()
    const refused = assert.rejects(queued, { name: 'AbortError' })
    release.resolve()
    await Promise.all([first, refused])
    await transport.send('/api/local-node/packs/activate', 'POST', '{}')
    assert.deepEqual(paths, ['/api/selected-node/local-node/catalogue/definitions', '/api/selected-node/session/antiforgery', '/api/selected-node/local-node/packs/activate'])
  })
}

for (const operation of ['sendForCaller', 'sendBytesForCaller']) {
  for (const disposed of [false, true]) {
    test(`${operation}: a cancelled or disposed Blazor caller cannot dispatch from the queue (${disposed})`, async () => {
      const started = deferred(), release = deferred(), paths = []
      const transport = createSelectedSessionTransport(async path => {
        paths.push(path); started.resolve(); await release.promise
        return new Response('{}')
      })
      const first = transport.send('/api/local-node/catalogue/definitions')
      await started.promise
      let cancelled = false
      const caller = { async invokeMethodAsync(method) {
        assert.equal(method, 'CanDispatch')
        if (disposed && cancelled) throw new Error('DotNetObjectReference disposed')
        return !cancelled
      } }
      const queued = transport[operation](caller, '/api/local-node/packs/install', 'POST', new Uint8Array([255]), 'application/octet-stream')
      cancelled = true
      const refused = assert.rejects(queued, disposed ? /disposed/ : { name: 'AbortError' })
      release.resolve()
      await Promise.all([first, refused])
      assert.deepEqual(paths, ['/api/selected-node/local-node/catalogue/definitions'])
    })
  }
}

test('Blazor cancellation during CSRF issuance is checked again before the mutation', async () => {
  const started = deferred(), release = deferred(), paths = []
  const transport = createSelectedSessionTransport(async path => {
    paths.push(path); started.resolve(); await release.promise
    return token()
  })
  let active = true
  const caller = { async invokeMethodAsync() { return active } }
  const request = transport.sendForCaller(caller, '/api/local-node/packs/install', 'POST', new Uint8Array([255]), 'application/octet-stream')
  await started.promise
  active = false
  const refused = assert.rejects(request, { name: 'AbortError' })
  release.resolve()
  await refused
  assert.deepEqual(paths, ['/api/selected-node/session/antiforgery'])
})

test('AbortSignal during CSRF issuance prevents mutation even if the network ignores abort', async () => {
  const started = deferred(), release = deferred(), paths = []
  const controller = new AbortController()
  const transport = createSelectedSessionTransport(async (path, options) => {
    paths.push(path)
    started.resolve()
    assert.equal(options.signal, controller.signal)
    await release.promise
    return token()
  })
  const request = transport.send('/api/local-node/packs/install', 'POST', '{}', 'application/json', {}, { signal: controller.signal })
  await started.promise
  controller.abort()
  const refused = assert.rejects(request, { name: 'AbortError' })
  release.resolve()
  await refused
  assert.deepEqual(paths, ['/api/selected-node/session/antiforgery'])
})
