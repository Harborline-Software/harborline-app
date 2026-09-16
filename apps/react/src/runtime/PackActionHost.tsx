import { useEffect, useMemo, useState } from 'react'
import { SchemaForm, ViewRuntime, type ViewRenderPlan } from '@harborline-software/ui-react'
import { createPackActionRuntime, type PackRuntimeState } from '../../../shared/pack-action-runtime.mjs'
import { formViewFromPlan } from '../workshop/WorkshopWorkflow'

/** Renders admitted request metadata; action IDs and operation names have no compiled behavior. */
export function PackActionHost({ viewId }: { readonly viewId: string }) {
  const runtime = useMemo(() => createPackActionRuntime(), [viewId])
  const [state, setState] = useState<PackRuntimeState>(() => runtime.snapshot())
  const [values, setValues] = useState<Readonly<Record<string, unknown>>>({})
  const [file, setFile] = useState<File>()
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    let live = true
    setBusy(true)
    void runtime.load(viewId).then(next => { if (live) { setState(next); setBusy(false) } })
    return () => { live = false }
  }, [runtime, viewId])
  const begin = async (id: string) => {
    setBusy(true); setValues({}); setFile(undefined)
    try { setState(await runtime.begin(id)) } finally { setBusy(false) }
  }
  const invoke = async (input: Readonly<Record<string, unknown>> = values) => {
    setBusy(true)
    try { setState(await runtime.invoke(input, file)) } finally { setBusy(false) }
  }
  const form = state.inputPlan ? formViewFromPlan(state.inputPlan) : null
  return <section aria-label="Pack actions">
    {busy && <p role="status">Working…</p>}
    {state.plan !== null && <ViewRuntime plan={state.plan as ViewRenderPlan} rows={state.rows}
      empty="No rows." actionsDisabled={busy} onRowActivate={id => setState(runtime.select(id))}
      onAction={id => { void begin(id) }} />}
    {state.selectedId && <p role="status">Selected: {state.selectedId}</p>}
    {state.activeAction && <section aria-label={state.activeAction.label}>
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
    {state.error && <p role="alert">{state.error}</p>}
    {state.receipt && <section aria-label="Action result" aria-live="polite">
      <p>{state.receipt.status >= 200 && state.receipt.status < 300 ? 'Completed' : 'Refused'} ({state.receipt.status})</p>
      {state.receipt.auditId && <p>Audit: {state.receipt.auditId}</p>}
      {state.receipt.correlationId && <p>Correlation: {state.receipt.correlationId}</p>}
      <pre>{state.receipt.text}</pre>
    </section>}
  </section>
}
