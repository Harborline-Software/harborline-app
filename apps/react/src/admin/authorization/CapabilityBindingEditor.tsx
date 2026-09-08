import { useEffect, useMemo, useState } from 'react'
import { ConfirmDialog } from '@harborline-software/ui-react'
import { AuthorizationTrace, type RecordedDecision } from '../../authorization/AuthorizationTrace'
import { AuthorizationAdminError } from './client'
import type { AuthorizationCapabilityDefinition, NarrowAuthorizationBindingResult, RoleDefinition, RoleReference } from './client'

const roleKey = (role: RoleReference) => `${role.vocabulary}\u0000${role.name}`
const qualifiedName = (role: RoleReference) => `${role.vocabulary}/${role.name}`
const CONFIRMATION = 'Removed roles, including the last role, cannot be restored through this definition revision. Continue with this narrower binding?'
const REASON = 'Narrowed in the Harborline Authorization editor.'

export interface CapabilityBindingEditorProps {
  readonly decisionTrace?: RecordedDecision
  readonly readTrace?: RecordedDecision['read']
  readonly definition: AuthorizationCapabilityDefinition
  readonly roleDefinitions: readonly RoleDefinition[]
  readonly onNarrow: (definitionId: string, selectedRoles: readonly RoleReference[], reason: string) => Promise<NarrowAuthorizationBindingResult>
  readonly onSaved: (result: NarrowAuthorizationBindingResult) => void
}

export function CapabilityBindingEditor({ definition, roleDefinitions, onNarrow, onSaved, decisionTrace, readTrace }: CapabilityBindingEditorProps) {
  const effectiveKeys = useMemo(() => new Set(definition.binding.effectiveRoles.map(roleKey)), [definition.binding.effectiveRoles])
  const [selectedKeys, setSelectedKeys] = useState(() => new Set(effectiveKeys))
  const [pendingRoles, setPendingRoles] = useState<readonly RoleReference[] | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [refusalId, setRefusalId] = useState<string>()
  const trace = refusalId && readTrace ? { auditId: refusalId, read: readTrace } : decisionTrace
  const [error, setError] = useState<string | null>(null)

  useEffect(() => setSelectedKeys(new Set(effectiveKeys)), [effectiveKeys])

  const selectedRoles = definition.binding.effectiveRoles.filter(role => selectedKeys.has(roleKey(role)))
  const unchanged = selectedRoles.length === definition.binding.effectiveRoles.length
  const title = `${definition.atom.operation} · ${definition.atom.scopeType}:${definition.atom.scopeValue}`

  function roleLabel(role: RoleReference): string {
    const match = roleDefinitions.find(candidate => roleKey(candidate.role) === roleKey(role))
    return match ? `${match.displayName} (${qualifiedName(role)})` : qualifiedName(role)
  }

  async function confirmNarrowing() {
    const roles = pendingRoles
    if (roles === null) return
    setPendingRoles(null)
    setSubmitting(true)
    setError(null)
    setRefusalId(undefined)
    try {
      onSaved(await onNarrow(definition.definitionId, roles, REASON))
    } catch (cause) {
      setRefusalId(cause instanceof AuthorizationAdminError ? cause.auditId : undefined)
      setError(cause instanceof AuthorizationAdminError ? cause.message : 'The authorization service could not complete the request. Try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <article aria-labelledby={`capability-${definition.definitionId}`} style={{ minWidth: 0 }}>
      <h3 id={`capability-${definition.definitionId}`} style={{ overflowWrap: 'anywhere' }}>{title}</h3>
      <dl>
        <dt>Operation</dt><dd style={{ overflowWrap: 'anywhere' }}>{definition.atom.operation}</dd>
        <dt>Scoped atom</dt><dd style={{ overflowWrap: 'anywhere' }}>{definition.atom.scopeType}:{definition.atom.scopeValue}</dd>
        <dt>Publisher-offered roles</dt><dd style={{ overflowWrap: 'anywhere' }}>{definition.offeredRoles.map(roleLabel).join(', ') || 'None'}</dd>
        <dt>Effective roles</dt><dd style={{ overflowWrap: 'anywhere' }}>{definition.binding.effectiveRoles.map(roleLabel).join(', ') || 'None'}</dd>
        <dt>Source</dt><dd style={{ overflowWrap: 'anywhere' }}>{definition.publisherPackageId} · definition revision {definition.definitionRevision}</dd>
      </dl>
      {definition.binding.warning === 'EmptyBinding' && (
        <p role="alert">Binding saved with warning: no roles are effective for this capability.</p>
      )}
      <p>Definition {definition.definitionId}; binding revision {definition.binding.revision}.</p>
      <p>Publisher-offered roles are shown below. Only roles still effective in this binding can be selected.</p>
      <fieldset disabled={submitting}>
        <legend>Effective roles</legend>
        {definition.offeredRoles.map(role => {
          const key = roleKey(role)
          const effective = effectiveKeys.has(key)
          return (
            <div key={key}>
              <label>
                <input
                  type="checkbox"
                  checked={effective && selectedKeys.has(key)}
                  disabled={!effective || submitting}
                  onChange={event => setSelectedKeys(current => {
                    const next = new Set(current)
                    if (event.target.checked) next.add(key); else next.delete(key)
                    return next
                  })}
                />{' '}{roleLabel(role)}
              </label>
              {!effective && <span> — Removed; history only</span>}
            </div>
          )
        })}
      </fieldset>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', marginBlockStart: '1rem' }}>
        <button type="button" disabled={submitting || unchanged} onClick={() => setPendingRoles(selectedRoles)}>Save narrower binding…</button>
        <button type="button" disabled={submitting || definition.binding.effectiveRoles.length === 0} onClick={() => setPendingRoles([])}>Clear all roles…</button>
      </div>
      {submitting && <p aria-live="polite">Saving narrower binding…</p>}
      {error && <p role="alert">{error}</p>}
      <AuthorizationTrace key={trace?.auditId ?? 'unlinked'} decision={trace} />
      <ConfirmDialog
        open={pendingRoles !== null}
        onOpenChange={open => { if (!open) setPendingRoles(null) }}
        title={pendingRoles?.length === 0 ? 'Clear all roles?' : 'Narrow capability roles?'}
        description={CONFIRMATION}
        confirmLabel={pendingRoles?.length === 0 ? 'Clear all roles' : 'Save narrower binding'}
        variant="destructive"
        onConfirm={() => { void confirmNarrowing() }}
      />
    </article>
  )
}
