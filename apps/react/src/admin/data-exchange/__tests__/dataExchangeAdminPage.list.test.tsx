import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { createFixtureDataExchangeAdminClient } from '../client/fixtureClient'
import { DataExchangeAdminClientProvider } from '../DataExchangeAdminClientContext'
import { DataExchangeAdminPage } from '../DataExchangeAdminPage'

describe('DataExchangeAdminPage list', () => {
  it('renders all deterministic definition rows', async () => {
    render(
      <DataExchangeAdminClientProvider client={createFixtureDataExchangeAdminClient()}>
        <DataExchangeAdminPage panelRailCapable />
      </DataExchangeAdminClientProvider>,
    )

    // The platform DataGrid renders a div-based ARIA grid (role="grid"/"row"/"gridcell"),
    // not a native table — query by grid semantics.
    const grid = await screen.findByRole('grid', { name: 'Data exchange definitions' })
    const trialBalanceRow = within(grid).getByRole('button', { name: 'bank-feed-import' }).closest<HTMLElement>('[role="row"]')
    expect(trialBalanceRow).not.toBeNull()
    expect(within(trialBalanceRow!).getByText('Bank feed import')).toBeInTheDocument()
    expect(within(trialBalanceRow!).getByText('2.1.0')).toBeInTheDocument()
    expect(within(trialBalanceRow!).getByText('import')).toBeInTheDocument()
    expect(within(trialBalanceRow!).getByText('Tenant')).toBeInTheDocument()
    expect(within(grid).getAllByRole('button')).toHaveLength(3)
    const legacyLedgerExportRow = within(grid).getByRole('button', { name: 'legacy-ledger-export' }).closest<HTMLElement>('[role="row"]')
    expect(legacyLedgerExportRow).not.toBeNull()
    expect(within(legacyLedgerExportRow!).getByText('export')).toBeInTheDocument()
  })
})
