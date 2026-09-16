import { send } from './selected-session-transport.mjs'

const object = value => value !== null && typeof value === 'object' && !Array.isArray(value)
const safeName = value => typeof value === 'string' && /^[A-Za-z][A-Za-z0-9_-]*$/.test(value)
const safePath = value => typeof value === 'string' && /^\/api\/(local-node|session)\//.test(value)
  && !/[\\?#\x00-\x20]|%2e|%2f|%5c/i.test(value) && !value.includes('//')
  && !value.split('/').some(segment => segment === '.' || segment === '..')

/** Consumes an admitted server descriptor. It never interprets action IDs or operation names. */
export function preparePackRequest(action, sources = {}) {
  try {
    const dispatch = action?.dispatch
    const descriptor = dispatch?.descriptor
    if (dispatch?.schemaVersion !== 1 || dispatch.kind !== 'request' || !object(descriptor)
      || typeof descriptor.id !== 'string' || !descriptor.id
      || !['selected-session', 'device-reachable-product'].includes(descriptor.audience)
      || !['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].includes(descriptor.method)
      || !['application/json', 'application/octet-stream'].includes(descriptor.contentType)
      || (descriptor.method !== 'GET' && descriptor.requiresAntiforgery !== true)
      || !safePath(descriptor.routeTemplate) || !Array.isArray(descriptor.inputs) || !object(dispatch.bindings)) return null
    let path = descriptor.routeTemplate
    const names = new Set(), wireNames = new Set(), fields = {}, headers = {}
    let root, rootPresent = false
    for (const input of descriptor.inputs) {
      if (!safeName(input.name) || names.has(input.name) || !Object.hasOwn(dispatch.bindings, input.name)) return null
      names.add(input.name)
      const value = bindingValue(dispatch.bindings[input.name], sources)
      if (!matches(input.kind, value) || !['Path', 'BodyField', 'BodyRoot', 'Header'].includes(input.placement)
        || typeof input.wireName !== 'string') return null
      const wireKey = `${input.placement}:${input.wireName.toLowerCase()}`
      if (wireNames.has(wireKey)) return null
      wireNames.add(wireKey)
      if (input.placement === 'Path') {
        const token = `{${input.wireName}}`
        if (input.kind !== 'Text' || !safeName(input.wireName) || path.split(token).length !== 2
          || !value || /[\/\\?#\x00-\x20]|%2e|%2f|%5c/i.test(value) || ['.', '..'].includes(value)) return null
        path = path.replace(token, encodeURIComponent(value))
      } else if (input.placement === 'Header') {
        if (input.kind !== 'Text' || input.wireName !== 'Idempotency-Key' || !/^[A-Za-z0-9._:-]{1,128}$/.test(value)) return null
        headers[input.wireName] = value
      } else if (input.placement === 'BodyRoot') {
        if (rootPresent || input.wireName !== '' || !['Object', 'Binary'].includes(input.kind)) return null
        if (input.kind !== (descriptor.contentType === 'application/json' ? 'Object' : 'Binary')) return null
        rootPresent = true
        root = value
      } else {
        if (!safeName(input.wireName) || input.kind === 'Binary') return null
        fields[input.wireName] = value
      }
    }
    if (Object.keys(dispatch.bindings).length !== names.size || /[{}]/.test(path) || !safePath(path)
      || (rootPresent && Object.keys(fields).length)) return null
    let body = null
    if (descriptor.method === 'GET') {
      if (rootPresent || Object.keys(fields).length || descriptor.contentType !== 'application/json') return null
    } else if (descriptor.contentType === 'application/json') {
      if (rootPresent && !object(root)) return null
      body = JSON.stringify(rootPresent ? root : fields)
    } else if (descriptor.contentType === 'application/octet-stream' && rootPresent
      && (root instanceof ArrayBuffer || ArrayBuffer.isView(root) || (typeof Blob !== 'undefined' && root instanceof Blob))) {
      body = root
    } else return null
    return { path, method: descriptor.method, contentType: descriptor.contentType, body, headers }
  } catch { return null }
}

function bindingValue(binding, sources) {
  if (!object(binding)) return undefined
  const keys = Object.keys(binding)
  if (keys.length === 1 && keys[0] === 'literal') return binding.literal
  if (keys.length !== 2 || !keys.includes('source') || !keys.includes('pointer')) return undefined
  if (binding.source === 'file' && binding.pointer === '') return sources.file
  if (!['input', 'selection'].includes(binding.source)) return undefined
  const source = sources[binding.source]
  if (binding.source === 'input' && binding.pointer === '') return source
  if (typeof binding.pointer !== 'string' || !/^\/([^/~]|~[01])+$/.test(binding.pointer) || !object(source)) return undefined
  const name = binding.pointer.slice(1).replaceAll('~1', '/').replaceAll('~0', '~')
  return Object.hasOwn(source, name) ? source[name] : undefined
}

function matches(kind, value) {
  if (kind === 'Text') return typeof value === 'string'
  if (kind === 'Number') return typeof value === 'number' && Number.isFinite(value)
  if (kind === 'Boolean') return typeof value === 'boolean'
  if (kind === 'Object') return object(value) && !(value instanceof ArrayBuffer) && !ArrayBuffer.isView(value)
    && !(typeof Blob !== 'undefined' && value instanceof Blob)
  if (kind === 'Binary') return value instanceof ArrayBuffer || ArrayBuffer.isView(value)
    || (typeof Blob !== 'undefined' && value instanceof Blob)
  return false
}

export async function dispatchPackRequest(action, sources, transport = { send }) {
  const request = preparePackRequest(action, sources)
  if (!request) return { code: 'pack_action_unsupported' }
  return transport.send(request.path, request.method, request.body, request.contentType, request.headers)
}
