import type { AuthorizationCapabilityDefinition, RoleReference } from './client'

// Ticket 217: the Auditor's single capability is DERIVED from the catalogue the api serves
// (one binding view per definition, publisher ceiling plus the tenant's effective roles), never
// from a compiled permission composition. Filtering the catalogue by effective roles is the whole
// display; when the catalogue does not yield exactly one row the surface says so instead of
// hiding the disagreement.
export const AUDITOR_ROLE: RoleReference = { vocabulary: 'sys.platform-roles', name: 'auditor' }

const isAuditor = (role: RoleReference) =>
  role.vocabulary === AUDITOR_ROLE.vocabulary && role.name === AUDITOR_ROLE.name

const roleList = (roles: readonly RoleReference[]) =>
  roles.length === 0 ? 'none' : roles.map(role => `${role.vocabulary}:${role.name}`).join(', ')

export function auditorCapabilities(
  definitions: readonly AuthorizationCapabilityDefinition[],
): readonly AuthorizationCapabilityDefinition[] {
  return definitions.filter(definition => definition.binding.effectiveRoles.some(isAuditor))
}

export function AuditorAccessReviewPanel(
  { definitions }: { readonly definitions: readonly AuthorizationCapabilityDefinition[] },
) {
  const held = auditorCapabilities(definitions)

  return (
    <section aria-labelledby="auditor-access-review-heading">
      <h2 id="auditor-access-review-heading">Access review · Auditor</h2>
      {held.length === 1 ? null : (
        <p role="alert">
          {`Access review: the Auditor should hold exactly one authorization capability, but the catalogue derives ${held.length}. Review the capability bindings below.`}
        </p>
      )}
      {held.length === 0 ? <p>The catalogue derives no capability for the Auditor.</p> : held.map(definition => (
        <article key={definition.definitionId} aria-labelledby={`auditor-capability-${definition.definitionId}`} style={{ minWidth: 0 }}>
          <h3 id={`auditor-capability-${definition.definitionId}`} style={{ overflowWrap: 'anywhere' }}>{definition.atom.operation}</h3>
          <dl>
            <dt>Operation</dt>
            <dd style={{ overflowWrap: 'anywhere' }}>{`${definition.atom.operation} · ${definition.atom.scopeType}:${definition.atom.scopeValue}`}</dd>
            <dt>Publisher ceiling</dt>
            <dd style={{ overflowWrap: 'anywhere' }}>{`${definition.publisherPackageId} offers ${roleList(definition.offeredRoles)}`}</dd>
            <dt>Effective roles</dt>
            <dd style={{ overflowWrap: 'anywhere' }}>{roleList(definition.binding.effectiveRoles)}</dd>
            <dt>Binding revision</dt>
            <dd>{definition.binding.revision}</dd>
          </dl>
        </article>
      ))}
    </section>
  )
}
