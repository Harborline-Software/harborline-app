export interface SelectedSessionResponse { status: number; body: string; auditId: string | null; correlationId?: string | null }
export function validCorrelationId(value: unknown): boolean
export function validRequestHeader(name: string, value: unknown): boolean
export interface SelectedSessionTransport {
  send(path: string, method?: string, body?: string | Uint8Array | ArrayBuffer | Blob | null, contentType?: string,
    headers?: Readonly<Record<string, string>>): Promise<SelectedSessionResponse>
}
export function createSelectedSessionTransport(fetchRequest?: typeof fetch): SelectedSessionTransport
export const send: SelectedSessionTransport['send']
