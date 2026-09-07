import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { createFixtureFormsAdminClient } from '../client/fixtureClient'
import type { FormsAdminClient } from '../client/types'
import { FormsAdminClientProvider } from '../FormsAdminClientContext'
import { FormsAdminPage } from '../FormsAdminPage'

function createSpyClient() {
  const fixture = createFixtureFormsAdminClient()
  return {
    listDefinitions: vi.fn((signal?: AbortSignal) => fixture.listDefinitions(signal)),
    listVersions: vi.fn((formId: string, signal?: AbortSignal) => fixture.listVersions(formId, signal)),
    restoreVersion: vi.fn((formId: string, version: string, signal?: AbortSignal) => fixture.restoreVersion(formId, version, signal)),
  } satisfies FormsAdminClient
}

async function openIncidentHistory(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('button', { name: 'incident-intake' }))
  const panel = await screen.findByRole('complementary', { name: 'Form definition incident-intake' })
  await within(panel).findByRole('table', { name: 'Version history' })
  return panel
}

describe('FormsAdminPage restore', () => {
  it('restores the picked revision once, refreshes history, and reports the minted draft', async () => {
    const user = userEvent.setup()
    const client = createSpyClient()
    render(
      <FormsAdminClientProvider client={client}>
        <FormsAdminPage panelRailCapable />
      </FormsAdminClientProvider>,
    )
    const panel = await openIncidentHistory(user)
    const sourceRow = within(panel).getByText('1.0.1').closest('tr')
    expect(sourceRow).not.toBeNull()
    await user.click(within(sourceRow!).getByRole('button', { name: 'Restore…' }))
    const dialog = await screen.findByRole('dialog', { name: 'Restore 1.0.1?' })
    await user.click(within(dialog).getByRole('button', { name: 'Restore as draft' }))

    expect(await within(panel).findByTestId('restore-status')).toHaveTextContent('Draft 1.0.4 created from 1.0.1.')
    expect(client.restoreVersion).toHaveBeenCalledTimes(1)
    expect(client.restoreVersion).toHaveBeenCalledWith('incident-intake', '1.0.1')
    expect(client.listVersions).toHaveBeenCalledTimes(2)
    expect(within(panel).getByText('1.0.4')).toBeInTheDocument()
  })

  it('does not restore when confirmation is cancelled', async () => {
    const user = userEvent.setup()
    const client = createSpyClient()
    render(
      <FormsAdminClientProvider client={client}>
        <FormsAdminPage panelRailCapable />
      </FormsAdminClientProvider>,
    )
    const panel = await openIncidentHistory(user)
    const sourceRow = within(panel).getByText('1.0.1').closest('tr')
    await user.click(within(sourceRow!).getByRole('button', { name: 'Restore…' }))
    const dialog = await screen.findByRole('dialog', { name: 'Restore 1.0.1?' })
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    expect(client.restoreVersion).not.toHaveBeenCalled()
  })
})
