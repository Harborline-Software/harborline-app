import { act, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import fixture from '../../../../tests/fixtures/access-navigation.json'

const media = window.matchMedia

afterEach(() => { vi.unstubAllEnvs(); vi.unstubAllGlobals(); vi.resetModules(); window.matchMedia = media })

it('renders the declared Access workspace and panels, removes them with the declaration, and retries a refused read', async () => {
  vi.stubEnv('VITE_FORMS_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'http://localhost:7308')
  window.matchMedia = query => ({ ...media(query), matches: !query.includes('max-width') })
  const response = { value: structuredClone(fixture), status: 200 }
  const request = vi.fn(async (url: string | URL | Request) => String(url).endsWith('/navigation/workspaces')
    ? new Response(JSON.stringify(response.value), { status: response.status })
    : new Response('[]'))
  vi.stubGlobal('fetch', request)
  const { App } = await import('../App')
  const first = render(<App />)
  await waitFor(() => expect(first.container.textContent).toContain('Access'))
  expect(request.mock.calls.some(([url]) => String(url).endsWith('/api/local-node/navigation/workspaces'))).toBe(true)
  if (screen.queryByRole('button', { name: 'Panels' })) await act(async () => screen.getByRole('button', { name: 'Panels' }).click())
  expect(first.container.querySelector('[data-action-id="access-details"]')).not.toBeNull()
  await act(async () => first.container.querySelector<HTMLButtonElement>('[data-action-id="access-details"] button')!.click())
  expect(first.container.querySelector('[data-shell-panel-id="access-details"]')).not.toBeNull()
  first.unmount()

  response.value.pack.seedWorkspaces = []
  response.value.pack.panelSet = []
  const removed = await act(async () => render(<App />))
  expect(removed.container.textContent).not.toContain('Access')
  expect(removed.container.querySelector('[data-action-id="access-details"]')).toBeNull()
  removed.unmount()

  response.status = 403
  const refused = render(<App />)
  expect(await screen.findByRole('alert')).toHaveTextContent('Unable to load application navigation')
  response.status = 200
  response.value = structuredClone(fixture)
  await act(async () => screen.getByRole('button', { name: 'Retry navigation' }).click())
  await waitFor(() => expect(refused.container.textContent).toContain('Access'))
  expect(screen.queryByRole('alert')).toBeNull()
})
