import { useEffect, useState } from 'react'
import { useAuthorizationAdminClient } from './AuthorizationAdminClientContext'
import { AuthorizationAdminError, type AccessHolder } from './client'

export function AccessHoldersPage() {
  const client = useAuthorizationAdminClient()
  const [holders, setHolders] = useState<readonly AccessHolder[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    const abort = new AbortController()
    setHolders(null); setError(null)
    void client.listHolders(abort.signal).then(result => {
      if (!Array.isArray(result.holders)) throw new Error('The holders service returned an invalid response.')
      if (!abort.signal.aborted) setHolders(result.holders)
    }).catch((failure: unknown) => {
      if (!abort.signal.aborted) setError(failure instanceof AuthorizationAdminError
        ? `${failure.message} (${failure.status}: ${failure.code})`
        : failure instanceof Error ? failure.message : 'Unable to load holders.')
    })
    return () => abort.abort()
  }, [client, attempt])
  return <section aria-labelledby="access-holders-heading" style={{ overflowWrap: 'anywhere' }}>
    <h1 id="access-holders-heading">Holders</h1>
    {error ? <div role="alert"><p>{error}</p><button type="button" onClick={() => setAttempt(value => value + 1)}>Retry holders</button></div>
      : holders === null ? <p role="status">Loading holders…</p>
        : holders.length === 0 ? <p>No active holders.</p>
          : holders.map(row => <article key={row.grantId} aria-label={`Grant ${row.grantId}`}>
            <dl>
              <dt>Party</dt><dd>{row.partyId}</dd><dt>Source</dt><dd>{row.source}</dd>
              {row.attributionFailure && <><dt>Attribution failure</dt><dd>{row.attributionFailure}</dd></>}
              <dt>Role or grant</dt><dd>{row.role ? `${row.role.vocabulary} / ${row.role.name}` : row.grantId}</dd>
              <dt>Grant</dt><dd>{row.grantId}</dd><dt>Granter</dt><dd>{row.granter}</dd>
              <dt>Scope</dt><dd>{row.scope}</dd>
              <dt>Effective from</dt><dd><time dateTime={row.effectiveFrom}>{row.effectiveFrom}</time></dd>
              <dt>Effective to</dt><dd>{row.effectiveTo ? <time dateTime={row.effectiveTo}>{row.effectiveTo}</time> : 'No end date'}</dd>
            </dl>
          </article>)}
  </section>
}
