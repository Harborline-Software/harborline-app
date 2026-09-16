export const validCorrelationId = value => typeof value === 'string'
  && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
  && value !== '00000000-0000-0000-0000-000000000000'
export const validRequestHeader = (name, value) => typeof value === 'string'
  && (name === 'Idempotency-Key' ? /^[A-Za-z0-9._:-]{1,128}$/.test(value)
    : name === 'X-Correlation-ID' && validCorrelationId(value))

/** Both lanes execute this in the browser; cookie handles never cross a Blazor circuit. */
export function createSelectedSessionTransport(fetchRequest = globalThis.fetch.bind(globalThis)) {
  let pending = Promise.resolve()
  async function execute(path, method, body, contentType, extraHeaders, readResponse, signal, caller) {
    const pathname = path.split('?')[0]
    if (!/^\/api\/(local-node|session)\//.test(pathname) || /[\\#\x00-\x20]|%2e|%2f|%5c/i.test(pathname)
      || pathname.includes('//') || pathname.split('/').some(part => part === '.' || part === '..')
      || !['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].includes(method))
      throw new Error('selected_session_request_invalid')
    if (!extraHeaders || typeof extraHeaders !== 'object' || Array.isArray(extraHeaders)
      || Object.entries(extraHeaders).some(([name, value]) => !validRequestHeader(name, value)))
      throw new Error('selected_session_headers_invalid')
    await canDispatch(signal, caller)
    const headers = { Accept: 'application/json', ...extraHeaders }
    const options = { method, headers, credentials: 'same-origin', redirect: 'error', cache: 'no-store' }
    if (signal) options.signal = signal
    if (method !== 'GET') {
      const issuance = await fetchRequest('/api/selected-node/session/antiforgery', {
        method: 'GET', credentials: 'same-origin', redirect: 'error', cache: 'no-store', headers: { Accept: 'application/json' },
        ...(signal ? { signal } : {}),
      })
      if (!issuance.ok) return readResponse(issuance)
      const token = issuance.headers.get('X-Harborline-Antiforgery')
      if (!token) throw new Error('selected_session_antiforgery_missing')
      headers['X-Harborline-Antiforgery'] = token
      headers['Content-Type'] = contentType
      options.body = body
      // CSRF issuance may have waited on the network. Recheck the still-live caller immediately
      // before dispatch; cancellation never retries or reverses an already-dispatched mutation.
      await canDispatch(signal, caller)
    }
    return readResponse(await fetchRequest(`/api/selected-node/${path.slice('/api/'.length)}`, options))
  }
  function enqueue(readResponse, caller, path, method = 'GET', body = null, contentType = 'application/json', headers = {}, options = {}) {
    // Text and binary mutations share one queue so each single-use token stays paired with its request.
    // A refusal never retries a write.
    const result = pending.then(() => execute(path, method, body, contentType, headers, readResponse, options.signal, caller))
    pending = result.then(() => undefined, () => undefined)
    return result
  }
  return {
    send(...args) { return enqueue(envelope, null, ...args) },
    sendBytes(...args) { return enqueue(binaryEnvelope, null, ...args) },
    sendForCaller(caller, ...args) { return enqueue(envelope, caller, ...args) },
    sendBytesForCaller(caller, ...args) { return enqueue(binaryEnvelope, caller, ...args) },
  }
}

async function canDispatch(signal, caller) {
  if (signal?.aborted) throw signal.reason ?? new DOMException('Selected-session request cancelled.', 'AbortError')
  if (caller && await caller.invokeMethodAsync('CanDispatch') !== true)
    throw new DOMException('Selected-session caller is no longer active.', 'AbortError')
  if (signal?.aborted) throw signal.reason ?? new DOMException('Selected-session request cancelled.', 'AbortError')
}

async function envelope(response) {
  return { status: response.status, body: await response.text(), auditId: response.headers.get('X-Harborline-Audit-Id'),
    correlationId: response.headers.get('X-Harborline-Audit-Correlation') }
}

async function binaryEnvelope(response) {
  if (!response.ok) return { ...await envelope(response), bytes: null }
  return { status: response.status, body: '', bytes: new Uint8Array(await response.arrayBuffer()),
    auditId: response.headers.get('X-Harborline-Audit-Id'),
    correlationId: response.headers.get('X-Harborline-Audit-Correlation') }
}

const browserTransport = createSelectedSessionTransport((...args) => globalThis.fetch(...args))
export const send = (...arguments_) => browserTransport.send(...arguments_)
export const sendBytes = (...arguments_) => browserTransport.sendBytes(...arguments_)
export const sendForCaller = (...arguments_) => browserTransport.sendForCaller(...arguments_)
export const sendBytesForCaller = (...arguments_) => browserTransport.sendBytesForCaller(...arguments_)
