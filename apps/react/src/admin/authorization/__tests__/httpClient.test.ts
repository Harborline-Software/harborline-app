import { describe, expect, it, vi } from 'vitest'
import { createHttpAuthorizationAdminClient } from '../client/httpClient'

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), {
  status,
  headers: { 'Content-Type': 'application/json' },
})

describe('HTTP authorization admin client', () => {
  it('uses the exact routes and preserves qualified roles, names, EmptyBinding, and standing rows', async () => {
    const responses = [
      json([{ roleDefinitionId: '11111111-1111-1111-1111-111111111111', role: { vocabulary: 'tax.roles', name: 'author' }, displayName: 'Tax author', owner: { kind: 'Package', ownerId: 'harborline.tax' }, isSealed: false }]),
      json([{ definitionId: 'a/b', publisherPackageId: 'harborline.tax', definitionRevision: 3, atom: { operation: 'tax.write', scopeType: 'Tenant', scopeValue: '*' }, offeredRoles: [{ vocabulary: 'tax.roles', name: 'author' }], binding: { revision: 4, effectiveRoles: [], warning: 'EmptyBinding' } }]),
      json({ revision: 4, effectiveRoles: [], warning: 'EmptyBinding' }),
      json([{ ruleId: 'standing-rule', ruleVersion: '1', standing: 'Filed', declaredRecordType: 'tax.return', fields: [{ field: 'returnId', carryingRecordTypes: ['tax.return', 'tax.amendment'] }] }]),
      json({ definitionId: 'a/b', revision: 5, effectiveRoles: [], warning: 'EmptyBinding', changedBy: 'admin', changedAt: '2026-09-02T12:00:00Z', reason: 'clear' }),
    ]
    const fetchImpl = vi.fn<typeof fetch>(async () => responses.shift()!)
    const client = createHttpAuthorizationAdminClient({ baseUrl: 'https://node.test', fetchImpl, createIdempotencyKey: () => 'request-key' })

    const roles = await client.listRoleVocabulary()
    const definitions = await client.listCapabilityDefinitions()
    const binding = await client.getEffectiveBinding('a/b')
    const standings = await client.listStandingCatalogue()
    const narrowed = await client.narrowCapabilityBinding('a/b', [], 'clear')

    expect(roles[0]).toMatchObject({ role: { vocabulary: 'tax.roles', name: 'author' }, displayName: 'Tax author' })
    expect(definitions[0].binding.warning).toBe('EmptyBinding')
    expect(binding).toEqual({ revision: 4, effectiveRoles: [], warning: 'EmptyBinding' })
    expect(standings[0].fields[0].carryingRecordTypes).toEqual(['tax.return', 'tax.amendment'])
    expect(narrowed.warning).toBe('EmptyBinding')
    expect(fetchImpl.mock.calls.map(call => String(call[0]))).toEqual([
      'https://node.test/api/local-node/authorization/role-vocabulary',
      'https://node.test/api/local-node/authorization/capability-definitions',
      'https://node.test/api/local-node/authorization/capability-definitions/a%2Fb/binding',
      'https://node.test/api/local-node/authorization/standing-catalogue',
      'https://node.test/api/local-node/authorization/capability-definitions/a%2Fb/binding',
    ])
    expect(fetchImpl.mock.calls[4][1]).toMatchObject({ method: 'POST', body: JSON.stringify({ selectedRoles: [], reason: 'clear' }) })
    expect(new Headers(fetchImpl.mock.calls[4][1]?.headers).get('Idempotency-Key')).toBe('request-key')
  })

  it.each([
    [400, 'authorization.binding_invalid', 'selected role binding is invalid'],
    [404, 'authorization.definition_not_found', 'no longer available'],
    [403, 'authorization.permission_required', 'do not have permission'],
    [409, 'authorization.idempotency_key_reused', 'conflicts with an earlier request'],
  ])('maps HTTP %s machine code %s to lane-owned copy', async (status, code, copy) => {
    const fetchImpl = vi.fn<typeof fetch>(async () => json({ code, detail: 'SERVER PROSE MUST NOT LEAK' }, status))
    const client = createHttpAuthorizationAdminClient({ fetchImpl })

    const error = await client.getEffectiveBinding('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa').catch(cause => cause)

    expect(error).toMatchObject({ status, code })
    expect(error.message).toContain(copy)
    expect(error.message).not.toContain('SERVER PROSE')
  })

  it('posts a crafted unoffered role and maps widening refusal to lane-owned copy', async () => {
    const fetchImpl = vi.fn<typeof fetch>(async () => json({
      code: 'authorization.binding_widening_refused',
      detail: 'SERVER PROSE MUST NOT LEAK',
    }, 409))
    const client = createHttpAuthorizationAdminClient({ fetchImpl, createIdempotencyKey: () => 'crafted-request' })
    const role = { vocabulary: 'tax.roles' as const, name: 'reviewer' }

    const error = await client.narrowCapabilityBinding('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', [role], 'crafted widening').catch(cause => cause)

    expect(fetchImpl).toHaveBeenCalledOnce()
    expect(fetchImpl.mock.calls[0][1]).toMatchObject({ method: 'POST', body: JSON.stringify({ selectedRoles: [role], reason: 'crafted widening' }) })
    expect(error).toMatchObject({ status: 409, code: 'authorization.binding_widening_refused' })
    expect(error.message).toBe('A removed role cannot be restored through this definition revision.')
    expect(error.message).not.toContain('SERVER PROSE')
  })
})
