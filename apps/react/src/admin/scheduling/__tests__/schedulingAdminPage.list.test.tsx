import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { createFixtureSchedulingAdminClient } from '../client/fixtureClient'
import { SchedulingAdminClientProvider } from '../SchedulingAdminClientContext'
import { SchedulingAdminPage } from '../SchedulingAdminPage'

describe('SchedulingAdminPage list', () => {
  it('renders all deterministic definition rows', async () => {
    render(
      <SchedulingAdminClientProvider client={createFixtureSchedulingAdminClient()}>
        <SchedulingAdminPage panelRailCapable />
      </SchedulingAdminClientProvider>,
    )

    // The platform DataGrid renders a div-based ARIA grid (role="grid"/"row"/"gridcell"),
    // not a native table — query by grid semantics.
    const grid = await screen.findByRole('grid', { name: 'Scheduling definitions' })
    const inspectionRow = within(grid)
      .getByRole('button', { name: 'inspection-protocol' })
      .closest<HTMLElement>('[role="row"]')
    expect(inspectionRow).not.toBeNull()
    expect(within(inspectionRow!).getByText('Inspection protocol')).toBeInTheDocument()
    expect(within(inspectionRow!).getByText('3')).toBeInTheDocument()
    expect(within(inspectionRow!).getByText('2026-08-18 09:00 UTC')).toBeInTheDocument()
    expect(within(inspectionRow!).getByText('user:fixture-scheduler')).toBeInTheDocument()

    expect(within(grid).getAllByRole('button')).toHaveLength(3)
  })
})
