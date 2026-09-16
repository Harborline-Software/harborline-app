import type { SelectedSessionResponse, SelectedSessionTransport } from './selected-session-transport.mjs'

export interface PackRequestSources {
  selection?: Readonly<Record<string, unknown>>
  input?: Readonly<Record<string, unknown>>
  file?: ArrayBuffer | Uint8Array | Blob
}
export interface PreparedPackRequest {
  path: string
  method: string
  body: string | ArrayBuffer | Uint8Array | Blob | null
  contentType: string
  headers: Record<string, string>
}
export interface UnsupportedPackAction { code: 'pack_action_unsupported' }
export function preparePackRequest(action: unknown, sources?: PackRequestSources): PreparedPackRequest | null
export function dispatchPackRequest(action: unknown, sources?: PackRequestSources,
  transport?: SelectedSessionTransport): Promise<SelectedSessionResponse | UnsupportedPackAction>
