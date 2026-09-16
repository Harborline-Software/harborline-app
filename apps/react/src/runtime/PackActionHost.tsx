import { useEffect, useRef, useState } from 'react'
import { SchemaForm, ViewRuntime, type ViewRenderPlan } from '@harborline-software/ui-react'
import { createPackActionRuntime, type PackActionRuntime, type PackRuntimeState } from '../../../shared/pack-action-runtime.mjs'
import { formViewFromPlan } from '../workshop/WorkshopWorkflow'

/** Renders admitted request metadata; action IDs and operation names have no compiled behavior. */
export function PackActionHost({ viewId, onNavigationChanged }: { readonly viewId: string; readonly onNavigationChanged?: () => void }) {
  const runtime = useRef<PackActionRuntime | null>(null)
  const [state, setState] = useState<PackRuntimeState | null>(null)
  const [values, setValues] = useState<Readonly<Record<string, unknown>>>({})
  const [file, setFile] = useState<File>()
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    const current = createPackActionRuntime()
    runtime.current = current
    setState(current.snapshot())
    setBusy(true)
    void current.load(viewId).then(next => { if (runtime.current === current) { setState(next); setBusy(false) } })
    return () => { current.dispose(); if (runtime.current === current) runtime.current = null }
  }, [viewId])
  const begin = async (id: string) => {
    const current = runtime.current
    if (!current) return
    setBusy(true); setValues({}); setFile(undefined)
    try { const next = await current.begin(id); if (runtime.current === current) setState(next) }
    finally { if (runtime.current === current) setBusy(false) }
  }
  const invoke = async (input: Readonly<Record<string, unknown>> = values) => {
    const current = runtime.current
    if (!current) return
    setBusy(true)
    try {
      const next = await current.invoke(input, file)
      if (runtime.current !== current) return
      setState(next)
      if (next.navigationRevision !== state?.navigationRevision) onNavigationChanged?.()
    } finally { if (runtime.current === current) setBusy(false) }
  }
  const reload = async () => {
    const current = runtime.current
    if (!current) return
    setBusy(true)
    try { const next = await current.load(viewId); if (runtime.current === current) setState(next) }
    finally { if (runtime.current === current) setBusy(false) }
  }
  if (!state) return <section aria-label="Pack actions"><p role="status">Working…</p></section>
  const form = state.inputPlan ? formViewFromPlan(state.inputPlan) : null
  return <section aria-label="Pack actions">
    {busy && <p role="status">Working…</p>}
    {state.plan !== null && <ViewRuntime plan={state.plan as ViewRenderPlan} rows={state.rows.map(row => ({ ...row.values, id: row.id }))}
      empty="No rows." actionsDisabled={busy} onRowActivate={id => { if (runtime.current) setState(runtime.current.select(id)) }}
      onAction={id => { void begin(id) }} />}
    {state.selectedId && <p role="status">Selected: {state.selectedId}</p>}
    {state.activeAction && <section aria-label={state.activeAction.label}>
      {Object.keys(state.requestDetails).length > 0 && <details><summary>Request details</summary>
        <fieldset disabled={busy || state.requestLocked}><legend>Request identifiers</legend>
          {Object.entries(state.requestDetails).map(([name, value]) => <label key={name}>
            {name === 'id' ? 'Request ID' : name === 'idempotencyKey' ? 'Idempotency key' : 'Correlation ID'}
            <input name={`request.${name}`} value={value} onChange={event => { if (runtime.current) setState(runtime.current.setRequestDetails(
              Object.entries({ ...state.requestDetails, [name]: event.target.value }))) }} />
          </label>)}
        </fieldset>
        <button type="button" disabled={busy} onClick={() => { if (runtime.current) setState(runtime.current.newRequest()) }}>New request</button>
      </details>}
      {form ? <SchemaForm key={state.activeAction.id} view={form} values={values} onValuesChange={setValues}
        disabled={busy} strings={{ submit: state.activeAction.label, submitting: state.activeAction.label }}
        onSubmit={async input => { await invoke(input) }} />
        : <fieldset disabled={busy}>
          <legend>{state.activeAction.label}</legend>
          {state.activeAction.fileInput && <label>Package file
            <input key={state.activeAction.id} type="file" accept={state.activeAction.fileInput.accept}
              onChange={event => setFile(event.target.files?.[0])} />
          </label>}
          <button type="button" disabled={!!state.activeAction.fileInput && !file} onClick={() => { void invoke() }}>{state.activeAction.label}</button>
        </fieldset>}
    </section>}
    {state.error && <section role="alert"><p>{state.error}</p><button type="button" disabled={busy}
      onClick={() => { void reload() }}>Reload view</button></section>}
    {state.receipt && <section aria-label="Action result" aria-live="polite">
      <p>{state.receipt.status >= 200 && state.receipt.status < 300 ? 'Completed' : 'Refused'} ({state.receipt.status})</p>
      {state.receipt.auditId && <p>Audit: {state.receipt.auditId}</p>}
      {state.receipt.correlationId && <p>Correlation: {state.receipt.correlationId}</p>}
      <pre>{state.receipt.text}</pre>
    </section>}
  </section>
}
