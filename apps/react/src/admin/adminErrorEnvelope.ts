/**
 * Ticket 094: every local-node admin api answers `{ code, detail? }` on error. This is the one parse
 * the React admin clients share: the code is carried through for localization, `message` renders
 * code + detail where the retired `error` prose was shown, and a body without a code degrades to the
 * HTTP status text.
 */
export type AdminErrorDetail = Readonly<Record<string, unknown>>

export interface AdminErrorEnvelope {
  readonly message: string
  readonly code: string | null
  readonly detail: AdminErrorDetail | null
}

/** `{ header: 'Idempotency-Key', maxLength: 200 }` -> `header=Idempotency-Key, maxLength=200`. */
function formatDetail(detail: AdminErrorDetail): string {
  return Object.entries(detail)
    .filter(([, value]) => value !== null && value !== undefined)
    .map(([key, value]) => `${key}=${String(value)}`)
    .join(', ')
}

/** Reads the error envelope off a failed response. */
export async function readAdminError(response: Response): Promise<AdminErrorEnvelope> {
  try {
    const body = await response.json() as { code?: unknown, detail?: unknown }
    if (typeof body.code !== 'string' || body.code.length === 0) {
      return { message: response.statusText, code: null, detail: null }
    }
    const detail = typeof body.detail === 'object' && body.detail !== null && !Array.isArray(body.detail)
      ? body.detail as AdminErrorDetail
      : null
    const params = detail ? formatDetail(detail) : ''
    return {
      message: params.length > 0 ? `${body.code} (${params})` : body.code,
      code: body.code,
      detail,
    }
  } catch {
    return { message: response.statusText, code: null, detail: null }
  }
}
