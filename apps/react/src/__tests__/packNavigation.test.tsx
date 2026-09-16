import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import fixture from '../../../../tests/fixtures/access-navigation.json'
import workshop from '../../../../tests/fixtures/workshop-navigation.json'

const media = window.matchMedia
const innerWidth = window.innerWidth
const workshopItems = [
  ['asset-types', 'Asset types'], ['forms', 'Forms'], ['workflows', 'Workflows'], ['standards', 'Standards'],
  ['defaults', 'Defaults'], ['terminology', 'Terminology'], ['documents', 'Documents'], ['taxonomies', 'Taxonomies'],
  ['reports', 'Reports'], ['data-exchanges', 'Data exchanges'], ['standing-rules', 'Standing rules'],
  ['schedules', 'Schedules'], ['views', 'Views'],
] as const
const workshopKeys = ['workshop.workspace', 'workshop.definitions', ...workshopItems.map(([id]) => `workshop.${id}`)]
const accessFirst = { ...workshop, pack: { ...workshop.pack, seedWorkspaces: [...fixture.pack.seedWorkspaces, ...workshop.pack.seedWorkspaces] } }

it.each(['admin-forms', 'admin-reports', 'admin-views', 'admin-data-exchange', 'admin-scheduling'])
  ('cannot restore retired route %s even when no navigation seed is configured', async item => {
    vi.stubEnv('VITE_AUTHORIZATION_FIXTURE', '1')
    vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', '')
    window.history.replaceState({}, '', `/?item=${item}`)
    const request = vi.fn()
    vi.stubGlobal('fetch', request)
    const { App } = await import('../App')
    const cut = render(<App />)
    await waitFor(() => expect(new URLSearchParams(window.location.search).get('item')).toBe('assets'))
    expect(cut.container.querySelector('main h1')).toHaveTextContent('Assets')
    expect(cut.container.querySelector('[role=grid]')).toBeNull()
    expect(cut.container.querySelectorAll('[href*="admin-forms"],[href*="admin-reports"],[href*="admin-views"],[href*="admin-data-exchange"],[href*="admin-scheduling"]')).toHaveLength(0)
    expect(request).not.toHaveBeenCalled()
  })

afterEach(() => {
  vi.unstubAllEnvs()
  vi.unstubAllGlobals()
  vi.resetModules()
  window.matchMedia = media
  Object.defineProperty(window, 'innerWidth', { configurable: true, value: innerWidth })
  window.history.replaceState({}, '', '/')
})

it.each(['health', 'browse'])('restores an explicit %s surface for a declared Workshop item', async surface => {
  vi.stubEnv('VITE_FORMS_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'http://localhost:7308')
  window.history.replaceState({}, '', `/?item=forms&surface=${surface}&selected=inspection%401.0.0&panels=inspector`)
  const viewId = `platform.${surface}.forms`
  const plan = { definitionHash: 'hash', definitionId: viewId, definitionVersion: '1.0.0',
    packKey: 'harborline.platform', packVersion: '1.0.0', definitionKind: 'ViewDefinition',
    bindings: { viewKind: 'views.entity-list/grid', parameters: { fields: [{ id: 'formId', label: 'Key' }] } } }
  const fetchMock = vi.fn(async (url: string | URL | Request) => {
    const path = String(url)
    if (path.endsWith('/navigation/workspaces')) return Response.json(workshop)
    if (path.includes('/ViewDefinition/')) return Response.json({ renderPlan: plan })
    if (path.includes('/catalogue/definitions?')) return Response.json({ entries: [{ id: 'inspection', version: '1.0.0', status: 'Published' }], kindsUnavailable: [] })
    return Response.json([])
  })
  vi.stubGlobal('fetch', fetchMock)
  const { App } = await import('../App')
  const cut = render(<App />)
  await waitFor(() => expect(cut.container.querySelector('[aria-label="Definition inspector"]')).toHaveTextContent('inspection'))
  expect(fetchMock.mock.calls.filter(([url]) => String(url).includes('/ViewDefinition/')).map(([url]) => url))
    .toEqual([`/api/local-node/catalogue/definitions/ViewDefinition/${viewId}`])
  expect(new URLSearchParams(window.location.search).get('surface')).toBe(surface)
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
  expect(request.mock.calls.some(([url]) => String(url).endsWith('/api/selected-node/local-node/navigation/workspaces'))).toBe(true)
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

it.each([[480, 'compact', 'bottom-sheet', false], [720, 'medium', 'side-sheet', false], [1024, 'expanded', 'side-sheet', false], [1024, 'expanded', 'side-sheet', true]] as const)('restores the shared Workshop address and command census at %ipx (%s, %s, Access first: %s)', async (width, breakpoint, containerKind, multipleWorkspaces) => {
  vi.stubEnv('VITE_FORMS_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'http://localhost:7308')
  window.matchMedia = query => ({ ...media(query), matches: [...query.matchAll(/(min|max)-width:\s*(\d+)px/g)].every(([, bound, value]) => bound === 'min' ? width >= Number(value) : width <= Number(value)) })
  Object.defineProperty(window, 'innerWidth', { configurable: true, value: width })
  window.history.replaceState({}, '', multipleWorkspaces
    ? '/?source=shared%20link&item=forms&selected=inspection%401.0.0&panels=inspector#details'
    : '/?item=forms&selected=inspection%401.0.0&panels=inspector,pilot')
  const declaration = multipleWorkspaces ? accessFirst : workshop
  let releaseNavigation!: () => void
  const navigationReady = new Promise<void>(resolve => { releaseNavigation = resolve })

  const plan = {
    definitionHash: 'forms-hash', definitionId: 'platform.list.forms', definitionVersion: '1.0.0',
    packKey: 'harborline.platform', packVersion: '1.0.0', definitionKind: 'ViewDefinition',
    bindings: { viewKind: 'views.entity-list/grid', parameters: { fields: [{ id: 'formId', label: 'Key' }, { id: 'title', label: 'Title' }, { id: 'version', label: 'Version' }] } },
  }
  const entries = [
    { id: 'work-order', version: '1.0.0', status: 'Published', title: { defaultLocale: 'en', values: { en: 'Work order' } }, body: { cascadeLayer: 'Tenant' } },
    { id: 'inspection', version: '1.0.0', status: 'Published', title: { defaultLocale: 'en', values: { en: 'Inspection' } }, body: { cascadeLayer: 'Pack' } },
  ]
  vi.stubGlobal('fetch', vi.fn(async (url: string | URL | Request) => {
    const path = String(url)
    if (path.endsWith('/api/selected-node/local-node/navigation/workspaces')) {
      await navigationReady
      return Response.json(declaration)
    }
    if (path.includes('/ViewDefinition/')) return Response.json({ renderPlan: plan })
    if (path.includes('/catalogue/definitions?kind=FormDefinition')) return Response.json({ entries, kindsUnavailable: [] })
    return Response.json([])
  }))

  const { App } = await import('../App')
  const view = render(<App />)
  expect(screen.queryByRole('link', { name: 'Workshop' })).toBeNull()
  await act(async () => releaseNavigation())

  await waitFor(() => expect(view.container.querySelector('[data-shell-panel-id="inspector"]')).toHaveTextContent('Inspection'))
  if (width < 840) fireEvent.click(view.container.querySelector('button[aria-label="Navigation"][aria-controls]')!)
  expect(screen.getByRole('link', { name: 'Workshop' })).toHaveAttribute('aria-current', 'page')
  expect(view.container.querySelector('.happ-breadcrumb')).toHaveTextContent('Harborline / Workshop / Forms')
  const workspace = workshop.pack.seedWorkspaces[0]
  const group = workspace.groups[0]
  const panel = workshop.pack.panelSet[0]
  expect([workspace.id, workspace.labelKey]).toEqual(['workshop', 'workshop.workspace'])
  expect([group.id, group.labelKey, group.itemIds]).toEqual(['definitions', 'workshop.definitions', workshopItems.map(([id]) => id)])
  expect(group.items.map(item => [item.id, item.labelKey, item.label])).toEqual(workshopItems.map(([id, label]) => [id, `workshop.${id}`, label]))
  const labels: Record<string, string> = { 'workshop.workspace': 'Workshop', 'workshop.definitions': 'Definitions', 'panels.inspector': 'Inspector' }
  const rail = screen.getByRole('link', { name: 'Workshop' }).closest('[data-shell-region="rail"]')!
  fireEvent.click(screen.getByRole('button', { name: 'Show 6 more' }))
  expect([...rail.querySelectorAll('a')].map(link => [link.getAttribute('href'), link.textContent?.trim()])).toEqual([
    ...(multipleWorkspaces ? [['/workspaces/access', 'Access']] : []),
    ['/workspaces/workshop', 'Workshop'],
    ...workshopItems.map(([id, label]) => [`/workspaces/${id}`, label]),
  ])
  expect(rail).toHaveTextContent('Definitions')
  expect(rail).toHaveTextContent('Asset types')
  expect(rail.textContent!.indexOf('Definitions')).toBeLessThan(rail.textContent!.indexOf('Asset types'))
  for (const key of workshopKeys) expect(rail).not.toHaveTextContent(key)
  expect(screen.getByRole('link', { name: 'Forms' })).toHaveAttribute('aria-current', 'page')
  fireEvent.click(screen.getByRole('button', { name: 'Panels' }))
  expect([...view.container.querySelectorAll('[data-action-id]')].map(control => control.getAttribute('data-action-id'))).toEqual(workshop.pack.panelSet.map(declared => declared.id))
  expect(screen.getByRole('menuitem', { name: labels[panel.labelKey] })).toBeInTheDocument()
  expect(panel.binding).toBe(`panels.${panel.id}.toggle`)
  expect(workshop.pack.modeSwitch).toBeNull()
  expect(view.container.querySelector('[data-shell-zone="mode"]')).toBeNull()
  expect(await screen.findByRole('grid', {}, { timeout: 10_000 })).toBeInTheDocument()
  await waitFor(() => expect(view.container.querySelector('[data-shell-panel-id="inspector"]')).not.toBeNull())
  const inspector = view.container.querySelector<HTMLElement>('[data-shell-panel-id="inspector"]')!
  expect(inspector).toHaveTextContent('"cascadeLayer": "Pack"')
  const address = new URLSearchParams(window.location.search)
  // This fixture has one implicit facet and one open record: the selected definition.
  expect({
    workspace: screen.getByRole('link', { name: 'Workshop' }).getAttribute('aria-current') === 'page' ? workspace.id : null,
    rail: view.container.querySelector('[data-shell-breakpoint]')!.getAttribute('data-shell-breakpoint'),
    mode: address.get('mode'), selection: address.get('selected'),
    facet: inspector.querySelector('[role="tablist"]') === null ? 'default' : null,
    openRecords: [...inspector.querySelectorAll('h2')].map(heading => heading.textContent === 'Inspection' ? 'inspection@1.0.0' : null),
    panels: [...view.container.querySelectorAll('[data-shell-panel-id]')].map(root => root.getAttribute('data-shell-panel-id')),
    container: inspector.getAttribute('data-shell-container-kind'),
  }).toEqual({ workspace: workspace.id, rail: breakpoint, mode: null, selection: 'inspection@1.0.0', facet: 'default', openRecords: ['inspection@1.0.0'], panels: [panel.id], container: containerKind })
  expect(address.get('panels')).toBe(panel.id)
  const beforeUndeclared = window.location.search
  fireEvent.keyDown(document, { key: 'p', metaKey: true, shiftKey: true })
  expect(window.location.search).toBe(beforeUndeclared)
  expect(view.container.querySelector('[data-action-id="pilot"], [data-shell-panel-id="pilot"]')).toBeNull()
  expect(screen.queryByRole('button', { name: 'Pilot' })).toBeNull()

  fireEvent.doubleClick(screen.getByText('Work order'))
  await waitFor(() => expect(view.container.querySelector('[data-shell-panel-id="inspector"]')).toHaveTextContent('Work order'))
  expect(new URLSearchParams(window.location.search).get('item')).toBe('forms')
  expect(new URLSearchParams(window.location.search).get('selected')).toBe('work-order@1.0.0')
  expect(new URLSearchParams(window.location.search).get('panels')).toBe('inspector')

  fireEvent.click(screen.getByRole('button', { name: /Close inspector/i }))
  expect(view.container.querySelector('[data-shell-panel-id="inspector"]')).toBeNull()
  expect(new URLSearchParams(window.location.search).has('panels')).toBe(false)
  fireEvent.click(screen.getByRole('menuitem', { name: labels[panel.labelKey] }))
  expect(new URLSearchParams(window.location.search).get('panels')).toBe(panel.id)
  fireEvent.click(screen.getByRole('button', { name: /Close inspector/i }))
  fireEvent.keyDown(document, { key: panel.shortcut.split('+').at(-1), metaKey: panel.shortcut.includes('mod'), shiftKey: panel.shortcut.includes('shift') })
  expect(view.container.querySelector('[data-shell-panel-id="inspector"]')).toHaveTextContent('Work order')
  expect(new URLSearchParams(window.location.search).get('panels')).toBe(panel.id)
  if (multipleWorkspaces) {
    expect(new URLSearchParams(window.location.search).get('source')).toBe('shared link')
    expect(window.location.hash).toBe('#details')
    const copiedAddress = window.location.href
    view.unmount()
    const restored = render(<App />)
    await waitFor(() => expect(restored.container.querySelector('[data-shell-panel-id="inspector"]')).toHaveTextContent('Work order'))
    expect(screen.getByRole('link', { name: 'Workshop' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('link', { name: 'Forms' })).toHaveAttribute('aria-current', 'page')
    expect(window.location.href).toBe(copiedAddress)
    fireEvent.click(screen.getByRole('link', { name: 'Access' }))
    expect(screen.getByRole('link', { name: 'Access' })).toHaveAttribute('aria-current', 'page')
    fireEvent.click(screen.getByRole('link', { name: 'Holders' }))
    expect(restored.container.querySelector('.happ-breadcrumb')).toHaveTextContent('Harborline / Access / Holders')
    expect(new URLSearchParams(window.location.search).has('selected')).toBe(false)
    fireEvent.click(screen.getByRole('link', { name: 'Workshop' }))
    fireEvent.click(screen.getByRole('link', { name: 'Forms' }))
    expect(screen.getByRole('link', { name: 'Workshop' })).toHaveAttribute('aria-current', 'page')
    expect(restored.container.querySelector('.happ-breadcrumb')).toHaveTextContent('Harborline / Workshop / Forms')
  }
}, 120_000)

it.each([
  ['', 'inspection@1.0.0'],
  [' \t\r\n ', 'inspection@1.0.0'],
  ['Inspection', 'Inspection'],
  ['  Inspection  ', 'Inspection'],
])('shows a stable Inspector identity for title %j on activation and restore', async (title, expectedIdentity) => {
  vi.stubEnv('VITE_FORMS_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'http://localhost:7308')
  window.matchMedia = query => ({ ...media(query), matches: !query.includes('max-width') })
  window.history.replaceState({}, '', '/?source=shared%20link&item=forms#details')
  const plan = {
    definitionHash: 'forms-hash', definitionId: 'platform.list.forms', definitionVersion: '1.0.0',
    packKey: 'harborline.platform', packVersion: '1.0.0', definitionKind: 'ViewDefinition',
    bindings: { viewKind: 'views.entity-list/grid', parameters: { fields: [{ id: 'formId', label: 'Key' }, { id: 'title', label: 'Title' }] } },
  }
  const entry = { id: 'inspection', version: '1.0.0', status: 'Active', title: { defaultLocale: 'en', values: { en: title } }, body: { cascadeLayer: 'Pack', privateNote: 'Private body is not an identity' } }
  vi.stubGlobal('fetch', vi.fn(async (url: string | URL | Request) => {
    const path = String(url)
    if (path.endsWith('/api/selected-node/local-node/navigation/workspaces')) return Response.json(workshop)
    if (path.includes('/ViewDefinition/')) return Response.json({ renderPlan: plan })
    if (path.includes('/catalogue/definitions?kind=FormDefinition')) return Response.json({ entries: [entry], kindsUnavailable: [] })
    return Response.json([])
  }))
  const { App } = await import('../App')
  const view = render(<App />)
  await waitFor(() => expect(view.container.querySelector('[data-row-id="inspection@1.0.0"]')).not.toBeNull())
  fireEvent.doubleClick(view.container.querySelector('[data-row-id="inspection@1.0.0"]')!)
  const assertIdentity = (container: HTMLElement) => {
    const inspector = container.querySelector('[data-shell-panel-id="inspector"]')!
    expect(inspector.querySelector('h2')?.textContent).toBe(expectedIdentity)
    expect([...inspector.querySelectorAll('p')].map(paragraph => paragraph.textContent)).toContain(`${expectedIdentity} · follows selection`)
    const address = new URLSearchParams(window.location.search)
    expect(address.get('selected')).toBe('inspection@1.0.0')
    expect(address.get('panels')).toBe('inspector')
    expect(address.get('source')).toBe('shared link')
    expect(window.location.hash).toBe('#details')
  }
  await waitFor(() => assertIdentity(view.container))
  const copiedAddress = window.location.href
  view.unmount()
  const restored = render(<App />)
  await waitFor(() => assertIdentity(restored.container))
  expect(window.location.href).toBe(copiedAddress)
})

it.each(['missing', 'assets', 'unavailable', 'forms', 'empty'])('clears a stale selected row when the addressed item %s cannot restore Workshop', async item => {
  vi.stubEnv('VITE_FORMS_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'http://localhost:7308')
  window.history.replaceState({}, '', `/?item=${item}&selected=inspection%401.0.0&panels=pilot`)
  const declaration = structuredClone(accessFirst)
  if (item === 'forms') declaration.pack.seedWorkspaces = declaration.pack.seedWorkspaces.filter(workspace => workspace.id !== 'workshop')
  if (item === 'empty') { declaration.pack.seedWorkspaces = []; declaration.pack.panelSet = [] }
  vi.stubGlobal('fetch', vi.fn(async (url: string | URL | Request) => String(url).endsWith('/api/selected-node/local-node/navigation/workspaces') ? Response.json(declaration) : Response.json([])))
  const { App } = await import('../App')
  const view = render(<App />)
  await waitFor(() => expect(view.container.querySelector('main h1')).toHaveTextContent('Assets'))
  await waitFor(() => expect(new URLSearchParams(window.location.search).has('selected')).toBe(false))
  expect(new URLSearchParams(window.location.search).get('item')).toBe('assets')
  expect(new URLSearchParams(window.location.search).has('panels')).toBe(false)
  expect(view.container.querySelector('.happ-breadcrumb')).toHaveTextContent('Harborline / Portfolio / Assets')
  if (item === 'empty') expect(view.container.querySelectorAll('[data-shell-region="rail"] a')).toHaveLength(0)
})
