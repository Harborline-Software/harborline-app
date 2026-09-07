import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { createFixtureDataExchangeAdminClient } from '../client/fixtureClient'
import { DataExchangeAdminClientProvider } from '../DataExchangeAdminClientContext'
import { DataExchangeAdminPage } from '../DataExchangeAdminPage'

describe('DataExchangeAdminPage detail', () => {
  it('renders the Data Exchange h1 with h2 detail-section headings', async () => {
    const user = userEvent.setup()
    render(
      <DataExchangeAdminClientProvider client={createFixtureDataExchangeAdminClient()}>
        <DataExchangeAdminPage panelRailCapable />
      </DataExchangeAdminClientProvider>,
    )

    await user.click(await screen.findByRole('button', { name: 'bank-feed-import' }))
    const panel = await screen.findByRole('complementary', { name: 'Data exchange definition bank-feed-import' })
    expect(screen.getByRole('heading', { level: 1, name: 'Data Exchange' })).toBeInTheDocument()
    expect(within(panel).getByRole('heading', { level: 2, name: 'Settings' })).toBeInTheDocument()
    expect(within(panel).getByRole('heading', { level: 2, name: 'Provenance' })).toBeInTheDocument()
    expect(within(panel).getByText('2.1.0', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText('import', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText('Tenant', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText('1', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText(/"mapping": "csv-standard"/, { selector: 'pre' })).toBeInTheDocument()
    expect(within(panel).getByText(/"originPackKey": "harborline.core-exchange"/, { selector: 'pre' })).toBeInTheDocument()
    expect(within(panel).getByText('Ordered by semver')).toBeInTheDocument()
    const history = await within(panel).findByRole('table', { name: 'Version history' })
    expect(within(history).getAllByRole('row')).toHaveLength(4)
    expect(within(history).getByText('2.0.0')).toBeInTheDocument()
    expect(within(history).getByText('1.9.0')).toBeInTheDocument()
    expect(within(panel).queryByRole('button', { name: /Restore/ })).not.toBeInTheDocument()
  })

  it('shows ordinal history in fixture order', async () => {
    const user = userEvent.setup()
    render(
      <DataExchangeAdminClientProvider client={createFixtureDataExchangeAdminClient()}>
        <DataExchangeAdminPage panelRailCapable />
      </DataExchangeAdminClientProvider>,
    )

    await user.click(await screen.findByRole('button', { name: 'legacy-ledger-export' }))
    const panel = await screen.findByRole('complementary', { name: 'Data exchange definition legacy-ledger-export' })
    expect(within(panel).getByText('Ordered ordinally')).toBeInTheDocument()
    const rows = within(await within(panel).findByRole('table', { name: 'Version history' })).getAllByRole('row')
    expect(rows[1]).toHaveTextContent('2024-legacy')
    expect(rows[2]).toHaveTextContent('0.3.0')
  })
})
