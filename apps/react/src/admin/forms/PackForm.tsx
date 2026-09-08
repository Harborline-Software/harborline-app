import { useEffect, useRef, useState } from 'react'
import { AuthorizationTrace } from '../../authorization/AuthorizationTrace'
import { useAuthorizationAdminClient } from '../authorization/AuthorizationAdminClientContext'
import { AuthorizationAdminError } from '../authorization/client'
import { useFormsAdminClient } from './FormsAdminClientContext'
import type { FormView, InternationalizedText } from './client/types'

const text = (value: InternationalizedText | null) => value?.values[value.defaultLocale] ?? ''
export function PackForm({ formId, onSubmitted }: { formId: string; onSubmitted: () => void }) {
  const client = useFormsAdminClient(), authorization = useAuthorizationAdminClient()
  const [view, setView] = useState<FormView>(), [error, setError] = useState<Error>()
  const [busy, setBusy] = useState(false), [receipt, setReceipt] = useState(''), [attempt, setAttempt] = useState(0)
  const cancellation = useRef<AbortController>(null)
  useEffect(() => {
    const abort = new AbortController(); cancellation.current = abort
    setView(undefined); setError(undefined); setReceipt('')
    void client.renderForm(formId, abort.signal).then(result => { if (!abort.signal.aborted) setView(result) })
      .catch((failure: Error) => { if (!abort.signal.aborted) setError(failure) })
    return () => abort.abort()
  }, [client, formId, attempt])
  const auditId = error instanceof AuthorizationAdminError ? error.auditId : undefined
  return <section aria-label="Grant a role">
    <AuthorizationTrace key={auditId} decision={auditId ? { auditId, read: authorization.readTrace } : undefined} />
    {error && <div role="alert">{error.message}<button type="button" onClick={() => setAttempt(n => n + 1)}>Reload form</button></div>}
    {!view ? !error && <p role="status">Loading form…</p> : <form aria-label={text(view.title)} onSubmit={async event => {
      event.preventDefault(); if (busy) return
      const values = new FormData(event.currentTarget), body = JSON.stringify(Object.fromEntries(values.entries()))
      const signal = cancellation.current!.signal
      setBusy(true); setError(undefined); setReceipt('')
      try {
        const result = await client.submitForm(view.formId, body, signal)
        if (!signal.aborted) { setReceipt(result.projection ? `Submission ${result.projection}. ${result.skips?.map(s => `${s.field}: ${s.reason}`).join('; ') ?? ''}` : 'Submission saved.'); onSubmitted() }
      } catch (failure) { if (!signal.aborted) setError(failure as Error) }
      finally { if (!signal.aborted) setBusy(false) }
    }}>
      <h2>{text(view.title)}</h2>
      {view.sections.map(section => <fieldset key={section.id} disabled={busy}><legend>{text(section.title)}</legend>
        {section.fields.filter(field => field.isReadable && field.rules?.visible !== false).map(field => <p key={field.name}>
          <label>{text(field.label)}<input name={field.name} defaultValue={field.value ?? ''} required={field.rules?.required} readOnly={field.rules?.readOnly} /></label>
        </p>)}
      </fieldset>)}
      <button type="submit" disabled={busy}>{busy ? 'Submitting…' : 'Submit'}</button>
    </form>}
    {receipt && <p role="status">{receipt}</p>}
  </section>
}
