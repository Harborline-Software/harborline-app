import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { createFixtureSchedulingAdminClient } from '../client/fixtureClient'
import { SchedulingAdminClientProvider } from '../SchedulingAdminClientContext'
import { SchedulingAdminPage } from '../SchedulingAdminPage'

describe('SchedulingAdminPage detail', () => {
  it('renders the Scheduling h1 with an h2 detail-section heading', async () => {
    const user = userEvent.setup()
    render(
      <SchedulingAdminClientProvider client={createFixtureSchedulingAdminClient()}>
        <SchedulingAdminPage panelRailCapable />
      </SchedulingAdminClientProvider>,
    )

    await user.click(await screen.findByRole('button', { name: 'inspection-protocol' }))
    const panel = await screen.findByRole('complementary', { name: 'Scheduling definition inspection-protocol' })
    expect(screen.getByRole('heading', { level: 1, name: 'Scheduling' })).toBeInTheDocument()
    expect(within(panel).getByRole('heading', { level: 2, name: 'Definition' })).toBeInTheDocument()
    // The Blazor lane pins the same five terms in the same order — the metadata list is the part
    // of the surface a reader compares between lanes first.
    expect([...panel.querySelectorAll('dl dt')].map(term => term.textContent)).toEqual(
      ['Definition id', 'Title', 'Head revision', 'Updated', 'Updated by'],
    )
    expect(within(panel).getByText('3', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText('Inspection protocol', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText('user:fixture-scheduler', { selector: 'dd' })).toBeInTheDocument()
    const definitionBody = await within(panel).findByText((_, element) => (
      element?.tagName === 'PRE' && element.textContent?.includes('"cadence": "weekly"') === true
    ))
    expect(definitionBody).toBeInTheDocument()

    const history = await within(panel).findByRole('table', { name: 'Revision history' })
    expect(within(history).getAllByRole('row')).toHaveLength(4)
    const firstRevisionRow = within(history).getByText('2026-08-05 08:00 UTC').closest('tr')
    expect(firstRevisionRow).not.toBeNull()
    expect(within(firstRevisionRow!).getByText('1')).toBeInTheDocument()
  })
})
