import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { createFixtureViewsAdminClient } from '../client/fixtureClient'
import { ViewsAdminClientProvider } from '../ViewsAdminClientContext'
import { ViewsAdminPage } from '../ViewsAdminPage'

describe('ViewsAdminPage list', () => {
  it('renders all deterministic definition rows', async () => {
    render(
      <ViewsAdminClientProvider client={createFixtureViewsAdminClient()}>
        <ViewsAdminPage panelRailCapable />
      </ViewsAdminClientProvider>,
    )

    // The platform DataGrid renders a div-based ARIA grid (role="grid"/"row"/"gridcell"),
    // not a native table — query by grid semantics.
    const grid = await screen.findByRole('grid', { name: 'View definitions' })
    const trialBalanceRow = within(grid).getByRole('button', { name: 'work-orders-table' }).closest<HTMLElement>('[role="row"]')
    expect(trialBalanceRow).not.toBeNull()
    expect(within(trialBalanceRow!).getByText('Work orders table')).toBeInTheDocument()
    expect(within(trialBalanceRow!).getByText('2.1.0')).toBeInTheDocument()
    expect(within(trialBalanceRow!).getByText('table')).toBeInTheDocument()
    expect(within(trialBalanceRow!).getByText('Tenant')).toBeInTheDocument()
    expect(within(grid).getAllByRole('button')).toHaveLength(3)
    const occupancyBoardRow = within(grid).getByRole('button', { name: 'occupancy-board' }).closest<HTMLElement>('[role="row"]')
    expect(occupancyBoardRow).not.toBeNull()
    expect(within(occupancyBoardRow!).getByText('board')).toBeInTheDocument()
  })
})
