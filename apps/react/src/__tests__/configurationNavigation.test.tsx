import { act, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import navigation from '../../../../tests/fixtures/configuration-navigation.json'
import navigationWithoutOperate from '../../../../tests/fixtures/configuration-navigation-without-operate.json'

// T-657. Reachability, per lane. The api projects the `configuration` workspace only to a caller its
// configuration activation routes would admit (selected-session-product audience holding
// `packages:operate` install-wide), so the two fixtures here are that one route's two answers: the
// projection a holder is served, and the projection a non-holder is served. The shell mounts the
// existing T-460 surface on the entry's id and nothing about how that page renders changed.

const media = window.matchMedia
const EFFECTIVE = { digest: 'sha256:baseline-generation', algorithm: 'sha256', references: [] }

afterEach(() => {
  vi.unstubAllEnvs(); vi.unstubAllGlobals(); vi.resetModules(); window.matchMedia = media
  // The shell writes the active item into the address, so each case starts from a fresh session
  // rather than inheriting the previous one's deep link.
  window.history.replaceState(null, '', '/')
})

function replay(declaration: unknown): typeof fetch {
  return vi.fn(async input => {
    const path = String(input)
    if (path.endsWith('/navigation/workspaces')) return new Response(JSON.stringify(declaration), { status: 200 })
    if (path.endsWith('/configuration/effective')) return new Response(JSON.stringify(EFFECTIVE), { status: 200 })
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

it('a caller holding packages:operate reaches the configuration activation surface from the projected entry', async () => {
  const { result, request } = await mount(navigation)

  await act(async () => screen.getByRole('link', { name: 'Configuration' }).click())
  const item = await screen.findByRole('link', { name: 'Activation' })
  expect(item).toHaveAttribute('href', '/workspaces/configuration.activation')
  await act(async () => item.click())

  expect(await screen.findByTestId('effective-generation')).toHaveTextContent(EFFECTIVE.digest)
  expect(result.container.textContent).toContain('No configuration activation has been reported for this generation.')
  expect(vi.mocked(request).mock.calls.some(([input]) => String(input).endsWith('/api/local-node/configuration/effective'))).toBe(true)
})

it('a caller without packages:operate is not shown the configuration entry', async () => {
  const { result, request } = await mount(navigationWithoutOperate)

  expect(screen.queryByRole('link', { name: 'Configuration' })).toBeNull()
  expect(screen.queryByRole('link', { name: 'Activation' })).toBeNull()
  expect(result.container.textContent).not.toContain('configuration.activation')
  expect(result.container.textContent).not.toContain('Effective generation:')
  expect(vi.mocked(request).mock.calls.every(([input]) => !String(input).endsWith('/api/local-node/configuration/effective'))).toBe(true)
})
