import { useEffect, useState, type ComponentProps } from 'react'
import { SchemaForm } from '@harborline-software/ui-react'
// The RELEASED definition, copied out of the pinned platform checkout by
// scripts/build-local-feed.mjs (platform-package-ck-7). The app authors no status vocabulary and
// no status label of its own; it renders the platform's Form and the api's bindings.
import definitions from '../../../.feed/platform/configuration-activation.json'
import type {
  ConfigurationActivationClient,
  ConfigurationActivationOutcome,
  EffectiveGeneration,
} from './client/httpClient'

type FormView = ComponentProps<typeof SchemaForm>['view']

const ACTIVATION_VIEW = definitions.detail as unknown as FormView

/**
 * T-460. Read-only administrator view of one tenant's configuration activation.
 *
 * `outcome` is the api's answer to prepare or activate; its `detail` is the released binding and
 * is rendered verbatim through the platform's SchemaForm. The surface never inspects, recomputes
 * or re-labels the status, so a refused projection reports the refusal the api bound and cannot
 * present itself as effective.
 *
 * The effective read carries generation identity only (no bound detail), so it is shown as the
 * current baseline beside the form rather than fed through it.
 */
export function ConfigurationActivationPage({ client, outcome = null }: {
  readonly client: ConfigurationActivationClient
  readonly outcome?: ConfigurationActivationOutcome | null
}) {
  const [effective, setEffective] = useState<EffectiveGeneration | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    const abort = new AbortController()
    setEffective(null); setError(null)
    client.readEffective(abort.signal)
      .then(read => { if (!abort.signal.aborted) setEffective(read) })
      .catch((reason: unknown) => {
        if (!abort.signal.aborted) setError(reason instanceof Error ? reason.message : 'Unable to read the effective configuration generation.')
      })
    return () => abort.abort()
  }, [client])

  if (error) return <section role="alert"><p>{error}</p></section>
  if (!effective) return <p role="status">Loading configuration activation…</p>
  return <section>
    <p>Effective generation: <span data-testid="effective-generation">{effective.digest}</span></p>
    {outcome?.detail
      ? <SchemaForm view={ACTIVATION_VIEW} values={outcome.detail} readOnly onSubmit={() => undefined} />
      : <p role="status">No configuration activation has been reported for this generation.</p>}
  </section>
}
