import type { RoleDefinition as PlatformRoleDefinition, RoleReference as PlatformRoleReference } from '@harborline-software/contracts/authorization'

export type RoleReference = PlatformRoleReference
export type RoleDefinition = PlatformRoleDefinition

export interface PermissionAtom {
  readonly operation: string
  readonly scopeType: string
  readonly scopeValue: string
}

export type BindingWarningCode = 'EmptyBinding'

export interface AuthorizationBinding {
  readonly revision: number
  readonly effectiveRoles: readonly RoleReference[]
  readonly warning: BindingWarningCode | null
}

export interface AuthorizationCapabilityDefinition {
  readonly definitionId: string
  readonly publisherPackageId: string
  readonly definitionRevision: number
  readonly atom: PermissionAtom
  readonly offeredRoles: readonly RoleReference[]
  readonly binding: AuthorizationBinding
}

export interface NarrowAuthorizationBindingResult extends AuthorizationBinding {
  readonly definitionId: string
  readonly changedBy: string
  readonly changedAt: string
  readonly reason: string
}

export interface StandingFieldRecordTypes {
  readonly field: string
  readonly carryingRecordTypes: readonly string[]
}

export interface StandingDefinition {
  readonly ruleId: string
  readonly ruleVersion: string
  readonly standing: string
  readonly declaredRecordType: string
  readonly fields: readonly StandingFieldRecordTypes[]
}

export type AuthorizationAdminErrorCode =
  | 'authorization.binding_invalid'
  | 'authorization.binding_stale'
  | 'authorization.binding_widening_refused'
  | 'authorization.definition_not_found'
  | 'authorization.idempotency_key_required'
  | 'authorization.idempotency_key_reused'
  | 'authorization.permission_required'

const ERROR_COPY: Readonly<Record<AuthorizationAdminErrorCode, string>> = {
  'authorization.binding_invalid': 'The selected role binding is invalid. Review the selection and try again.',
  'authorization.binding_stale': 'This binding changed while you were editing it. Reload and try again.',
  'authorization.binding_widening_refused': 'A removed role cannot be restored through this definition revision.',
  'authorization.definition_not_found': 'This authorization definition is no longer available. Reload the catalogue.',
  'authorization.idempotency_key_required': 'The binding request could not be safely identified. Try again.',
  'authorization.idempotency_key_reused': 'This binding request conflicts with an earlier request. Try again.',
  'authorization.permission_required': 'You do not have permission to administer authorization settings.',
}

export class AuthorizationAdminError extends Error {
  constructor(public readonly status: number, public readonly code: string) {
    super(ERROR_COPY[code as AuthorizationAdminErrorCode] ?? 'The authorization service could not complete the request. Try again.')
    this.name = 'AuthorizationAdminError'
  }
}

export interface AuthorizationAdminClient {
  listRoleVocabulary(signal?: AbortSignal): Promise<readonly RoleDefinition[]>
  listCapabilityDefinitions(signal?: AbortSignal): Promise<readonly AuthorizationCapabilityDefinition[]>
  getEffectiveBinding(definitionId: string, signal?: AbortSignal): Promise<AuthorizationBinding>
  narrowCapabilityBinding(definitionId: string, selectedRoles: readonly RoleReference[], reason: string, signal?: AbortSignal): Promise<NarrowAuthorizationBindingResult>
  listStandingCatalogue(signal?: AbortSignal): Promise<readonly StandingDefinition[]>
}
