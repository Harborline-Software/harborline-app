const prefix = '/api/selected-node/'
const selectedCookie = '__Host-hl-selected'
const antiforgeryHeader = 'x-harborline-antiforgery'
const maximumBodyBytes = 10 * 1024 * 1024
const responseHeaders = ['content-type', antiforgeryHeader, 'x-harborline-audit-id', 'x-harborline-audit-correlation', 'x-correlation-id']

/** A separate, cookie-only transport. Never reuse the bootstrap proxy's headers or cookie state. */
export function createSelectedSessionProxy(nodeOrigin) {
  const target = new URL(nodeOrigin)
  if (!['http:', 'https:'].includes(target.protocol) || target.username || target.password
    || target.pathname !== '/' || target.search || target.hash) throw new Error('A fixed local-node origin is required.')

  return async (request, response, next) => {
    if (!request.url?.startsWith(prefix)) return next()
    const refuse = (status, code) => response.writeHead(status, {
      'Content-Type': 'application/json', 'Cache-Control': 'no-store',
    }).end(JSON.stringify({ error: code }))
    const path = request.url.slice(prefix.length)
    const pathname = path.split('?')[0]
    if (!/^(local-node|session)\//.test(pathname) || /[\\#\x00-\x20]|%2e|%2f|%5c/i.test(pathname)
      || pathname.includes('//') || pathname.split('/').some(part => part === '.' || part === '..'))
      return refuse(400, 'selected_session_path_invalid')

    const method = request.method ?? 'GET'
    if (!['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].includes(method)) return refuse(405, 'selected_session_method_invalid')
    const read = method === 'GET'
    const appOrigin = `${request.socket.encrypted ? 'https' : 'http'}://${request.headers.host}`
    if ((request.headers.origin && request.headers.origin !== appOrigin)
      || (request.headers['sec-fetch-site'] && request.headers['sec-fetch-site'] !== 'same-origin')
      || (!request.headers.origin && (!read || request.headers['sec-fetch-site'] !== 'same-origin')))
      return refuse(403, 'selected_session_origin_invalid')

    const cookies = (request.headers.cookie ?? '').split(';').map(part => part.trim())
      .filter(part => part.startsWith(`${selectedCookie}=`))
    if (cookies.length !== 1 || !/^__Host-hl-selected=[A-Za-z0-9_-]{1,256}$/.test(cookies[0]))
      return refuse(401, 'selected_session_required')
    const token = request.headers[antiforgeryHeader]
    if (!read && (typeof token !== 'string' || !/^[A-Za-z0-9_-]{1,256}$/.test(token)))
      return refuse(403, 'selected_session_antiforgery_required')
    const requestId = request.headers['idempotency-key']
    if (requestId !== undefined && (typeof requestId !== 'string' || !/^[A-Za-z0-9._:-]{1,128}$/.test(requestId)))
      return refuse(400, 'selected_session_headers_invalid')
    const correlationId = request.headers['x-correlation-id']
    if (correlationId !== undefined && (typeof correlationId !== 'string'
      || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(correlationId)
      || correlationId === '00000000-0000-0000-0000-000000000000'))
      return refuse(400, 'selected_session_headers_invalid')

    // Positive allowlist: neither inbound Authorization nor server bootstrap credentials can flow.
    const headers = { Cookie: cookies[0], Accept: 'application/json' }
    if (requestId !== undefined) headers['Idempotency-Key'] = requestId
    if (correlationId !== undefined) headers['X-Correlation-ID'] = correlationId
    if (!read) headers[antiforgeryHeader] = token
    if (request.headers['content-type']) headers['Content-Type'] = request.headers['content-type']
    try {
      let body
      if (!read) {
        const chunks = []
        let length = 0
        for await (const chunk of request) {
          length += chunk.length
          if (length > maximumBodyBytes) return refuse(413, 'selected_session_body_too_large')
          chunks.push(chunk)
        }
        body = Buffer.concat(chunks)
      }
      const upstream = await fetch(new URL(`/api/${path}`, target), { method, headers, body, redirect: 'manual' })
      // Redirects must not move a credentialed request outside this configured node.
      if (upstream.status >= 300 && upstream.status < 400) return refuse(502, 'selected_session_redirect_refused')
      const outgoing = { 'Cache-Control': 'no-store' }
      for (const name of responseHeaders) if (upstream.headers.has(name)) outgoing[name] = upstream.headers.get(name)
      const selectedCookies = upstream.headers.getSetCookie().filter(value => value.startsWith(`${selectedCookie}=`))
      if (selectedCookies.length) outgoing['Set-Cookie'] = selectedCookies
      const bytes = new Uint8Array(await upstream.arrayBuffer())
      response.writeHead(upstream.status, outgoing).end(bytes)
    } catch {
      return refuse(502, 'selected_session_upstream_unavailable')
    }
  }
}
