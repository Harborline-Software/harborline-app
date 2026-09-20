import { act, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import navigation from '../../../../tests/fixtures/proposal-navigation.json'
import navigationWithoutAuthor from '../../../../tests/fixtures/proposal-navigation-without-author.json'
// The SAME released definition and conformance example T-461's surface test drives, copied out of the
// pinned platform checkout by scripts/build-local-feed.mjs. This test authors no proposal payload of
// its own, so the answer it replays cannot drift from the one the surface is proved against.
import example from '../../.feed/platform/proposal-cases.json'

// T-668. Reachability, per lane. The api projects `configuration.proposal` only to a caller its
// proposed-change routes would admit (the selected-session-product audience holding `packages:author`
// install-wide), so the two fixtures here are that one route's two answers: the projection an author
// is served, and the projection an operator who cannot author is served — the SAME workspace, minus
// the one entry. The shell mounts the existing T-461 surface on the entry's id and nothing about how
// that page renders changed.

const media = window.matchMedia

type Case = { step: string; status: string; values: Record<string, string> }
const PROPOSED = (example.cases as Case[]).find(entry => entry.step === 'proposed')!

/** The api answer for the working proposed change, as bytes on the wire. */
const READ = JSON.stringify({
  status: PROPOSED.status,
  tenantKey: PROPOSED.values.tenantKey,
  proposalId: PROPOSED.values.proposalId,
  baselineDigest: PROPOSED.values.baselineDigest,
  effectiveDigest: PROPOSED.values.effectiveDigest,
  workingDigest: PROPOSED.values.workingDigest,
  edits: [],
  savedVersionCount: 0,
  savedVersion: null,
  check: null,
  releasedPackage: null,
  refusals: [],
  detail: PROPOSED.values,
})

afterEach(() => {
  vi.unstubAllEnvs(); vi.unstubAllGlobals(); vi.resetModules(); window.matchMedia = media
  window.history.replaceState(null, '', '/')
})

function replay(declaration: unknown): typeof fetch {
  return vi.fn(async input => {
    const path = String(input)
    if (path.endsWith('/navigation/workspaces')) return new Response(JSON.stringify(declaration), { status: 200 })
    if (path.includes('/configuration/proposals/')) return new Response(READ, { status: 200 })
    return new Response(JSON.stringify([]), { status: 200 })
  })
}

async function mount(declaration: unknown) {
  vi.stubEnv('VITE_FORMS_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'http://127.0.0.1:7322')
  window.matchMedia = query => ({ ...media(query), matches: !query.includes('max-width') })
  const request = replay(declaration)
  vi.stubGlobal('fetch', request)
  const { App } = await import('../App')
  const result = render(<App />)
  await waitFor(() => expect(result.container.textContent).toContain('Access'))
  return { result, request }
}

const proposalRequests = (request: typeof fetch) =>
  vi.mocked(request).mock.calls.filter(([input]) => String(input)
    .includes('/api/local-node/configuration/proposals/'))

it('a caller holding packages:author reaches the proposed-change surface from the projected entry', async () => {
  const { request } = await mount(navigation)

  await act(async () => screen.getByRole('link', { name: 'Configuration' }).click())
  const item = await screen.findByRole('link', { name: 'Proposed change' })
  expect(item).toHaveAttribute('href', '/workspaces/configuration.proposal')
  await act(async () => item.click())

  // The existing T-461 surface, rendering the released detail through the shared SchemaForm exactly
  // as its own test proves. This asserts it is MOUNTED, not how it renders.
  expect(await screen.findByRole('form', { name: 'Proposed change' })).toBeInTheDocument()
  // The entry addresses this install's working proposed change by the entry's own id.
  expect(proposalRequests(request).map(([input]) => String(input)))
    .toEqual(['/api/local-node/configuration/proposals/configuration.proposal'])
})

it('a caller without packages:author is not shown the proposed-change entry, and still reaches activation', async () => {
  const { result } = await mount(navigationWithoutAuthor)

  await act(async () => screen.getByRole('link', { name: 'Configuration' }).click())
  expect(screen.queryByRole('link', { name: 'Proposed change' })).toBeNull()
  expect(result.container.textContent).not.toContain('configuration.proposal')
  // The workspace itself survives: the api scopes the two entries separately, so an operator who
  // cannot author still reaches activation.
  expect(await screen.findByRole('link', { name: 'Activation' }))
    .toHaveAttribute('href', '/workspaces/configuration.activation')
})

it('no proposed-change request is made for a caller the api did not offer the entry', async () => {
  const { request } = await mount(navigationWithoutAuthor)

  await act(async () => screen.getByRole('link', { name: 'Configuration' }).click())

  expect(proposalRequests(request)).toEqual([])
})
