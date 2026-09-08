import { AuthorizationAdminError } from './types'
import type {
  AuthorizationAdminClient,
  AuthorizationBinding,
  AuthorizationCapabilityDefinition,
  NarrowAuthorizationBindingResult,
  RoleDefinition,
  RoleReference,
  StandingDefinition,
} from './types'

export const ROLE_DEFINITIONS: readonly RoleDefinition[] = [
  { roleDefinitionId: '11111111-1111-1111-1111-111111111111', role: { vocabulary: 'sys.platform-roles', name: 'administrator' }, displayName: 'Administrator', owner: { kind: 'Platform', ownerId: 'harborline-platform' }, isSealed: true },
  { roleDefinitionId: '22222222-2222-2222-2222-222222222222', role: { vocabulary: 'sys.platform-roles', name: 'auditor' }, displayName: 'Auditor', owner: { kind: 'Platform', ownerId: 'harborline-platform' }, isSealed: true },
  { roleDefinitionId: '44444444-4444-4444-4444-444444444444', role: { vocabulary: 'sys.platform-roles', name: 'node-operator' }, displayName: 'Node operator', owner: { kind: 'Platform', ownerId: 'harborline-platform' }, isSealed: true },
  { roleDefinitionId: '33333333-3333-3333-3333-333333333333', role: { vocabulary: 'tax.roles', name: 'author' }, displayName: 'Tax author', owner: { kind: 'Package', ownerId: 'harborline.tax' }, isSealed: false },
]

export const CAPABILITY_DEFINITIONS: readonly AuthorizationCapabilityDefinition[] = [
  {
    definitionId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', publisherPackageId: 'harborline.tax', definitionRevision: 3,
    atom: { operation: 'tax.return.write', scopeType: 'Tenant', scopeValue: '*' },
    offeredRoles: [{ vocabulary: 'sys.platform-roles', name: 'administrator' }, { vocabulary: 'tax.roles', name: 'author' }],
    binding: { revision: 2, effectiveRoles: [{ vocabulary: 'sys.platform-roles', name: 'administrator' }, { vocabulary: 'tax.roles', name: 'author' }], warning: null },
  },
  {
    definitionId: '12121212-1212-1212-1212-121212121212', publisherPackageId: 'harborline.access-grant', definitionRevision: 1,
    atom: { operation: 'audit:read', scopeType: 'Tenant', scopeValue: '*' },
    offeredRoles: [{ vocabulary: 'sys.platform-roles', name: 'auditor' }, { vocabulary: 'sys.platform-roles', name: 'node-operator' }],
    binding: { revision: 1, effectiveRoles: [{ vocabulary: 'sys.platform-roles', name: 'auditor' }, { vocabulary: 'sys.platform-roles', name: 'node-operator' }], warning: null },
  },
  {
    definitionId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', publisherPackageId: 'harborline.audit', definitionRevision: 1,
    atom: { operation: 'audit.export', scopeType: 'Tenant', scopeValue: '*' },
    offeredRoles: [{ vocabulary: 'sys.platform-roles', name: 'auditor' }],
    binding: { revision: 4, effectiveRoles: [], warning: 'EmptyBinding' },
  },
]

export const STANDING_DEFINITIONS: readonly StandingDefinition[] = [
  {
    ruleId: 'tax-return-standing', ruleVersion: '1.0.0', standing: 'Filed', declaredRecordType: 'tax.return',
    fields: [
      { field: 'returnId', carryingRecordTypes: ['tax.return', 'tax.return.amendment'] },
      { field: 'legacyReference', carryingRecordTypes: [] },
    ],
  },
]

const cloneRole = (role: RoleReference): RoleReference => ({ ...role })
const cloneBinding = (binding: AuthorizationBinding): AuthorizationBinding => ({
  revision: binding.revision,
  effectiveRoles: binding.effectiveRoles.map(cloneRole),
  warning: binding.warning,
})
const cloneDefinition = (definition: AuthorizationCapabilityDefinition): AuthorizationCapabilityDefinition => ({
  ...definition,
  atom: { ...definition.atom },
  offeredRoles: definition.offeredRoles.map(cloneRole),
  binding: cloneBinding(definition.binding),
})

export function createFixtureAuthorizationAdminClient(): AuthorizationAdminClient {
  const definitions = CAPABILITY_DEFINITIONS.map(cloneDefinition)
  return {
    async listHolders() { throw new Error('Holders require a configured authorization service.') },

    async listRoleVocabulary() {
      return ROLE_DEFINITIONS.map(definition => ({ ...definition, role: cloneRole(definition.role), owner: { ...definition.owner } }))
    },
    async listCapabilityDefinitions() {
      return definitions.map(cloneDefinition)
    },
    async getEffectiveBinding(definitionId) {
      const definition = definitions.find(row => row.definitionId === definitionId)
      if (!definition) throw new AuthorizationAdminError(404, 'authorization.definition_not_found')
      return cloneBinding(definition.binding)
    },
    async narrowCapabilityBinding(definitionId, selectedRoles, reason) {
      const index = definitions.findIndex(row => row.definitionId === definitionId)
      if (index < 0) throw new AuthorizationAdminError(404, 'authorization.definition_not_found')
      const current = definitions[index]
      const effective = new Set(current.binding.effectiveRoles.map(role => `${role.vocabulary}\u0000${role.name}`))
      if (selectedRoles.some(role => !effective.has(`${role.vocabulary}\u0000${role.name}`)))
        throw new AuthorizationAdminError(409, 'authorization.binding_widening_refused')
      const binding: AuthorizationBinding = {
        revision: current.binding.revision + 1,
        effectiveRoles: selectedRoles.map(cloneRole),
        warning: selectedRoles.length === 0 ? 'EmptyBinding' : null,
      }
      definitions[index] = { ...current, binding }
      return {
        definitionId,
        ...cloneBinding(binding),
        changedBy: 'fixture-admin',
        changedAt: '2026-09-02T12:00:00Z',
        reason,
      } satisfies NarrowAuthorizationBindingResult
    },
    async listStandingCatalogue() {
      return STANDING_DEFINITIONS.map(row => ({ ...row, fields: row.fields.map(field => ({ ...field, carryingRecordTypes: [...field.carryingRecordTypes] })) }))
    },
  }
}
