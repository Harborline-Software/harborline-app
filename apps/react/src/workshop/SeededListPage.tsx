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
  readonly title?: { readonly defaultLocale: string; readonly values: Readonly<Record<string, string>> } | null
  readonly body?: Readonly<Record<string, unknown>>
  readonly renderPlan?: ViewRenderPlan | null
}

interface CatalogueList { readonly entries: readonly CatalogueEntry[]; readonly kindsUnavailable: readonly string[] }

function title(entry: CatalogueEntry): string {
  return entry.title?.values[entry.title.defaultLocale] ?? entry.title?.values.en ?? entry.id
}

function row(entry: CatalogueEntry): ViewRuntimeRow {
  return { id: `${entry.id}@${entry.version}`, ...entry.body, formId: entry.id, title: title(entry), version: entry.version, status: entry.status }
}

async function readJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  // During development the Vite proxy owns the node hop and attaches the bearer token. Keeping
  // this request same-origin is therefore part of the authentication boundary, not just a CORS
  // convenience. A built bundle has no proxy and uses the explicitly configured origin.
  const origin = import.meta.env.DEV ? '' : (import.meta.env.VITE_FORMS_API_ORIGIN?.replace(/\/$/, '') ?? '')
  const response = await fetch(`${origin}${path}`, { credentials: 'include', signal })
  if (!response.ok) throw new Error(`Workshop catalogue request failed (${response.status}).`)
  return await response.json() as T
}

export interface SeededListPageProps {
  readonly itemId: string
  readonly selectedRowId?: string | null
  readonly onRowActivate?: (row: ViewRuntimeRow) => void
  readonly onSelectionRestored?: (row: ViewRuntimeRow) => void
}

export function SeededListPage({ itemId, selectedRowId, onRowActivate, onSelectionRestored }: SeededListPageProps) {
  const [state, setState] = useState<{ plan: ViewRenderPlan; rows: readonly ViewRuntimeRow[] } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const resolvedSelection = useRef<string | null>(null)
  useEffect(() => {
    const kind = KINDS[itemId]
    const abort = new AbortController()
    setState(null); setError(null)
    if (!kind) { setError('This Workshop list is not declared by the platform pack.'); return () => abort.abort() }
    void Promise.all([
      readJson<CatalogueEntry>(`/api/local-node/catalogue/definitions/ViewDefinition/platform.list.${itemId}`, abort.signal),
      readJson<CatalogueList>(`/api/local-node/catalogue/definitions?kind=${encodeURIComponent(kind)}`, abort.signal),
    ]).then(([view, list]) => {
      if (abort.signal.aborted) return
      if (!view.renderPlan) throw new Error('The seeded view has no active compiled render plan.')
      setState({ plan: view.renderPlan, rows: list.entries.map(row) })
    }).catch((reason: unknown) => { if (!abort.signal.aborted) setError(reason instanceof Error ? reason.message : 'Unable to load Workshop list.') })
    return () => abort.abort()
  }, [itemId])
  useEffect(() => {
    if (!state || !selectedRowId || !onSelectionRestored) return
    const selectionKey = `${itemId}:${selectedRowId}`
    if (resolvedSelection.current === selectionKey) return
    const restored = state.rows.find(candidate => candidate.id === selectedRowId)
    if (restored) {
      resolvedSelection.current = selectionKey
      onSelectionRestored(restored)
    }
  }, [itemId, onSelectionRestored, selectedRowId, state])
  if (error) return <section role="alert"><p>{error}</p></section>
  if (!state) return <p role="status">Loading Workshop list…</p>
  const refreshRows = async () => {
    const kind = KINDS[itemId]
    if (!kind) return
    const list = await readJson<CatalogueList>(`/api/local-node/catalogue/definitions?kind=${encodeURIComponent(kind)}`)
    setState(current => current?.plan.definitionId === `platform.list.${itemId}`
      ? { ...current, rows: list.entries.map(row) }
      : current)
  }
  return <WorkshopWorkflow key={`${state.plan.definitionKind}:${state.plan.definitionId}@${state.plan.definitionVersion}:${state.plan.definitionHash}`} plan={state.plan} rows={state.rows} onRowActivate={id => {
    const activated = state.rows.find(candidate => candidate.id === id)
    if (activated) onRowActivate?.(activated)
  }} onActivated={refreshRows} />
}
