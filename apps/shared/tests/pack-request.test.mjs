import assert from 'node:assert/strict'
import { test } from 'node:test'
import { preparePackRequest, dispatchPackRequest } from '../pack-request.mjs'

const action = {
  id: 'opaque-action', operation: 'opaque-operation',
  dispatch: { schemaVersion: 1, kind: 'request',
    descriptor: { id: 'example.submit.v1', method: 'POST', routeTemplate: '/api/local-node/forms/{formId}/submit',
      contentType: 'application/json', audience: 'selected-session', requiresAntiforgery: true,
      authorizationCapability: 'forms:write', inputs: [
        { name: 'form', kind: 'Text', placement: 'Path', wireName: 'formId' },
        { name: 'values', kind: 'Object', placement: 'BodyRoot', wireName: '' },
        { name: 'requestId', kind: 'Text', placement: 'Header', wireName: 'Idempotency-Key' },
      ] },
    bindings: { form: { literal: 'example.form' }, values: { source: 'input', pointer: '' },
      requestId: { literal: 'example-submit-v1' } },
  },
}

test('host-owned placement serializes a raw form payload and only the registered idempotency header', () => {
  assert.deepEqual(preparePackRequest(action, { input: { title: 'Example', count: 4 } }), {
    path: '/api/local-node/forms/example.form/submit', method: 'POST', contentType: 'application/json',
    body: '{"title":"Example","count":4}', headers: { 'Idempotency-Key': 'example-submit-v1' },
  })
})

test('selected row identity binds by declared pointer and never array position', () => {
  const request = structuredClone(action)
  request.dispatch.descriptor.routeTemplate = '/api/session/commands/apply'
  request.dispatch.descriptor.inputs = [{ name: 'target', kind: 'Text', placement: 'BodyField', wireName: 'targetId' }]
  request.dispatch.bindings = { target: { source: 'selection', pointer: '/identity' } }
  assert.equal(preparePackRequest(request, { selection: { identity: 'row-42' } }).body, '{"targetId":"row-42"}')
  assert.equal(preparePackRequest(request, { selection: { position: 42 } }), null)
})

test('unknown kinds, missing or duplicate placement, credentials, and path escapes remain inert', async () => {
  const mutations = [
    request => { request.dispatch.kind = 'script' },
    request => { delete request.dispatch.descriptor.inputs[0].placement },
    request => { request.dispatch.descriptor.inputs[0].placement = 'QueryMagic' },
    request => { request.dispatch.descriptor.inputs.push(request.dispatch.descriptor.inputs[0]) },
    request => { request.dispatch.descriptor.inputs[2].wireName = 'Authorization' },
    request => { request.dispatch.descriptor.inputs[2].wireName = 'Cookie' },
    request => { request.dispatch.descriptor.inputs[2].wireName = 'X-Harborline-Antiforgery' },
    request => { request.dispatch.descriptor.inputs[2].wireName = 'X-Forwarded-Host' },
    request => { request.dispatch.bindings.form.literal = '../outside' },
    request => { request.dispatch.descriptor.routeTemplate = 'https://example.invalid/collect' },
    request => { delete request.dispatch.bindings.requestId },
  ]
  for (const mutate of mutations) {
    const request = structuredClone(action)
    mutate(request)
    let calls = 0
    const result = await dispatchPackRequest(request, { input: {} }, { send() { calls++; throw Error('must remain inert') } })
    assert.equal(result.code, 'pack_action_unsupported')
    assert.equal(calls, 0)
  }
})

test('server denial status, body and audit receipt pass through without a retry or client authorization decision', async () => {
  const response = { status: 403, body: '{"code":"authorization.permission_required","auditId":"server-id"}', auditId: 'server-id' }
  let calls = 0
  const result = await dispatchPackRequest(action, { input: {} }, { async send(...args) {
    calls++
    assert.deepEqual(args, ['/api/local-node/forms/example.form/submit', 'POST', '{}', 'application/json', { 'Idempotency-Key': 'example-submit-v1' }])
    return response
  } })
  assert.strictEqual(result, response)
  assert.equal(calls, 1)
})

test('binary whole-body input preserves bytes and cannot masquerade as a JSON form', () => {
  const request = structuredClone(action)
  request.dispatch.descriptor.routeTemplate = '/api/session/packages/replace'
  request.dispatch.descriptor.contentType = 'application/octet-stream'
  request.dispatch.descriptor.inputs = [{ name: 'artifact', kind: 'Binary', placement: 'BodyRoot', wireName: '' }]
  request.dispatch.bindings = { artifact: { source: 'file', pointer: '' } }
  const file = new Uint8Array([1, 0, 255, 42])
  assert.strictEqual(preparePackRequest(request, { file }).body, file)
  request.dispatch.descriptor.contentType = 'application/json'
  assert.equal(preparePackRequest(request, { file }), null)
})
