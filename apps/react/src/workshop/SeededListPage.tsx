import { useEffect, useRef, useState } from 'react'
import { type ViewRenderPlan, type ViewRuntimeRow } from '@harborline-software/ui-react'
import { WorkshopWorkflow } from './WorkshopWorkflow'

const KINDS: Readonly<Record<string, string>> = {
  'asset-types': 'AssetTypeDefinition', forms: 'FormDefinition', workflows: 'WorkflowDefinition',
  standards: 'StandardsCatalog', defaults: 'CascadeDefaults', terminology: 'TerminologyOverride',
  documents: 'TemplateDefinition', taxonomies: 'TaxonomyDefinition', reports: 'ReportDefinition',
  'data-exchanges': 'DataExchangeDefinition', 'standing-rules': 'StandingRuleDefinition',
  schedules: 'ScheduleDefinition', views: 'ViewDefinition',
}

interface CatalogueEntry {
  readonly id: string
  readonly version: string
  readonly status: string
  readonly kind?: string | number
  readonly provenance?: { readonly packKey?: string; readonly packVersion?: string; readonly kind: string }
  readonly sealed?: boolean
  readonly updatedAt?: string
  readonly definitionHash?: string | null
  readonly title?: { readonly defaultLocale: string; readonly values: Readonly<Record<string, string>> } | null
  readonly body?: Readonly<Record<string, unknown>>
  readonly renderPlan?: ViewRenderPlan | null
}

interface CatalogueList { readonly entries: readonly CatalogueEntry[]; readonly kindsUnavailable: readonly (string | number)[] }

function title(entry: CatalogueEntry): string {
  return entry.title?.values[entry.title.defaultLocale] ?? entry.title?.values.en ?? entry.id
}

function row(entry: CatalogueEntry): ViewRuntimeRow {
  return { ...entry.body, ...entry, id: `${entry.id}@${entry.version}`, formId: entry.id, title: title(entry), catalogue: entry }
}

type CatalogueReadFailure =
  | { readonly kind: 'http'; readonly status: number }
  | { readonly kind: 'catalogue'; readonly status: number; readonly code: string }
  | { readonly kind: 'authorization'; readonly status: number; readonly code: string; readonly permission: string
      readonly title: string; readonly detail: string; readonly remediation: string
      readonly auditId: string | null; readonly correlationId: string | null }

function catalogueReadFailure(status: number, body: unknown): CatalogueReadFailure {
  const fallback = { kind: 'http', status } satisfies CatalogueReadFailure
  if (typeof body !== 'object' || body === null || Array.isArray(body)
    || !('code' in body) || typeof body.code !== 'string') return fallback
  if ((status === 400 && body.code === 'catalogue.kind_unknown')
    || (status === 404 && body.code === 'catalogue.definition_not_found')) {
    return { kind: 'catalogue', status, code: body.code }
  }
  // RequestAuthorization.Write owns this public 403 shape, including the server-owned reason code.
  // Diagnostic, pointers, inline traces and other response members are never projected here.
  if (status !== 403 || body.code.trim() !== body.code || !/^[a-z][a-z0-9]*(?:[._-][a-z0-9]+)+$/.test(body.code)
    || !('permission' in body) || body.permission !== 'catalogue:read'
    || !('title' in body) || typeof body.title !== 'string' || !body.title.trim()
    || !('detail' in body) || typeof body.detail !== 'string' || !body.detail.trim()
    || !('remediation' in body) || typeof body.remediation !== 'string' || !body.remediation.trim()) return fallback
  const auditId = 'auditId' in body ? body.auditId : null
  const correlationId = 'correlationId' in body ? body.correlationId : null
  const receiptId = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
  if (('auditId' in body && auditId === null) || ('correlationId' in body && correlationId === null)
    || (auditId !== null && (typeof auditId !== 'string' || !receiptId.test(auditId)))
    || (correlationId !== null && (typeof correlationId !== 'string' || !receiptId.test(correlationId)))) return fallback
  return { kind: 'authorization', status, code: body.code, permission: body.permission,
    title: body.title, detail: body.detail, remediation: body.remediation, auditId, correlationId }
}

function catalogueReadMessage(failure: CatalogueReadFailure): string {
  const status = `Workshop catalogue request failed (${failure.status}).`
  switch (failure.kind) {
    case 'http': return status
    case 'catalogue': return `${status} ${failure.code}`
    case 'authorization': return [status, failure.code, failure.permission, failure.title, failure.detail, failure.remediation,
      failure.auditId === null ? null : `Audit ID: ${failure.auditId}`,
      failure.correlationId === null ? null : `Correlation ID: ${failure.correlationId}`].filter(value => value !== null).join(' ')
    default: { const exhaustive: never = failure; return exhaustive }
  }
}

async function readJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  // During development the Vite proxy owns the node hop and attaches the bearer token. Keeping
  // this request same-origin is therefore part of the authentication boundary, not just a CORS
  // convenience. A built bundle has no proxy and uses the explicitly configured origin.
  const origin = import.meta.env.DEV ? '' : (import.meta.env.VITE_FORMS_API_ORIGIN?.replace(/\/$/, '') ?? '')
  const response = await fetch(`${origin}${path}`, { credentials: 'include', signal })
  if (!response.ok) {
    let body: unknown = null
    try { body = await response.json() } catch (error: unknown) { if (signal?.aborted) throw error }
    throw new Error(catalogueReadMessage(catalogueReadFailure(response.status, body)))
  }
  return await response.json() as T
}

export interface SeededListPageProps {
  readonly itemId: string
  readonly viewId?: string
  readonly selectedRowId?: string | null
  readonly onRowActivate?: (row: ViewRuntimeRow) => void
  readonly onSelectionRestored?: (row: ViewRuntimeRow) => void
}

export function SeededListPage({ itemId, viewId = `platform.list.${itemId}`, selectedRowId, onRowActivate, onSelectionRestored }: SeededListPageProps) {
  const [state, setState] = useState<{ itemId: string; viewId: string; request: AbortController; plan: ViewRenderPlan; rows: readonly ViewRuntimeRow[]; list: CatalogueList } | null>(null)
  const [error, setError] = useState<{ itemId: string; viewId: string; message: string } | null>(null)
  const activeRequest = useRef<AbortController | null>(null)
  const resolvedSelection = useRef<string | null>(null)
  useEffect(() => {
    const kind = KINDS[itemId]
    const abort = new AbortController()
    activeRequest.current = abort
    setState(null); setError(null)
    if (!kind) { setError({ itemId, viewId, message: 'This Workshop list is not declared by the platform pack.' }); return () => abort.abort() }
    void Promise.all([
      readJson<CatalogueEntry>(`/api/local-node/catalogue/definitions/ViewDefinition/${encodeURIComponent(viewId)}`, abort.signal),
      readJson<CatalogueList>(`/api/local-node/catalogue/definitions?kind=${encodeURIComponent(kind)}`, abort.signal),
    ]).then(([view, list]) => {
      if (abort.signal.aborted) return
      if (!view.renderPlan) throw new Error('The seeded view has no active compiled render plan.')
      setState({ itemId, viewId, request: abort, plan: view.renderPlan, rows: list.entries.map(row), list })
    }).catch((reason: unknown) => { if (!abort.signal.aborted) setError({ itemId, viewId,
      message: reason instanceof Error ? reason.message : 'Unable to load Workshop list.' }) })
    return () => abort.abort()
  }, [itemId, viewId])
  useEffect(() => {
    if (!selectedRowId) { resolvedSelection.current = null; return }
    if (!state || state.itemId !== itemId || state.viewId !== viewId || state.list.kindsUnavailable.length || !supported(state.plan) || !onSelectionRestored) return
    const selectionKey = `${viewId}:${selectedRowId}`
    if (resolvedSelection.current === selectionKey) return
    const restored = state.rows.find(candidate => candidate.id === selectedRowId)
    if (restored) {
      resolvedSelection.current = selectionKey
      onSelectionRestored(restored)
    }
  }, [itemId, viewId, onSelectionRestored, selectedRowId, state])
  if (error?.itemId === itemId && error.viewId === viewId) return <section role="alert"><p>{error.message}</p></section>
  if (!state || state.itemId !== itemId || state.viewId !== viewId) return <p role="status">Loading Workshop list…</p>
  if (state.list.kindsUnavailable.length) return <p role="status">{KINDS[itemId]} is unavailable in this host.</p>
  if (!supported(state.plan)) return null
  const refreshRows = async () => {
    const kind = KINDS[itemId]
    const request = state.request
    if (!kind || activeRequest.current !== request || request.signal.aborted) return
    try {
      const list = await readJson<CatalogueList>(`/api/local-node/catalogue/definitions?kind=${encodeURIComponent(kind)}`, request.signal)
      if (activeRequest.current !== request || request.signal.aborted) return
      setState(current => current?.itemId === itemId && current.viewId === viewId
        ? { ...current, rows: list.entries.map(row), list }
        : current)
    } catch (reason: unknown) {
      if (activeRequest.current === request && !request.signal.aborted) setError({ itemId, viewId,
        message: reason instanceof Error ? reason.message : 'Unable to load Workshop list.' })
    }
  }
  return <WorkshopWorkflow key={`${state.plan.definitionKind}:${state.plan.definitionId}@${state.plan.definitionVersion}:${state.plan.definitionHash}`} plan={state.plan} rows={state.rows} onRowActivate={id => {
    const activated = state.rows.find(candidate => candidate.id === id)
    if (activated) onRowActivate?.(activated)
  }} onActivated={refreshRows} />
}

function supported(plan: ViewRenderPlan): boolean {
  return plan.definitionKind === 'ViewDefinition' && plan.bindings.viewKind === 'layout.table'
    && Array.isArray(plan.bindings.parameters?.fields)
}
