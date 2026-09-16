import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { PackActionHost } from '../PackActionHost'

const { send } = vi.hoisted(() => ({ send: vi.fn() }))
vi.mock('../../../../shared/selected-session-transport.mjs', async original => ({
  ...await original<object>(), send,
}))
afterEach(() => vi.clearAllMocks())

it('renders a generic declared action and preserves the actual refusal evidence', async () => {
  const dispatch = { schemaVersion: 1, kind: 'request', descriptor: { id: 'example.apply.v1',
    method: 'POST', routeTemplate: '/api/session/example/apply', contentType: 'application/json',
    audience: 'selected-session', requiresAntiforgery: true, inputs: [] }, bindings: {} }
  const plan = { definitionHash: 'hash', definitionId: 'example', definitionVersion: '1.0.0',
    packKey: 'example.pack', packVersion: '1.0.0', definitionKind: 'ViewDefinition',
    bindings: { viewKind: 'views.entity-list/grid', parameters: { entityType: 'Example', fields: [{ id: 'title', label: 'Title' }] },
      actions: [{ id: 'opaque', label: 'Apply change', operation: 'no.compiled.meaning', dispatch }] } }
  send.mockImplementation(async (path: string) => path.includes('/catalogue/')
    ? { status: 200, body: JSON.stringify({ renderPlan: plan }), auditId: null }
    : { status: 403, body: '{"code":"authorization.permission_required"}', auditId: 'native-audit', correlationId: 'native-correlation' })
  render(<PackActionHost viewId="example" />)
  fireEvent.click(await screen.findByRole('button', { name: 'Apply change' }))
  await waitFor(() => expect(screen.getAllByRole('button', { name: 'Apply change' })).toHaveLength(2))
  fireEvent.click(screen.getAllByRole('button', { name: 'Apply change' }).at(-1)!)
  expect(await screen.findByText('Audit: native-audit')).toBeInTheDocument()
  expect(screen.getByText('Correlation: native-correlation')).toBeInTheDocument()
  expect(screen.getByText('{"code":"authorization.permission_required"}')).toBeInTheDocument()
  expect(send.mock.calls.filter(call => call[1] === 'POST')).toHaveLength(1)
})
