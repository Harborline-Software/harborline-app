import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import fixture from '../../../../tests/fixtures/access-navigation.json'

const media = window.matchMedia
const innerWidth = window.innerWidth

afterEach(() => {
  vi.unstubAllEnvs()
  vi.unstubAllGlobals()
  vi.resetModules()
  window.matchMedia = media
  Object.defineProperty(window, 'innerWidth', { configurable: true, value: innerWidth })
  window.history.replaceState({}, '', '/')
})

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

it('restores and controls the declared Workshop Inspector through stable URL state', async () => {
  vi.stubEnv('VITE_FORMS_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'http://localhost:7308')
  window.matchMedia = query => ({ ...media(query), matches: !query.includes('max-width') })
  Object.defineProperty(window, 'innerWidth', { configurable: true, value: 1200 })
  window.history.replaceState({}, '', '/?item=forms&selected=work-order%401.0.0&panels=inspector')

  const plan = {
    definitionHash: 'forms-hash', definitionId: 'platform.list.forms', definitionVersion: '1.0.0',
    packKey: 'harborline.platform', packVersion: '1.0.0', definitionKind: 'ViewDefinition',
    bindings: { viewKind: 'views.entity-list/grid', parameters: { fields: [{ id: 'formId', label: 'Key' }, { id: 'title', label: 'Title' }, { id: 'version', label: 'Version' }] } },
  }
  const navigation = {
    configured: true,
    pack: {
      seedWorkspaces: [{ id: 'workshop', labelKey: 'workshop.workspace', groups: [{ id: 'definitions', labelKey: 'workshop.definitions', itemIds: ['forms'] }] }],
      panelSet: [{ id: 'inspector', labelKey: 'Inspector', binding: 'panels.inspector.toggle', shortcut: 'mod+shift+i', defaultWidth: 400, minimumHeight: 300, defaultOpen: false, traits: ['Scoped'] }],
    },
  }
  const entries = [
    { id: 'work-order', version: '1.0.0', status: 'Published', title: { defaultLocale: 'en', values: { en: 'Work order' } }, body: { cascadeLayer: 'Tenant' } },
    { id: 'inspection', version: '2.0.0', status: 'Published', title: { defaultLocale: 'en', values: { en: 'Inspection' } }, body: { cascadeLayer: 'Pack' } },
  ]
  vi.stubGlobal('fetch', vi.fn(async (url: string | URL | Request) => {
    const path = String(url)
    if (path.endsWith('/api/local-node/navigation/workspaces')) return Response.json(navigation)
    if (path.includes('/ViewDefinition/')) return Response.json({ renderPlan: plan })
    if (path.includes('/catalogue/definitions?kind=FormDefinition')) return Response.json({ entries, kindsUnavailable: [] })
    return Response.json([])
  }))

  const { App } = await import('../App')
  const view = render(<App />)

  await screen.findByText('Workshop')
  expect(await screen.findByRole('grid', {}, { timeout: 10_000 })).toBeInTheDocument()
  await waitFor(() => expect(view.container.querySelector('[data-shell-panel-id="inspector"]')).not.toBeNull())
  const inspector = view.container.querySelector<HTMLElement>('[data-shell-panel-id="inspector"]')!
  await waitFor(() => expect(inspector).toHaveTextContent('Work order'))
  expect(inspector).toHaveTextContent('"cascadeLayer": "Tenant"')

  fireEvent.doubleClick(screen.getByText('Inspection'))
  await waitFor(() => expect(view.container.querySelector('[data-shell-panel-id="inspector"]')).toHaveTextContent('Inspection'))
  expect(new URLSearchParams(window.location.search).get('item')).toBe('forms')
  expect(new URLSearchParams(window.location.search).get('selected')).toBe('inspection@2.0.0')
  expect(new URLSearchParams(window.location.search).get('panels')).toBe('inspector')

  fireEvent.click(screen.getByRole('button', { name: /Close inspector/i }))
  expect(view.container.querySelector('[data-shell-panel-id="inspector"]')).toBeNull()
  fireEvent.keyDown(document, { key: 'i', metaKey: true, shiftKey: true })
  expect(view.container.querySelector('[data-shell-panel-id="inspector"]')).toHaveTextContent('Inspection')
}, 120_000)
