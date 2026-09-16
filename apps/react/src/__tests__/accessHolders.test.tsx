import { act, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import navigation from '../../../../tests/fixtures/access-navigation.json'

const media = window.matchMedia
afterEach(() => { vi.unstubAllEnvs(); vi.unstubAllGlobals(); vi.resetModules(); window.matchMedia = media; window.history.replaceState({}, '', '/') })
const plan = {
  definitionHash: 'fixture-hash', definitionId: 'access.holders', definitionVersion: '1.0.0',
  packKey: 'harborline.access-administration', packVersion: '1.1.1', definitionKind: 'ViewDefinition',
  bindings: { viewKind: 'views.entity-list/grid', parameters: { entityType: 'Example', fields: [{ id: 'scope', label: 'Scope' }] }, actions: [],
    dataSource: { descriptor: { id: 'authorization.holders.selected.read.v1', method: 'GET',
      routeTemplate: '/api/session/admin/grants/holders', contentType: 'application/json', audience: 'selected-session',
      requiresAntiforgery: false, inputs: [], rowsPointer: '/rows', rowIdentityPointer: '/grantId' }, bindings: {} } },
}
async function mount(response: { status: number; body: unknown }, unknownKind = false) {
  vi.stubEnv('VITE_FORMS_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'http://127.0.0.1:7322')
  window.matchMedia = query => ({ ...media(query), matches: !query.includes('max-width') })
  const request = vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input)
    if (path.endsWith('/navigation/workspaces')) return Response.json(navigation)
    if (path.includes('/catalogue/definitions/ViewDefinition/')) return Response.json({ renderPlan: unknownKind
      ? { ...plan, bindings: { ...plan.bindings, viewKind: 'views.unknown' } } : plan })
    if (path.endsWith('/admin/grants/holders')) return Response.json(response.body, { status: response.status })
    return Response.json([])
  })
  vi.stubGlobal('fetch', request)
  const { App } = await import('../App')
  const component = render(<App />)
  await waitFor(() => expect(component.container.textContent).toContain('Access'))
  await act(async () => screen.getByRole('link', { name: 'Access' }).click())
  await act(async () => (await screen.findByRole('link', { name: 'Holders' })).click())
  return { component, request }
}

it('an active pack item loads its compiled definition and selected-session rows instead of a compiled Access page', async () => {
  const { request } = await mount({ status: 200, body: { rows: [{ grantId: 'row-one', scope: '/records/one' }] } })
  expect(await screen.findByText('/records/one')).toBeInTheDocument()
  expect(request.mock.calls.some(([url]) => String(url).endsWith('/api/selected-node/local-node/catalogue/definitions/ViewDefinition/access.holders'))).toBe(true)
  expect(request.mock.calls.some(([url]) => String(url).endsWith('/api/selected-node/session/admin/grants/holders'))).toBe(true)
  expect(request.mock.calls.some(([url]) => String(url).endsWith('/authorization/holders'))).toBe(false)
})

it('a refused source keeps server evidence and reloads through the same selected-session route', async () => {
  const response = { status: 403, body: { code: 'authorization.permission_required', auditId: 'native-denial' } as unknown }
  await mount(response)
  expect(await screen.findByText('Audit: native-denial')).toBeInTheDocument()
  expect(screen.queryByText('No active holders.')).toBeNull()
  response.status = 200; response.body = { rows: [{ grantId: 'row-one', scope: '/records/one' }] }
  await act(async () => screen.getByRole('button', { name: 'Reload view' }).click())
  expect(await screen.findByText('/records/one')).toBeInTheDocument()
})

it('unknown definitions are inert and never fall back to the hardcoded holder reader', async () => {
  const { request } = await mount({ status: 200, body: { rows: [] } }, true)
  expect(await screen.findByText('pack_view_unsupported')).toBeInTheDocument()
  expect(request.mock.calls.some(([url]) => String(url).endsWith('/admin/grants/holders') || String(url).endsWith('/authorization/holders'))).toBe(false)
})
