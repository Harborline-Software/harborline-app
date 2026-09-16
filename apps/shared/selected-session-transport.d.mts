export interface SelectedSessionResponse { status: number; body: string; auditId: string | null; correlationId?: string | null }
export interface SelectedSessionBytesResponse extends SelectedSessionResponse { bytes: Uint8Array | null }
export interface SelectedSessionOptions { signal?: AbortSignal }
export interface SelectedSessionCaller { invokeMethodAsync(method: 'CanDispatch'): Promise<boolean> }
export function validCorrelationId(value: unknown): boolean
export function validRequestHeader(name: string, value: unknown): boolean
export interface SelectedSessionTransport {
  send(path: string, method?: string, body?: string | Uint8Array | ArrayBuffer | Blob | null, contentType?: string,
    headers?: Readonly<Record<string, string>>, options?: SelectedSessionOptions): Promise<SelectedSessionResponse>
  sendBytes(path: string, method?: string, body?: string | Uint8Array | ArrayBuffer | Blob | null, contentType?: string,
    headers?: Readonly<Record<string, string>>, options?: SelectedSessionOptions): Promise<SelectedSessionBytesResponse>
  sendForCaller(caller: SelectedSessionCaller, ...args: Parameters<SelectedSessionTransport['send']>): Promise<SelectedSessionResponse>
  sendBytesForCaller(caller: SelectedSessionCaller, ...args: Parameters<SelectedSessionTransport['sendBytes']>): Promise<SelectedSessionBytesResponse>
}
export function createSelectedSessionTransport(fetchRequest?: typeof fetch): SelectedSessionTransport
export const send: SelectedSessionTransport['send']
export const sendBytes: SelectedSessionTransport['sendBytes']
export const sendForCaller: SelectedSessionTransport['sendForCaller']
export const sendBytesForCaller: SelectedSessionTransport['sendBytesForCaller']
