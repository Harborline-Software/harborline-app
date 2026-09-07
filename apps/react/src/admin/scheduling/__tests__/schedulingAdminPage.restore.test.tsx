import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { createFixtureSchedulingAdminClient } from '../client/fixtureClient'
import type { SchedulingAdminClient } from '../client/types'
import { SchedulingAdminClientProvider } from '../SchedulingAdminClientContext'
import { SchedulingAdminPage } from '../SchedulingAdminPage'

function createSpyClient() {
  const fixture = createFixtureSchedulingAdminClient()
  return {
    listDefinitions: vi.fn((signal?: AbortSignal) => fixture.listDefinitions(signal)),
    getDefinition: vi.fn((definitionId: string, signal?: AbortSignal) => fixture.getDefinition(definitionId, signal)),
    listVersions: vi.fn((definitionId: string, signal?: AbortSignal) => fixture.listVersions(definitionId, signal)),
    restoreRevision: vi.fn((definitionId: string, revision: number, signal?: AbortSignal) => fixture.restoreRevision(definitionId, revision, signal)),
  } satisfies SchedulingAdminClient
}

async function openInspectionHistory(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('button', { name: 'inspection-protocol' }))
  const panel = await screen.findByRole('complementary', { name: 'Scheduling definition inspection-protocol' })
  await within(panel).findByRole('table', { name: 'Revision history' })
  return panel
}

describe('SchedulingAdminPage restore', () => {
  it('restores the picked revision once, refreshes history AND the definitions list, and moves the head', async () => {
    const user = userEvent.setup()
    const client = createSpyClient()
    render(
      <SchedulingAdminClientProvider client={client}>
        <SchedulingAdminPage panelRailCapable />
      </SchedulingAdminClientProvider>,
    )
    const panel = await openInspectionHistory(user)
    const historyTable = within(panel).getByRole('table', { name: 'Revision history' })
    const sourceRow = within(historyTable).getByText('2026-08-05 08:00 UTC').closest('tr')
    expect(sourceRow).not.toBeNull()
    await user.click(within(sourceRow!).getByRole('button', { name: 'Restore…' }))
    const dialog = await screen.findByRole('dialog', { name: 'Restore revision 1?' })
    // The Blazor lane pins this same sentence, so the two lanes cannot drift apart in copy.
    expect(dialog).toHaveTextContent(
      "Appends revision 1's content of 'inspection-protocol' as the new head revision, effective immediately. History is never modified.",
    )
    await user.click(within(dialog).getByRole('button', { name: 'Restore as head' }))

    expect(await within(panel).findByTestId('restore-status')).toHaveTextContent('Revision 4 restored from 1.')
    expect(client.restoreRevision).toHaveBeenCalledTimes(1)
    expect(client.restoreRevision).toHaveBeenCalledWith('inspection-protocol', 1)
    // Unlike Forms, restore moves the scheduling head, so the definitions list refreshes too.
    expect(client.listDefinitions).toHaveBeenCalledTimes(2)
    expect(client.listVersions).toHaveBeenCalledTimes(2)
    expect(within(historyTable).getByText('2026-08-20 00:00 UTC')).toBeInTheDocument()
    const grid = screen.getByRole('grid', { name: 'Scheduling definitions' })
    const inspectionRow = within(grid)
      .getByRole('button', { name: 'inspection-protocol' })
      .closest<HTMLElement>('[role="row"]')
    expect(inspectionRow).not.toBeNull()
    expect(within(inspectionRow!).getByText('4')).toBeInTheDocument()
  })

  it('does not restore when confirmation is cancelled', async () => {
    const user = userEvent.setup()
    const client = createSpyClient()
    render(
      <SchedulingAdminClientProvider client={client}>
        <SchedulingAdminPage panelRailCapable />
      </SchedulingAdminClientProvider>,
    )
    const panel = await openInspectionHistory(user)
    const historyTable = within(panel).getByRole('table', { name: 'Revision history' })
    const sourceRow = within(historyTable).getByText('2026-08-05 08:00 UTC').closest('tr')
    await user.click(within(sourceRow!).getByRole('button', { name: 'Restore…' }))
    const dialog = await screen.findByRole('dialog', { name: 'Restore revision 1?' })
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    expect(client.restoreRevision).not.toHaveBeenCalled()
  })
})
