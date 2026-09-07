import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { createFixtureReportsAdminClient } from '../client/fixtureClient'
import { ReportsAdminClientProvider } from '../ReportsAdminClientContext'
import { ReportsAdminPage } from '../ReportsAdminPage'

describe('ReportsAdminPage detail', () => {
  it('renders the Reports h1 with h2 detail-section headings', async () => {
    const user = userEvent.setup()
    render(
      <ReportsAdminClientProvider client={createFixtureReportsAdminClient()}>
        <ReportsAdminPage panelRailCapable />
      </ReportsAdminClientProvider>,
    )

    await user.click(await screen.findByRole('button', { name: 'trial-balance' }))
    const panel = await screen.findByRole('complementary', { name: 'Report definition trial-balance' })
    expect(screen.getByRole('heading', { level: 1, name: 'Reports' })).toBeInTheDocument()
    expect(within(panel).getByRole('heading', { level: 2, name: 'Parameters' })).toBeInTheDocument()
    expect(within(panel).getByRole('heading', { level: 2, name: 'Provenance' })).toBeInTheDocument()
    expect(within(panel).getByText('2.1.0', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText('standard', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText('Tenant', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText('1', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText(/"chartId": "chart-7"/, { selector: 'pre' })).toBeInTheDocument()
    expect(within(panel).getByText(/"originPackKey": "harborline.core-reports"/, { selector: 'pre' })).toBeInTheDocument()
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
      <ReportsAdminClientProvider client={createFixtureReportsAdminClient()}>
        <ReportsAdminPage panelRailCapable />
      </ReportsAdminClientProvider>,
    )

    await user.click(await screen.findByRole('button', { name: 'occupancy' }))
    const panel = await screen.findByRole('complementary', { name: 'Report definition occupancy' })
    expect(within(panel).getByText('Ordered ordinally')).toBeInTheDocument()
    const rows = within(await within(panel).findByRole('table', { name: 'Version history' })).getAllByRole('row')
    expect(rows[1]).toHaveTextContent('2024-legacy')
    expect(rows[2]).toHaveTextContent('0.3.0')
  })
})
