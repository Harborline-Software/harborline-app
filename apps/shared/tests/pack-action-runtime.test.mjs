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

for (const invalid of ['POST', 'PUT', 'PATCH', 'DELETE', 'missing-rows', 'missing-identity', 'invalid-rows', 'invalid-identity'])
  test(`automatic data source refuses ${invalid} before any dispatch and leaves controls inert`, async () => {
    const definition = structuredClone(entry)
    const descriptor = definition.renderPlan.bindings.dataSource.descriptor
    if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(invalid)) {
      descriptor.method = invalid
      descriptor.requiresAntiforgery = true
    } else if (invalid === 'missing-rows') delete descriptor.rowsPointer
    else if (invalid === 'missing-identity') delete descriptor.rowIdentityPointer
    else if (invalid === 'invalid-rows') descriptor.rowsPointer = 'items'
    else descriptor.rowIdentityPointer = '/nested/identity'
    const calls = []
    const runtime = createPackActionRuntime({ async send(path, method) {
      calls.push({ path, method })
      return reply(path.includes('/catalogue/') ? definition : { items: [{ identity: 'row-1' }] })
    } })
    const result = await runtime.load('example')
    assert.equal(result.error, 'pack_data_source_unsupported')
    assert.deepEqual(result.rows, [])
    assert.deepEqual(result.actions, [])
    await runtime.begin('opaque'); await runtime.invoke()
    assert.deepEqual(calls, [{ path: '/api/local-node/catalogue/definitions/ViewDefinition/example', method: undefined }])
  })

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

test('explicit retry preserves invocation and changed input requires an explicit new request', async () => {
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
  assert.equal((await runtime.invoke({ note: 'changed' })).error, 'pack_request_changed_start_new')
  assert.equal(sent.length, 2)
  assert.equal(runtime.snapshot().requestLocked, true)
  runtime.newRequest()
  await runtime.invoke({ note: 'changed' })
  assert.notEqual(sent[1].headers['X-Correlation-ID'], sent[2].headers['X-Correlation-ID'])
})

test('only bound request details are editable; malformed, duplicate, and authority values cannot dispatch', async () => {
  let writes = 0
  const runtime = createPackActionRuntime({ async send(path, method, _body, _type, headers) {
    if (path.includes('/catalogue/')) return reply(entry)
    if (path.endsWith('/list')) return reply({ items: [{ identity: 'row-1' }] })
    writes++; return { ...reply({ status: 'applied' }), correlationId: headers['X-Correlation-ID'] }
  } }, () => id)
  await runtime.load('example'); runtime.select('row-1'); await runtime.begin('opaque')
  assert.deepEqual(runtime.snapshot().requestDetails, { correlationId: id })
  for (const entries of [[['actor', 'administrator']], [['correlationId', id], ['correlationId', id]], []]) {
    assert.equal(runtime.setRequestDetails(entries).error, 'pack_request_details_invalid')
    assert.deepEqual(runtime.snapshot().requestDetails, { correlationId: id })
    assert.equal((await runtime.invoke()).error, 'pack_request_details_invalid')
  }
  for (const value of ['invalid', '00000000-0000-0000-0000-000000000000', `${id}\r\nAuthorization: bearer`]) {
    runtime.setRequestDetails([['correlationId', value]])
    assert.equal((await runtime.invoke()).error, 'pack_request_details_invalid')
  }
  assert.equal(writes, 0)
  const fixtureCorrelation = '43300000-0000-4000-8000-000000000101'
  runtime.setRequestDetails([['correlationId', fixtureCorrelation]])
  const completed = await runtime.invoke()
  assert.equal(completed.receipt.correlationId, fixtureCorrelation)
  assert.equal(completed.requestDetails.correlationId, fixtureCorrelation)
  assert.equal(completed.requestLocked, true)
  assert.equal(runtime.setRequestDetails([['correlationId', id]]).requestDetails.correlationId, fixtureCorrelation)
  await runtime.invoke()
  assert.equal(writes, 2)
})

test('view refresh consumes the new active plan and signals navigation reload while preserving the native receipt', async () => {
  let active = '1.0.0', reads = 0
  const runtime = createPackActionRuntime({ async send(path) {
    if (path.includes('/catalogue/')) {
      reads++
      const definition = structuredClone(entry)
      definition.renderPlan.definitionVersion = active
      definition.renderPlan.bindings.actions[0].result.refresh = 'view'
      return reply(definition)
    }
    if (path.endsWith('/list')) return reply({ items: [{ identity: 'row-1' }] })
    active = '1.0.1'; return reply({ status: 'replaced' })
  } }, () => id)
  await runtime.load('example'); runtime.select('row-1'); await runtime.begin('opaque')
  const state = await runtime.invoke({})
  assert.equal(reads, 2)
  assert.equal(state.plan.definitionVersion, '1.0.1')
  assert.equal(state.navigationRevision, 1)
  assert.equal(state.receipt.body.status, 'replaced')
  assert.equal(state.receipt.auditId, 'native-audit')
  assert.equal(state.activeAction, null)
})

test('predeclared request ID and idempotency key bind through admitted inputs and replay unchanged', async () => {
  const definition = structuredClone(entry), sent = []
  const declared = definition.renderPlan.bindings.actions[0]
  declared.dispatch.descriptor.inputs.push(
    { name: 'successor', kind: 'Text', placement: 'BodyField', wireName: 'successorId' },
    { name: 'key', kind: 'Text', placement: 'Header', wireName: 'Idempotency-Key' })
  declared.dispatch.bindings.successor = { source: 'invocation', pointer: '/id' }
  declared.dispatch.bindings.key = { source: 'invocation', pointer: '/idempotencyKey' }
  const runtime = createPackActionRuntime({ async send(path, _method, body, _type, headers) {
    if (path.includes('/catalogue/')) return reply(definition)
    if (path.endsWith('/list')) return reply({ items: [{ identity: 'row-1' }] })
    sent.push({ body, headers }); return reply({ status: 'applied', successorId: JSON.parse(body).successorId })
  } }, () => id)
  await runtime.load('example'); runtime.select('row-1'); await runtime.begin('opaque')
  const details = { id: '43300000-0000-4000-8000-000000000002', idempotencyKey: 'm6-t433-grant-submit-v1', correlationId: id }
  runtime.setRequestDetails(Object.entries({ ...details, idempotencyKey: 'bad key' }))
  assert.equal((await runtime.invoke()).error, 'pack_request_details_invalid')
  runtime.setRequestDetails(Object.entries(details))
  const result = await runtime.invoke()
  await runtime.invoke()
  assert.deepEqual(sent[0], sent[1])
  assert.equal(sent[0].headers['Idempotency-Key'], details.idempotencyKey)
  assert.equal(result.receipt.body.successorId, details.id)
})
