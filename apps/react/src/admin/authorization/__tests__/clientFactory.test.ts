import { afterEach, describe, expect, it, vi } from 'vitest'
import { createAuthorizationAdminClient } from '../client'

describe('authorization admin client factory', () => {
  afterEach(() => vi.unstubAllEnvs())

  it('fails closed when neither a live origin nor explicit fixture opt-in is configured', () => {
    vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', '')
    vi.stubEnv('VITE_AUTHORIZATION_FIXTURE', '')

    expect(() => createAuthorizationAdminClient()).toThrow(/VITE_AUTHORIZATION_API_ORIGIN|VITE_AUTHORIZATION_FIXTURE/)
  })

  it.each(['1', 'true'])('uses the fixture only for the explicit %s opt-in', async fixture => {
    vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', '')
    vi.stubEnv('VITE_AUTHORIZATION_FIXTURE', fixture)

    expect(await createAuthorizationAdminClient().listCapabilityDefinitions()).not.toHaveLength(0)
  })
})
