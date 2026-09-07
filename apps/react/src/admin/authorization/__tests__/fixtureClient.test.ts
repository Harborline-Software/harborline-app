import { describe, expect, it } from 'vitest'
import { createFixtureAuthorizationAdminClient } from '../client/fixtureClient'

describe('fixture authorization admin client', () => {
  it('retains explicitly empty bindings through definition and binding reloads', async () => {
    const client = createFixtureAuthorizationAdminClient()
    const definitionId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'

    await client.narrowCapabilityBinding(definitionId, [], 'clear')

    expect((await client.listCapabilityDefinitions()).find(row => row.definitionId === definitionId)?.binding)
      .toEqual({ revision: 3, effectiveRoles: [], warning: 'EmptyBinding' })
    expect(await client.getEffectiveBinding(definitionId))
      .toEqual({ revision: 3, effectiveRoles: [], warning: 'EmptyBinding' })
  })

  it('posts and persists only a strict subset of qualified role references', async () => {
    const client = createFixtureAuthorizationAdminClient()
    const definitionId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    const selected = [{ vocabulary: 'tax.roles' as const, name: 'author' }]

    const result = await client.narrowCapabilityBinding(definitionId, selected, 'least privilege')

    expect(result.effectiveRoles).toEqual(selected)
    expect((await client.getEffectiveBinding(definitionId)).effectiveRoles).toEqual(selected)
  })

  it('refuses to re-add a removed role', async () => {
    const client = createFixtureAuthorizationAdminClient()
    const definitionId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    const author = { vocabulary: 'tax.roles' as const, name: 'author' }
    await client.narrowCapabilityBinding(definitionId, [author], 'remove administrator')

    await expect(client.narrowCapabilityBinding(definitionId, [author, { vocabulary: 'sys.platform-roles', name: 'administrator' }], 'widen'))
      .rejects.toMatchObject({ status: 409, code: 'authorization.binding_widening_refused' })
  })
})
