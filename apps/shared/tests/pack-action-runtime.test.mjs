import assert from 'node:assert/strict'
import { test } from 'node:test'
import { createPackActionRuntime } from '../pack-action-runtime.mjs'

const id = '43300000-0000-4000-8000-000000000010'
const descriptor = { id: 'example.apply.v1', method: 'POST', routeTemplate: '/api/session/example/apply',
  contentType: 'application/json', audience: 'selected-session', requiresAntiforgery: true,
  inputs: [{ name: 'id', kind: 'Text', placement: 'BodyField', wireName: 'id' },
    { name: 'correlation', kind: 'Text', placement: 'Header', wireName: 'X-Correlation-ID' }] }
const action = { id: 'opaque', label: 'Apply', operation: 'ignored', result: { refresh: 'data-source' },
  dispatch: { schemaVersion: 1, kind: 'request', descriptor,
    bindings: { id: { source: 'selection', pointer: '/identity' }, correlation: { source: 'invocation', pointer: '/correlationId' } } } }
const source = { descriptor: { id: 'example.list.v1', method: 'GET', routeTemplate: '/api/session/example/list',
  contentType: 'application/json', audience: 'selected-session', requiresAntiforgery: false, inputs: [],
  rowsPointer: '/items', rowIdentityPointer: '/identity' }, bindings: {} }
const entry = { renderPlan: { definitionId: 'example', definitionKind: 'ViewDefinition', definitionVersion: '1.0.0',
  bindings: { viewKind: 'views.entity-list/grid', dataSource: source, actions: [action], parameters: {} } } }
const reply = (body, status = 200) => ({ status, body: JSON.stringify(body), auditId: 'native-audit', correlationId: id })

test('shared runtime binds stable row identity, keeps refusal receipt, and never retries a denial', async () => {
  const sent = []
  const runtime = createPackActionRuntime({ async send(path, method, body, type, headers) {
    sent.push({ path, method, body, type, headers })
    if (path.includes('/catalogue/')) return reply(entry)
    if (path.endsWith('/list')) return reply({ items: [{ identity: 'row-1', title: 'Example' }] })
    return reply({ code: 'authorization.permission_required' }, 403)
  } }, () => id)
  const loaded = await runtime.load('example')
  assert.deepEqual(loaded.rows, [{ id: 'row-1', values: { identity: 'row-1', title: 'Example' } }])
  runtime.select('row-1')
  await runtime.begin('opaque')
  const result = await runtime.invoke({})
  assert.equal(result.receipt.status, 403)
  assert.equal(result.receipt.code, 'authorization.permission_required')
  assert.equal(result.receipt.auditId, 'native-audit')
  assert.equal(result.receipt.correlationId, id)
  assert.equal(sent.filter(row => row.method === 'POST').length, 1)
  assert.equal(sent.at(-1).body, '{"id":"row-1"}')
  assert.equal(sent.at(-1).headers['X-Correlation-ID'], id)
})

test('success refreshes declared rows without dropping the returned receipt', async () => {
  let reads = 0
  const runtime = createPackActionRuntime({ async send(path) {
    if (path.includes('/catalogue/')) return reply(entry)
    if (path.endsWith('/list')) { reads++; return reply({ items: [{ identity: 'row-1' }] }) }
    return reply({ status: 'applied', result: { identifier: 'native-result' } })
  } }, () => id)
  await runtime.load('example'); runtime.select('row-1'); await runtime.begin('opaque')
  const result = await runtime.invoke({})
  assert.equal(reads, 2)
  assert.equal(result.receipt.body.result.identifier, 'native-result')
  assert.equal(result.selectedId, 'row-1')
})

test('unknown action and malformed/duplicate row identities stay inert', async () => {
  let sends = 0
  const runtime = createPackActionRuntime({ async send(path) {
    sends++
    return reply(path.includes('/catalogue/') ? entry : { items: [{ identity: 'same' }, { identity: 'same' }] })
  } }, () => id)
  assert.equal((await runtime.load('example')).error, 'pack_rows_invalid')
  await runtime.begin('unknown'); await runtime.invoke({})
  assert.equal(sends, 2)
})

test('unknown view kinds and action kinds never create an executable control', async () => {
  for (const unknownView of [false, true]) {
    const definition = structuredClone(entry)
    if (unknownView) definition.renderPlan.bindings.viewKind = 'views.unknown'
    else { delete definition.renderPlan.bindings.dataSource; definition.renderPlan.bindings.actions[0].dispatch.kind = 'script' }
    let sends = 0
    const runtime = createPackActionRuntime({ async send() { sends++; return reply(definition) } }, () => id)
    const state = await runtime.load('example')
    assert.equal(state.actions.length, 0)
    await runtime.begin('opaque'); await runtime.invoke({})
    assert.equal(sends, 1)
  }
})

test('explicit retry preserves invocation, but changed input starts a new intention without automatic retry', async () => {
  let sequence = 0
  const sent = []
  const runtime = createPackActionRuntime({ async send(path, method, _body, _type, headers) {
    if (path.includes('/catalogue/')) return reply(entry)
    if (path.endsWith('/list')) return reply({ items: [{ identity: 'row-1' }] })
    sent.push({ method, headers })
    if (sent.length === 1) throw Error('network failure')
    return reply({ code: 'authorization.permission_required' }, 403)
  } }, () => `43300000-0000-4000-8000-${String(++sequence).padStart(12, '0')}`)
  await runtime.load('example'); runtime.select('row-1'); await runtime.begin('opaque')
  await runtime.invoke({ note: 'first' })
  assert.equal(sent.length, 1)
  await runtime.invoke({ note: 'first' })
  assert.deepEqual(sent[0].headers, sent[1].headers)
  await runtime.invoke({ note: 'changed' })
  assert.notEqual(sent[1].headers['X-Correlation-ID'], sent[2].headers['X-Correlation-ID'])
})
