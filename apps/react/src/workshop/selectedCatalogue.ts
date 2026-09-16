import { createSelectedSessionTransport, send } from '../../../shared/selected-session-transport.mjs'

// Projection POSTs share the browser transport's single-use antiforgery queue.
// Independent reads retain cancellation and cannot block a newer selection.

export async function requestSelectedCatalogue(path: string, signal?: AbortSignal, body?: unknown) {
  signal?.throwIfAborted()
  const transport = body === undefined
    ? createSelectedSessionTransport((url, options) => globalThis.fetch(url, { ...options, signal }))
    : { send }
  const response = await transport.send(path, body === undefined ? 'GET' : 'POST',
    body === undefined ? null : JSON.stringify(body), 'application/json', {}, { signal })
  signal?.throwIfAborted()
  return response
}

export async function readSelectedCatalogue<T>(path: string, signal?: AbortSignal, body?: unknown): Promise<T> {
  const response = await requestSelectedCatalogue(path, signal, body)
  if (response.status < 200 || response.status >= 300)
    throw new Error(`Workshop catalogue request failed (${response.status}).`)
  return JSON.parse(response.body) as T
}
