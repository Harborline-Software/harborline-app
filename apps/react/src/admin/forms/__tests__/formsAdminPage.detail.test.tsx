import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MASTER_DETAIL_RAIL_QUERY } from '@harborline-software/ui-react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createFixtureFormsAdminClient } from '../client/fixtureClient'
import { FormsAdminClientProvider } from '../FormsAdminClientContext'
import { FormsAdminPage } from '../FormsAdminPage'

// Stub matchMedia so exactly the queries `matcher` grants report a match. Queries are keyed
// off the platform's exported MASTER_DETAIL_RAIL_QUERY constant, never a re-typed literal —
// a retuned rail retunes these tests with it.
function stubMatchMedia(matcher: (query: string) => boolean) {
  vi.stubGlobal('matchMedia', vi.fn((query: string) => ({
    matches: matcher(query),
    media: query,
    onchange: null,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
    dispatchEvent: () => false,
  })))
}

describe('FormsAdminPage detail', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('lets the capability hook decide the rail when no override is passed (ticket 154)', async () => {
    // useCanShowMasterDetail resolves true only when the shared rail query matches.
    stubMatchMedia(query => query === MASTER_DETAIL_RAIL_QUERY)
    const user = userEvent.setup()
    render(
      <FormsAdminClientProvider client={createFixtureFormsAdminClient()}>
        <FormsAdminPage />
      </FormsAdminClientProvider>,
    )

    await user.click(await screen.findByRole('button', { name: 'incident-intake' }))
    // Docked (complementary aside), not a modal dialog: the hook granted the rail.
    const panel = await screen.findByRole('complementary', { name: 'Form definition incident-intake' })
    expect(panel).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('refuses the rail when desktop width matches but the rail query does not (ticket 154)', async () => {
    // The discriminating direction: the pre-154 hook (canShowMasterDetail = mode !== phone)
    // answers TRUE here (desktop width matches), and the panel with the wiring deleted would
    // dock too under a rail-matching stub — so only the rail-aware policy renders the modal.
    // Desktop width matches; the rail must NOT (the 1280x500 class).
    stubMatchMedia(query => query !== MASTER_DETAIL_RAIL_QUERY && query === '(min-width: 1280px)')
    const user = userEvent.setup()
    render(
      <FormsAdminClientProvider client={createFixtureFormsAdminClient()}>
        <FormsAdminPage />
      </FormsAdminClientProvider>,
    )

    await user.click(await screen.findByRole('button', { name: 'incident-intake' }))
    const dialog = await screen.findByRole('dialog', { name: 'Form definition incident-intake' })
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(screen.queryByRole('complementary', { name: 'Form definition incident-intake' })).toBeNull()
  })

  it('renders the Forms pillar name as the page h1', async () => {
    const user = userEvent.setup()
    render(
      <FormsAdminClientProvider client={createFixtureFormsAdminClient()}>
        <FormsAdminPage panelRailCapable />
      </FormsAdminClientProvider>,
    )

    await user.click(await screen.findByRole('button', { name: 'incident-intake' }))
    const panel = await screen.findByRole('complementary', { name: 'Form definition incident-intake' })
    expect(screen.getByRole('heading', { level: 1, name: 'Forms' })).toBeInTheDocument()
    expect(within(panel).getByText('1.0.3', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText('Tenant', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText('user:fixture-admin', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText('Yes', { selector: 'dd' })).toBeInTheDocument()
    expect(within(panel).getByText('No', { selector: 'dd' })).toBeInTheDocument()

    const history = await within(panel).findByRole('table', { name: 'Version history' })
    expect(within(history).getAllByRole('row')).toHaveLength(5)
    const draftRow = within(history).getByText('Draft').closest('tr')
    expect(draftRow).not.toBeNull()
    expect(within(draftRow!).getByText('1.0.0')).toBeInTheDocument()
  })
})
