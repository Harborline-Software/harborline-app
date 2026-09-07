import { describe, expect, it } from 'vitest'
import { createFixtureFormsAdminClient } from '../client/fixtureClient'
import { FormsAdminError } from '../client/types'

describe('createFixtureFormsAdminClient', () => {
  it('lists the deterministic definitions and restores after the maximum retained patch', async () => {
    const client = createFixtureFormsAdminClient()

    const definitions = await client.listDefinitions()
    expect(definitions).toHaveLength(3)
    expect(definitions.map(row => row.formId)).toEqual([
      'incident-intake',
      'vessel-registration',
      'crew-manifest',
    ])

    await expect(client.restoreVersion('incident-intake', '1.0.0')).resolves.toEqual({
      formId: 'incident-intake',
      version: '1.0.4',
    })
    const versions = await client.listVersions('incident-intake')
    expect(versions[0]).toMatchObject({
      formId: 'incident-intake',
      version: '1.0.4',
      status: 'Draft',
      derivedFrom: '1.0.0',
    })
  })

  it('rejects an unknown revision with a status-bearing error', async () => {
    const client = createFixtureFormsAdminClient()
    const rejection = client.restoreVersion('incident-intake', '9.9.9')

    await expect(rejection).rejects.toBeInstanceOf(FormsAdminError)
    await expect(rejection).rejects.toMatchObject({
      status: 404,
      message: "No revision '9.9.9' of form 'incident-intake' to restore.",
    })
  })
})
