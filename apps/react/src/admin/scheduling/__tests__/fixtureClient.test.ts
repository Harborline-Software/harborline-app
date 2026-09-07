import { describe, expect, it } from 'vitest'
import { createFixtureSchedulingAdminClient } from '../client/fixtureClient'
import { SchedulingAdminError } from '../client/types'

describe('createFixtureSchedulingAdminClient', () => {
  it('lists the deterministic definitions and reads the head view', async () => {
    const client = createFixtureSchedulingAdminClient()

    const definitions = await client.listDefinitions()
    expect(definitions).toHaveLength(3)
    expect(definitions.map(row => row.id)).toEqual([
      'inspection-protocol',
      'move-in-checklist',
      'turnover-schedule',
    ])
    expect(definitions.map(row => row.revision)).toEqual([3, 1, 2])
    await expect(client.getDefinition('inspection-protocol')).resolves.toEqual({
      id: 'inspection-protocol',
      revision: 3,
      definition: { title: 'Inspection protocol', cadence: 'weekly' },
      updatedAt: '2026-08-18T09:00:00Z',
      updatedBy: 'user:fixture-scheduler',
    })
  })

  it('restores by minting head + 1 and moving the list head', async () => {
    const client = createFixtureSchedulingAdminClient()

    await expect(client.restoreRevision('inspection-protocol', 1)).resolves.toEqual({
      definitionId: 'inspection-protocol',
      revision: 4,
      restoredFrom: 1,
    })
    const versions = await client.listVersions('inspection-protocol')
    expect(versions[0]).toEqual({
      id: 'inspection-protocol',
      revision: 4,
      title: 'Inspection protocol',
      updatedAt: '2026-08-20T00:00:00Z',
      updatedBy: 'user:fixture-operator',
    })
    expect(versions).toHaveLength(4)
    const definition = (await client.listDefinitions()).find(row => row.id === 'inspection-protocol')
    expect(definition).toMatchObject({ revision: 4, updatedBy: 'user:fixture-operator' })
    await expect(client.getDefinition('inspection-protocol')).resolves.toMatchObject({
      revision: 4,
      definition: { title: 'Inspection protocol', cadence: 'weekly' },
    })

    await expect(client.restoreRevision('inspection-protocol', 2)).resolves.toMatchObject({ revision: 5 })
    expect((await client.listVersions('inspection-protocol'))[0].updatedAt).toBe('2026-08-20T00:01:00Z')
  })

  it('rejects an unknown revision with a status-bearing error', async () => {
    const client = createFixtureSchedulingAdminClient()
    const revisionRejection = client.restoreRevision('inspection-protocol', 99)

    await expect(revisionRejection).rejects.toBeInstanceOf(SchedulingAdminError)
    await expect(revisionRejection).rejects.toMatchObject({
      status: 404,
      message: 'scheduling.draft.revision_not_found',
    })
    // Restoring an unknown DEFINITION also answers revision_not_found — the real route looks for
    // the revision first and never gets far enough to tell the two apart. Verified against a live
    // node 2026-08-22, and the Blazor lane pins the same pair.
    await expect(client.restoreRevision('nope', 1)).rejects.toMatchObject({
      status: 404,
      message: 'scheduling.draft.revision_not_found',
    })
    await expect(client.getDefinition('nope')).rejects.toMatchObject({
      status: 404,
      message: 'scheduling.draft.not_found',
    })
  })
})
