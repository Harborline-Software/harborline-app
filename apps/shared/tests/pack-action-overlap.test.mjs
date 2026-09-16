import assert from 'node:assert/strict'
import { test } from 'node:test'
import { createPackActionRuntime } from '../pack-action-runtime.mjs'
import { createSelectedSessionTransport } from '../selected-session-transport.mjs'

const deferred = () => {
  let resolve
  const promise = new Promise(done => { resolve = done })
  return { promise, resolve }
}
const reply = (body, status = 200) => ({ status, body: JSON.stringify(body), auditId: `audit-${status}` })
function definition(id, inputForm = false, refresh = 'data-source') {
  return { renderPlan: { definitionId: id, definitionVersion: '1.0.0', definitionKind: 'ViewDefinition', bindings: {
    viewKind: 'views.entity-list/grid', parameters: {},
    dataSource: { descriptor: { id: 'list', method: 'GET', routeTemplate: `/api/session/list-${id}`,
      contentType: 'application/json', audience: 'selected-session', inputs: [], rowsPointer: '/rows', rowIdentityPointer: '/id' }, bindings: {} },
    actions: [{ id: `apply-${id}`, label: id, result: { refresh },
      ...(inputForm ? { inputForm: { formId: `form-${id}`, version: '1.0.0' } } : {}),
      dispatch: { schemaVersion: 1, kind: 'request', bindings: {}, descriptor: { id: 'apply', method: 'POST',
        routeTemplate: `/api/session/apply-${id}`, contentType: 'application/json', audience: 'selected-session',
        requiresAntiforgery: true, inputs: [] } } }],
  } } }
}

for (const status of [200, 403]) test(`late A rows (${status}) cannot overwrite completed B`, async () => {
  const rows = deferred(), entered = deferred()
  let oldSignal
  const runtime = createPackActionRuntime({ async send(path, _method, _body, _type, _headers, options) {
    if (path.endsWith('/A')) return reply(definition('A'))
    if (path.endsWith('/B')) return reply(definition('B'))
    if (path.endsWith('/list-A')) { oldSignal = options?.signal; entered.resolve(); return rows.promise }
    return reply({ rows: [{ id: 'row-B' }] })
  } })
  const old = runtime.load('A')
  await entered.promise
  const current = await runtime.load('B')
  rows.resolve(reply(status === 200 ? { rows: [{ id: 'row-A' }] } : { code: 'permission_required' }, status))
  await old
  assert.deepEqual(runtime.snapshot(), current)
  assert.equal(oldSignal?.aborted, true)
})

for (const status of [200, 403]) test(`late A input form (${status}) cannot overwrite B actions or busy state`, async () => {
  const form = deferred(), entered = deferred()
  const runtime = createPackActionRuntime({ async send(path) {
    if (path.includes('/FormDefinition/')) { entered.resolve(); return form.promise }
    if (path.endsWith('/A')) return reply(definition('A', true))
    if (path.endsWith('/B')) return reply(definition('B'))
    return reply({ rows: [{ id: path.endsWith('/list-A') ? 'row-A' : 'row-B' }] })
  } })
  await runtime.load('A')
  const old = runtime.begin('apply-A')
  await entered.promise
  await runtime.load('B'); const current = await runtime.begin('apply-B')
  form.resolve(reply(status === 200 ? { renderPlan: { definitionKind: 'FormDefinition', bindings: {} } }
    : { code: 'permission_required' }, status))
  await old
  assert.deepEqual(runtime.snapshot(), current)
})

for (const status of [200, 403]) test(`late A mutation (${status}) neither refreshes B nor retries`, async () => {
  const mutation = deferred(), entered = deferred(), calls = []
  const runtime = createPackActionRuntime({ async send(path, method) {
    calls.push({ path, method })
    if (method === 'POST') { entered.resolve(); return mutation.promise }
    if (path.endsWith('/A')) return reply(definition('A'))
    if (path.endsWith('/B')) return reply(definition('B'))
    return reply({ rows: [{ id: path.endsWith('/list-A') ? 'row-A' : 'row-B' }] })
  } })
  await runtime.load('A'); await runtime.begin('apply-A')
  const old = runtime.invoke()
  await entered.promise
  await runtime.load('B'); const current = await runtime.begin('apply-B')
  const count = calls.length
  mutation.resolve(reply(status === 200 ? { applied: true } : { code: 'permission_required' }, status))
  await old
  assert.deepEqual(runtime.snapshot(), current)
  assert.equal(calls.length, count)
  assert.equal(calls.filter(call => call.method === 'POST').length, 1)
})

test('late refused A definition cannot attach its audit receipt to B', async () => {
  const definitionA = deferred(), entered = deferred()
  const runtime = createPackActionRuntime({ async send(path) {
    if (path.endsWith('/A')) { entered.resolve(); return definitionA.promise }
    if (path.endsWith('/B')) return reply(definition('B'))
    return reply({ rows: [{ id: 'row-B' }] })
  } })
  const old = runtime.load('A'); await entered.promise
  const current = await runtime.load('B')
  definitionA.resolve(reply({ code: 'permission_required' }, 403)); await old
  assert.deepEqual(runtime.snapshot(), current)
})

test('supersession while preparing A file refuses its mutation before dispatch', async () => {
  const bytes = deferred(), entered = deferred(), writes = []
  class DelayedFile extends Blob { arrayBuffer() { entered.resolve(); return bytes.promise } }
  const runtime = createPackActionRuntime({ async send(path, method) {
    if (method === 'POST') writes.push(path)
    if (path.endsWith('/A')) return reply(definition('A'))
    if (path.endsWith('/B')) return reply(definition('B'))
    return reply({ rows: [] })
  } })
  await runtime.load('A'); await runtime.begin('apply-A')
  const old = runtime.invoke({}, new DelayedFile())
  await entered.promise
  const current = await runtime.load('B')
  bytes.resolve(new Uint8Array([0, 255]).buffer); await old
  assert.deepEqual(runtime.snapshot(), current)
  assert.deepEqual(writes, [])
})

test('superseded replacement refresh cannot attach A receipt or navigation revision to B', async () => {
  const replacement = deferred(), entered = deferred()
  let reads = 0
  const runtime = createPackActionRuntime({ async send(path, method) {
    if (method === 'POST') return reply({ replaced: true })
    if (path.endsWith('/A')) {
      if (++reads === 2) { entered.resolve(); return replacement.promise }
      return reply(definition('A', false, 'view'))
    }
    if (path.endsWith('/B')) return reply(definition('B'))
    return reply({ rows: [] })
  } })
  await runtime.load('A'); await runtime.begin('apply-A')
  const old = runtime.invoke(); await entered.promise
  const current = await runtime.load('B')
  replacement.resolve(reply(definition('A', false, 'view'))); await old
  assert.deepEqual(runtime.snapshot(), current)
})

test('superseding a queued action prevents CSRF issuance and mutation through the real transport', async () => {
  const entered = deferred(), release = deferred(), calls = []
  const transport = createSelectedSessionTransport(async (path, options) => {
    calls.push({ path, method: options.method })
    if (path.endsWith('/block')) { entered.resolve(); await release.promise }
    const body = path.endsWith('/A') ? definition('A') : path.endsWith('/B') ? definition('B') : { rows: [] }
    return new Response(JSON.stringify(body))
  })
  const runtime = createPackActionRuntime(transport)
  await runtime.load('A'); await runtime.begin('apply-A')
  const blocker = transport.send('/api/session/block')
  await entered.promise
  const old = runtime.invoke()
  const current = runtime.load('B')
  release.resolve()
  await Promise.all([blocker, old, current])
  assert.equal(runtime.snapshot().plan.definitionId, 'B')
  assert.equal(runtime.snapshot().error, null)
  assert.equal(calls.some(call => call.method !== 'GET' || call.path.endsWith('/antiforgery')), false)
})
