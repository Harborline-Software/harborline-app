import { useEffect, useState } from 'react'
import { AuthorizationTrace } from '../../authorization/AuthorizationTrace'
import { ErrorCard, LoadingState } from '@harborline-software/ui-react'
import { AuthorizationAdminError } from './client'
import type { AuthorizationCapabilityDefinition, RoleDefinition, StandingDefinition } from './client'
import { AuditorAccessReviewPanel } from './AuditorAccessReviewPanel'
import { CapabilityBindingEditor } from './CapabilityBindingEditor'
import { StandingCataloguePanel } from './StandingCataloguePanel'
import { useAuthorizationAdminClient } from './AuthorizationAdminClientContext'

export function AuthorizationAdminPage() {
  const client = useAuthorizationAdminClient()
  const [roleDefinitions, setRoleDefinitions] = useState<readonly RoleDefinition[] | null>(null)
  const [definitions, setDefinitions] = useState<readonly AuthorizationCapabilityDefinition[] | null>(null)
  const [standings, setStandings] = useState<readonly StandingDefinition[] | null>(null)
  const [refusalId, setRefusalId] = useState<string>()
  const [loadError, setLoadError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const abort = new AbortController()
    setRoleDefinitions(null)
    setDefinitions(null)
    setStandings(null)
    setLoadError(null)
    setRefusalId(undefined)
    void (async () => {
      try {
        const [roles, catalogue, standingCatalogue] = await Promise.all([
          client.listRoleVocabulary(abort.signal),
          client.listCapabilityDefinitions(abort.signal),
          client.listStandingCatalogue(abort.signal),
        ])
        const refreshed = await Promise.all(catalogue.map(async definition => ({
          ...definition,
          binding: await client.getEffectiveBinding(definition.definitionId, abort.signal),
        })))
        setRoleDefinitions(roles)
        setDefinitions(refreshed)
        setStandings(standingCatalogue)
      } catch (cause) {
        if (!abort.signal.aborted) setRefusalId(cause instanceof AuthorizationAdminError ? cause.auditId : undefined)
        if (!abort.signal.aborted) setLoadError(cause instanceof Error ? cause.message : 'The authorization catalogue could not be loaded.')
      }
    })()
    return () => abort.abort()
  }, [attempt, client])

  if (loadError) return (
    <>
      <h1>Settings › System</h1>
      <ErrorCard title="Unable to load authorization settings" message={loadError} />
      <AuthorizationTrace key={refusalId} decision={refusalId ? { auditId: refusalId, read: client.readTrace } : undefined} />
      <button type="button" onClick={() => setAttempt(value => value + 1)}>Retry</button>
    </>
  )
  if (roleDefinitions === null || definitions === null || standings === null) return (
    <><h1>Settings › System</h1><LoadingState label="Loading authorization settings" /></>
  )

  return (
    <div style={{ display: 'grid', gap: '2rem', minWidth: 0 }}>
      <section aria-labelledby="capability-bindings-heading">
        <h1>Settings › System</h1>
        <h2 id="capability-bindings-heading">Authorization capability bindings</h2>
        {definitions.length === 0 ? <p>No authorization capability definitions are available.</p> : definitions.map(definition => (
          <CapabilityBindingEditor
            key={definition.definitionId}
            definition={definition}
            roleDefinitions={roleDefinitions}
            onNarrow={client.narrowCapabilityBinding}
            readTrace={client.readTrace}
            onSaved={result => setDefinitions(current => current?.map(row => row.definitionId === result.definitionId
              ? { ...row, binding: { revision: result.revision, effectiveRoles: result.effectiveRoles, warning: result.warning } }
              : row) ?? null)}
          />
        ))}
      </section>
      <AuditorAccessReviewPanel definitions={definitions} />
      <StandingCataloguePanel standings={standings} />
    </div>
  )
}
