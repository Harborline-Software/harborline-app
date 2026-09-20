import { useCallback, useEffect, useState, type ComponentProps } from 'react'
import { SchemaForm } from '@harborline-software/ui-react'
// The RELEASED definition and vocabulary, copied out of the pinned platform checkout by
// scripts/build-local-feed.mjs (platform-package-ck-7). The app authors no step vocabulary and no
// step label of its own; it renders the platform's Form over the api's bindings.
import definitions from '../../../.feed/platform/configuration-proposal.json'
import type { ConfigurationProposalClient, ProposedChange } from './client/proposalClient'

type FormView = ComponentProps<typeof SchemaForm>['view']

const PROPOSAL_VIEW = definitions.detail as unknown as FormView

/**
 * T-461. The domain expert's Proposed change surface: start from the effective generation, edit a
 * coherent set of definitions away from operational users, freeze a Saved version with a rationale,
 * and release the exact checked candidate as a Released package.
 *
 * Every answer's `detail` is the platform's released binding, rendered verbatim through the shared
 * SchemaForm. This surface never inspects, recomputes or re-labels a step, and it never derives a
 * digest of its own: the Released package digest it shows is the one the api took over the exported
 * artifact's bytes. A refused release therefore reports the refusal the api bound and cannot present
 * itself as a release.
 */
export function ConfigurationProposalPage({ client, proposalId }: {
  readonly client: ConfigurationProposalClient
  readonly proposalId: string
}) {
  const [proposed, setProposed] = useState<ProposedChange | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    const abort = new AbortController()
    setProposed(null); setError(null)
    client.read(proposalId, abort.signal)
      .then(read => { if (!abort.signal.aborted) setProposed(read) })
      .catch((reason: unknown) => {
        if (!abort.signal.aborted) setError(reason instanceof Error ? reason.message : 'Unable to read the proposed change.')
      })
    return () => abort.abort()
  }, [client, proposalId])

  const run = useCallback(async (act: () => Promise<ProposedChange>) => {
    setBusy(true); setError(null)
    try { setProposed(await act()) }
    catch (reason: unknown) { setError(reason instanceof Error ? reason.message : 'The act was refused.') }
    finally { setBusy(false) }
  }, [])

  if (error) return <section role="alert"><p>{error}</p></section>
  if (!proposed) return <p role="status">Loading the proposed change…</p>

  // The only act offered is the next one the recorded state allows: a Saved version cannot be
  // released while its check is stale, so the surface does not invite it.
  const canSave = proposed.edits.length > 0
  const canRelease = proposed.savedVersionCount > 0 && proposed.check?.isCurrent === true
  return <section>
    <SchemaForm view={PROPOSAL_VIEW} values={proposed.detail} readOnly onSubmit={() => undefined} />
    <div>
      <button type="button" disabled={busy || !canSave}
        onClick={() => void run(() => client.saveVersion(proposalId, 'Saved from the configuration surface.'))}>
        {label('saved')}
      </button>
      <button type="button" disabled={busy || !canRelease}
        onClick={() => void run(() => client.release(proposalId, proposed.savedVersionCount,
          `${proposed.tenantKey}.${proposalId}`, `1.0.${proposed.savedVersionCount}`))}>
        {label('released')}
      </button>
    </div>
  </section>
}

/** The released English label for a step; this surface authors none of its own. */
function label(step: string): string {
  const statuses = definitions.statuses as Readonly<Record<string, { values: Readonly<Record<string, string>> }>>
  return statuses[step].values.en
}
