/** Both lanes execute this in the browser; cookie handles never cross a Blazor circuit. */
export function createSelectedSessionTransport(fetchRequest = globalThis.fetch.bind(globalThis)) {
  let pending = Promise.resolve()
  async function execute(path, method, body, contentType) {
    const pathname = path.split('?')[0]
    if (!/^\/api\/(local-node|session)\//.test(pathname) || /[\\#\x00-\x20]|%2e|%2f|%5c/i.test(pathname)
      || pathname.includes('//') || pathname.split('/').some(part => part === '.' || part === '..')
      || !['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].includes(method))
      throw new Error('selected_session_request_invalid')
    const headers = { Accept: 'application/json' }
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
    send(path, method = 'GET', body = null, contentType = 'application/json') {
      // Each single-use token stays paired with its mutation. A refusal never retries a write.
      const result = pending.then(() => execute(path, method, body, contentType))
      pending = result.then(() => undefined, () => undefined)
      return result
    },
  }
}

async function envelope(response) {
  return { status: response.status, body: await response.text(), auditId: response.headers.get('X-Harborline-Audit-Id') }
}

const browserTransport = createSelectedSessionTransport()
export const send = (...arguments_) => browserTransport.send(...arguments_)
