import { StrictMode } from 'react'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import userEvent from '@testing-library/user-event'
import { send } from '../../../shared/selected-session-transport.mjs'
import { PackActionHost } from './PackActionHost'

afterEach(() => vi.unstubAllGlobals())
const deferred = () => { let resolve!: () => void; const promise = new Promise<void>(done => { resolve = done }); return { promise, resolve } }
const plan = { definitionId: 'example', definitionKind: 'ViewDefinition', definitionVersion: '1.0.0',
  bindings: { viewKind: 'views.entity-list/grid', parameters: { entityType: 'Example', fields: [] },
    actions: [{ id: 'opaque', label: 'Apply', dispatch: { schemaVersion: 1, kind: 'request', bindings: {},
      descriptor: { id: 'example.apply.v1', method: 'POST', routeTemplate: '/api/session/apply', contentType: 'application/json',
        audience: 'selected-session', requiresAntiforgery: true, inputs: [] } } }] } }
const response = (path: string) => path.endsWith('/antiforgery')
  ? new Response(null, { status: 204, headers: { 'X-Harborline-Antiforgery': 'once' } })
  : Response.json(path.includes('/catalogue/') ? { renderPlan: plan } : {})

it('unmount revokes a queued pack action before CSRF issuance or mutation', async () => {
  const entered = deferred(), release = deferred()
  const request = vi.fn(async (url: string | URL | Request) => {
    const path = String(url)
    if (path.endsWith('/block')) { entered.resolve(); await release.promise }
    return response(path)
  })
  vi.stubGlobal('fetch', request)
  const view = render(<PackActionHost viewId="example" />)
  fireEvent.click(await screen.findByRole('button', { name: 'Apply' }))
  await waitFor(() => expect(screen.getAllByRole('button', { name: 'Apply' })).toHaveLength(2))
  const apply = screen.getAllByRole('button', { name: 'Apply' }).at(-1)!
  const blocker = send('/api/session/block'); await entered.promise
  fireEvent.click(apply)
  view.unmount()
  await act(async () => { release.resolve(); await blocker; await send('/api/session/drain') })
  expect(request.mock.calls.some(([url]) => /antiforgery|\/apply$/.test(String(url)))).toBe(false)
})

it('unmount never retries an already dispatched action or publishes its navigation callback', async () => {
  const entered = deferred(), release = deferred(), navigation = vi.fn()
  const replacementPlan = { ...plan, bindings: { ...plan.bindings,
    actions: plan.bindings.actions.map(action => ({ ...action, result: { refresh: 'view' } })) } }
  const request = vi.fn(async (url: string | URL | Request) => {
    const path = String(url)
    if (path.endsWith('/apply')) { entered.resolve(); await release.promise }
    return path.includes('/catalogue/') ? Response.json({ renderPlan: replacementPlan }) : response(path)
  })
  vi.stubGlobal('fetch', request)
  const view = render(<PackActionHost viewId="example" onNavigationChanged={navigation} />)
  fireEvent.click(await screen.findByRole('button', { name: 'Apply' }))
  await waitFor(() => expect(screen.getAllByRole('button', { name: 'Apply' })).toHaveLength(2))
  fireEvent.click(screen.getAllByRole('button', { name: 'Apply' }).at(-1)!)
  await entered.promise
  view.unmount()
  await act(async () => { release.resolve(); await send('/api/session/drain') })
  expect(request.mock.calls.filter(([url]) => String(url).endsWith('/apply'))).toHaveLength(1)
  expect(request.mock.calls.filter(([url]) => String(url).includes('/catalogue/'))).toHaveLength(1)
  expect(navigation).not.toHaveBeenCalled()
})

it('StrictMode remount creates a live runtime after the previous effect is disposed', async () => {
  vi.stubGlobal('fetch', vi.fn(async (url: string | URL | Request) => response(String(url))))
  render(<StrictMode><PackActionHost viewId="example" /></StrictMode>)
  expect(await screen.findByRole('button', { name: 'Apply' })).toBeEnabled()
})

it('reopening the same file action clears the native picker and accepts the same file again', async () => {
  const filePlan = { ...plan, bindings: { ...plan.bindings,
    actions: plan.bindings.actions.map(action => ({ ...action, fileInput: { accept: '.json' } })) } }
  vi.stubGlobal('fetch', vi.fn(async () => Response.json({ renderPlan: filePlan })))
  const user = userEvent.setup()
  render(<PackActionHost viewId="example" />)
  await user.click(await screen.findByRole('button', { name: 'Apply' }))
  const file = new File(['{}'], 'replacement.json', { type: 'application/json' })
  await user.upload(await screen.findByLabelText('Package file'), file)
  expect(screen.getAllByRole('button', { name: 'Apply' }).at(-1)).toBeEnabled()
  await user.click(screen.getAllByRole('button', { name: 'Apply' })[0])
  const reopened = await screen.findByLabelText<HTMLInputElement>('Package file')
  expect(reopened.files).toHaveLength(0)
  expect(reopened.value).toBe('')
  expect(screen.getAllByRole('button', { name: 'Apply' }).at(-1)).toBeDisabled()
  await user.upload(reopened, file)
  expect(reopened.files?.[0]).toBe(file)
  expect(screen.getAllByRole('button', { name: 'Apply' }).at(-1)).toBeEnabled()
})
