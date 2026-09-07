import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { createFixtureReportsAdminClient } from '../client/fixtureClient'
import { ReportsAdminClientProvider } from '../ReportsAdminClientContext'
import { ReportsAdminPage } from '../ReportsAdminPage'

describe('ReportsAdminPage list', () => {
  it('renders all deterministic definition rows', async () => {
    render(
      <ReportsAdminClientProvider client={createFixtureReportsAdminClient()}>
        <ReportsAdminPage panelRailCapable />
      </ReportsAdminClientProvider>,
    )

    // The platform DataGrid renders a div-based ARIA grid (role="grid"/"row"/"gridcell"),
    // not a native table — query by grid semantics.
    const grid = await screen.findByRole('grid', { name: 'Report definitions' })
    const trialBalanceRow = within(grid).getByRole('button', { name: 'trial-balance' }).closest<HTMLElement>('[role="row"]')
    expect(trialBalanceRow).not.toBeNull()
    expect(within(trialBalanceRow!).getByText('Trial balance')).toBeInTheDocument()
    expect(within(trialBalanceRow!).getByText('2.1.0')).toBeInTheDocument()
    expect(within(trialBalanceRow!).getByText('standard')).toBeInTheDocument()
    expect(within(trialBalanceRow!).getByText('Tenant')).toBeInTheDocument()
    expect(within(grid).getAllByRole('button')).toHaveLength(3)
    const occupancyRow = within(grid).getByRole('button', { name: 'occupancy' }).closest<HTMLElement>('[role="row"]')
    expect(occupancyRow).not.toBeNull()
    expect(within(occupancyRow!).getByText('snapshot')).toBeInTheDocument()
  })
})
