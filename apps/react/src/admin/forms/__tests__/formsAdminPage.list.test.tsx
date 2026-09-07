import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { createFixtureFormsAdminClient } from '../client/fixtureClient'
import { FormsAdminClientProvider } from '../FormsAdminClientContext'
import { FormsAdminPage } from '../FormsAdminPage'

describe('FormsAdminPage list', () => {
  it('renders all deterministic definition rows and the missing-title fallback', async () => {
    render(
      <FormsAdminClientProvider client={createFixtureFormsAdminClient()}>
        <FormsAdminPage panelRailCapable />
      </FormsAdminClientProvider>,
    )

    // The platform DataGrid renders a div-based ARIA grid (role="grid"/"row"/"gridcell"),
    // not a native table — query by grid semantics.
    const grid = await screen.findByRole('grid', { name: 'Form definitions' })
    const incidentRow = within(grid)
      .getByRole('button', { name: 'incident-intake' })
      .closest<HTMLElement>('[role="row"]')
    expect(incidentRow).not.toBeNull()
    expect(within(incidentRow!).getByText('Incident intake')).toBeInTheDocument()
    expect(within(incidentRow!).getByText('1.0.3')).toBeInTheDocument()
    expect(within(incidentRow!).getByText('Tenant')).toBeInTheDocument()

    expect(within(grid).getAllByRole('button')).toHaveLength(3)
    const crewRow = within(grid)
      .getByRole('button', { name: 'crew-manifest' })
      .closest<HTMLElement>('[role="row"]')
    expect(crewRow).not.toBeNull()
    expect(within(crewRow!).getByText('—')).toBeInTheDocument()
    // The fixture deliberately carries one non-'Tenant' row (review P2-12) so the 'Pack'
    // cascade-layer render path is exercised by a fixture-backed screen.
    expect(within(crewRow!).getByText('Pack')).toBeInTheDocument()
  })
})
