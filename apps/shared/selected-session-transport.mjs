export const validCorrelationId = value => typeof value === 'string'
  && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
  && value !== '00000000-0000-0000-0000-000000000000'
export const validRequestHeader = (name, value) => typeof value === 'string'
  && (name === 'Idempotency-Key' ? /^[A-Za-z0-9._:-]{1,128}$/.test(value)
    : name === 'X-Correlation-ID' && validCorrelationId(value))

/** Both lanes execute this in the browser; cookie handles never cross a Blazor circuit. */
export function createSelectedSessionTransport(fetchRequest = globalThis.fetch.bind(globalThis)) {
  let pending = Promise.resolve()
  async function execute(path, method, body, contentType, extraHeaders) {
    const pathname = path.split('?')[0]
    if (!/^\/api\/(local-node|session)\//.test(pathname) || /[\\#\x00-\x20]|%2e|%2f|%5c/i.test(pathname)
      || pathname.includes('//') || pathname.split('/').some(part => part === '.' || part === '..')
      || !['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].includes(method))
      throw new Error('selected_session_request_invalid')
    if (!extraHeaders || typeof extraHeaders !== 'object' || Array.isArray(extraHeaders)
      || Object.entries(extraHeaders).some(([name, value]) => !validRequestHeader(name, value)))
      throw new Error('selected_session_headers_invalid')
    const headers = { Accept: 'application/json', ...extraHeaders }
    const options = { method, headers, credentials: 'same-origin', redirect: 'error', cache: 'no-store' }
    if (method !== 'GET') {
      const issuance = await fetchRequest('/api/selected-node/session/antiforgery', {
        method: 'GET', credentials: 'same-origin', redirect: 'error', cache: 'no-store', headers: { Accept: 'application/json' },
      })
      if (!issuance.ok) return envelope(issuance)
      const token = issuance.headers.get('X-Harborline-Antiforgery')
      if (!token) throw new Error('selected_session_antiforgery_missing')
      headers['X-Harborline-Antiforgery'] = token
      headers['Content-Type'] = contentType
      options.body = body
    }
    return envelope(await fetchRequest(`/api/selected-node/${path.slice('/api/'.length)}`, options))
  }
  return {
    send(path, method = 'GET', body = null, contentType = 'application/json', headers = {}) {
      // Each single-use token stays paired with its mutation. A refusal never retries a write.
      const result = pending.then(() => execute(path, method, body, contentType, headers))
      pending = result.then(() => undefined, () => undefined)
      return result
    },
  }
}

async function envelope(response) {
  return { status: response.status, body: await response.text(), auditId: response.headers.get('X-Harborline-Audit-Id'),
    correlationId: response.headers.get('X-Harborline-Audit-Correlation') }
}

const browserTransport = createSelectedSessionTransport()
export const send = (...arguments_) => browserTransport.send(...arguments_)
