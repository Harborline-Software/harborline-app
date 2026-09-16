import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { expect, it, vi } from 'vitest'
import { PackActionHost } from './PackActionHost'

const { send } = vi.hoisted(() => ({ send: vi.fn() }))
vi.mock('../../../shared/selected-session-transport.mjs', async importOriginal => ({
  ...await importOriginal<object>(), send,
}))

it('renders only admitted request details, preserves retry values, and resets only on New request', async () => {
  const correlation = '43300000-0000-4000-8000-000000000101'
  const action = { id: 'opaque', label: 'Apply', dispatch: { schemaVersion: 1, kind: 'request',
    descriptor: { id: 'example.apply.v1', method: 'POST', routeTemplate: '/api/session/example/apply',
      contentType: 'application/json', audience: 'selected-session', requiresAntiforgery: true,
      inputs: [{ name: 'correlation', kind: 'Text', placement: 'Header', wireName: 'X-Correlation-ID' }] },
    bindings: { correlation: { source: 'invocation', pointer: '/correlationId' } } } }
  send.mockImplementation(async (path: string, _method: string, _body: string, _type: string, headers: Record<string, string>) => ({
    status: path.includes('/catalogue/') ? 200 : 403,
    body: JSON.stringify(path.includes('/catalogue/') ? { renderPlan: {
      definitionId: 'example', definitionKind: 'ViewDefinition', definitionVersion: '1.0.0',
      bindings: { viewKind: 'views.entity-list/grid', parameters: { entityType: 'Example', fields: [] }, actions: [action] },
    } } : { code: 'authorization.permission_required' }), auditId: 'native-audit', correlationId: headers?.['X-Correlation-ID'],
  }))
  render(<PackActionHost viewId="example" />)
  fireEvent.click(await screen.findByRole('button', { name: 'Apply' }))
  const field = await screen.findByLabelText('Correlation ID')
  expect(screen.queryByLabelText('Actor')).toBeNull()
  expect(screen.queryByLabelText('Idempotency key')).toBeNull()
  const panel = screen.getByRole('region', { name: 'Apply' })
  fireEvent.change(field, { target: { value: 'invalid' } })
  fireEvent.click(within(panel).getByRole('button', { name: 'Apply' }))
  expect(await screen.findByText('pack_request_details_invalid')).toBeInTheDocument()
  expect(send).toHaveBeenCalledTimes(1)
  fireEvent.change(field, { target: { value: correlation } })
  fireEvent.click(within(panel).getByRole('button', { name: 'Apply' }))
  expect(await screen.findByText(`Correlation: ${correlation}`)).toBeInTheDocument()
  expect(field).toBeDisabled()
  fireEvent.click(within(panel).getByRole('button', { name: 'Apply' }))
  await waitFor(() => expect(send).toHaveBeenCalledTimes(3))
  expect(send.mock.calls[1][4]).toEqual(send.mock.calls[2][4])
  fireEvent.click(screen.getByRole('button', { name: 'New request' }))
  expect(field).not.toBeDisabled()
  expect(field).not.toHaveValue(correlation)
})
