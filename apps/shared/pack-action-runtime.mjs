import { dispatchPackRequest, preparePackRequest } from './pack-request.mjs'
import { send, validCorrelationId, validRequestHeader } from './selected-session-transport.mjs'

const object = value => value !== null && typeof value === 'object' && !Array.isArray(value)
const supported = action => action?.dispatch?.schemaVersion === 1 && action.dispatch.kind === 'request'
const identifier = value => typeof value === 'string' && /^[A-Za-z0-9][A-Za-z0-9._-]*$/.test(value)
const pointer = (value, path) => typeof path === 'string' && /^\/[A-Za-z][A-Za-z0-9_-]*$/.test(path)
  && object(value) && Object.hasOwn(value, path.slice(1)) ? value[path.slice(1)] : undefined

function normalizeForm(plan) {
  const fields = plan.bindings?.fields ?? {}
  const overlay = plan.bindings?.overlay ?? {}
  const labels = Object.fromEntries(Object.keys(fields).map(name => [name,
    { label: name, ...overlay.fields?.[name] }]))
  return { ...plan, bindings: { fields, overlay: { ...overlay, fields: labels,
    sections: overlay.sections?.length ? overlay.sections : [{ id: 'details', title: 'Details', fields: Object.keys(fields) }] } } }
}

export function normalizePackReceipt(response) {
  if ('code' in response) return { status: 0, code: response.code, body: null, text: response.code, auditId: null, correlationId: null }
  let body
  try { body = JSON.parse(response.body) } catch { body = response.body }
  return { status: response.status, code: typeof body?.code === 'string' ? body.code : null,
    body, text: response.body, auditId: response.auditId ?? body?.auditId ?? null,
    correlationId: response.correlationId ?? body?.correlationId ?? null }
}

/** One controller per mounted view, shared by React and the browser-owned Blazor bridge. */
export function createPackActionRuntime(transport = { send }, uuid = () => crypto.randomUUID()) {
  let state = { plan: null, rows: [], actions: [], selectedId: null, activeAction: null, inputPlan: null,
    receipt: null, error: null, busy: false, navigationRevision: 0, requestDetails: {}, requestLocked: false }
  let fingerprint, generation = 0, requestEditRefused = false
  const snapshot = () => structuredClone(state)
  function resetRequest() {
    const pointers = Object.values(state.activeAction?.dispatch?.bindings ?? {})
      .filter(binding => binding.source === 'invocation').map(binding => binding.pointer)
    state.requestDetails = Object.fromEntries(['id', 'idempotencyKey', 'correlationId']
      .filter(name => pointers.includes(`/${name}`)).map(name => [name, uuid()]))
    state.requestLocked = false; fingerprint = undefined; requestEditRefused = false
  }
  async function readEntry(kind, id, version) {
    if (!identifier(id) || (version !== undefined && !identifier(version))) throw Error('pack_definition_invalid')
    const response = await transport.send(`/api/local-node/catalogue/definitions/${kind}/${encodeURIComponent(id)}${version ? `?version=${encodeURIComponent(version)}` : ''}`)
    if (response.status < 200 || response.status >= 300) { state.receipt = normalizePackReceipt(response); throw Error('pack_definition_refused') }
    return JSON.parse(response.body)
  }
  async function refresh() {
    const source = state.plan?.bindings?.dataSource
    if (!source) { state.rows = []; state.selectedId = null; return }
    const response = await dispatchPackRequest({ dispatch: { schemaVersion: 1, kind: 'request', ...source } }, {}, transport)
    const receipt = normalizePackReceipt(response)
    if (receipt.status < 200 || receipt.status >= 300) {
      state.rows = []; state.selectedId = null; state.receipt = receipt; throw Error(receipt.code ?? 'pack_rows_refused')
    }
    const rows = pointer(receipt.body, source.descriptor.rowsPointer)
    const ids = new Set()
    if (!Array.isArray(rows)) throw Error('pack_rows_invalid')
    state.rows = rows.map(values => {
      const id = pointer(values, source.descriptor.rowIdentityPointer)
      if (typeof id !== 'string' || !id || ids.has(id)) throw Error('pack_rows_invalid')
      ids.add(id)
      return { id, values }
    })
    if (!ids.has(state.selectedId)) state.selectedId = null
  }
  return {
    snapshot,
    async load(viewId) {
      const request = ++generation
      state = { plan: null, rows: [], actions: [], selectedId: null, activeAction: null, inputPlan: null,
        receipt: null, error: null, busy: true, navigationRevision: state.navigationRevision, requestDetails: {}, requestLocked: false }
      fingerprint = undefined
      try {
        const entry = await readEntry('ViewDefinition', viewId)
        if (request !== generation) return snapshot()
        if (entry.renderPlan?.definitionKind !== 'ViewDefinition'
          || entry.renderPlan.bindings?.viewKind !== 'views.entity-list/grid') throw Error('pack_view_unsupported')
        state.plan = entry.renderPlan
        state.actions = (entry.renderPlan.bindings.actions ?? []).filter(supported)
        state.plan.bindings.actions = state.actions
        await refresh()
      } catch (error) { if (request === generation) { state.rows = []; state.error = error.message } }
      finally { if (request === generation) state.busy = false }
      return snapshot()
    },
    select(id) {
      if (!state.busy) state.selectedId = state.rows.some(row => row.id === id) ? id : null
      return snapshot()
    },
    async begin(id) {
      if (state.busy) return snapshot()
      state.activeAction = state.actions.find(action => action.id === id) ?? null
      state.inputPlan = null; state.error = null; state.receipt = null; resetRequest()
      if (!state.activeAction) { state.error = 'pack_action_unsupported'; return snapshot() }
      state.busy = true
      try {
        const action = state.activeAction
        if (action.inputForm) {
          const entry = await readEntry('FormDefinition', action.inputForm.formId, action.inputForm.version)
          if (entry.renderPlan?.definitionKind !== 'FormDefinition') throw Error('pack_input_unsupported')
          state.inputPlan = normalizeForm(entry.renderPlan)
        } else if (action.input) state.inputPlan = normalizeForm({ definitionId: action.id, definitionVersion: '1',
          definitionKind: 'FormDefinition', bindings: action.input })
      } catch (error) { state.error = error.message; state.activeAction = null }
      finally { state.busy = false }
      return snapshot()
    },
    setRequestDetails(entries) {
      if (state.busy || state.requestLocked) return snapshot()
      const expected = Object.keys(state.requestDetails), names = new Set()
      if (!Array.isArray(entries) || entries.length !== expected.length || entries.some(entry => {
        if (!Array.isArray(entry) || entry.length !== 2 || !expected.includes(entry[0])
          || names.has(entry[0]) || typeof entry[1] !== 'string') return true
        names.add(entry[0]); return false
      })) { requestEditRefused = true; state.error = 'pack_request_details_invalid'; return snapshot() }
      state.requestDetails = Object.fromEntries(entries); state.error = null; requestEditRefused = false
      return snapshot()
    },
    newRequest() {
      if (!state.busy && state.activeAction) { resetRequest(); state.error = null; state.receipt = null }
      return snapshot()
    },
    async invoke(values = {}, file) {
      if (state.busy || !state.activeAction) return snapshot()
      state.busy = true; state.error = null
      try {
        if (requestEditRefused || Object.entries(state.requestDetails).some(([name, value]) => name === 'idempotencyKey'
          ? !validRequestHeader('Idempotency-Key', value) : !validCorrelationId(value))) throw Error('pack_request_details_invalid')
        const selection = state.rows.find(row => row.id === state.selectedId)?.values
        const bytes = file instanceof Blob ? new Uint8Array(await file.arrayBuffer()) : file
        const digest = bytes ? Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))).join(',') : null
        const next = JSON.stringify({ values, selection, digest })
        if (fingerprint !== undefined && fingerprint !== next) throw Error('pack_request_changed_start_new')
        const sources = { input: values, selection, file: bytes, invocation: state.requestDetails }
        if (!preparePackRequest(state.activeAction, sources)) throw Error('pack_action_unsupported')
        fingerprint = next; state.requestLocked = true
        state.receipt = normalizePackReceipt(await dispatchPackRequest(state.activeAction, sources, transport))
        if (state.receipt.status >= 200 && state.receipt.status < 300 && state.activeAction.result?.refresh === 'data-source') {
          const receipt = state.receipt
          try { await refresh() } finally { state.receipt = receipt }
        } else if (state.receipt.status >= 200 && state.receipt.status < 300 && state.activeAction.result?.refresh === 'view') {
          const receipt = state.receipt, revision = state.navigationRevision
          await this.load(state.plan.definitionId)
          state.receipt = receipt; state.navigationRevision = revision + 1
        }
      } catch (error) { state.error = error.message }
      finally { state.busy = false }
      return snapshot()
    },
    async invokeWithFile(values, element) { return this.invoke(values, element?.files?.[0]) },
  }
}
