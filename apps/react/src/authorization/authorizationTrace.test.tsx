import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import fixture from '../../../../tests/fixtures/authorization-trace.json'
import { CapabilityBindingEditor } from '../admin/authorization/CapabilityBindingEditor'
import { CAPABILITY_DEFINITIONS, ROLE_DEFINITIONS } from '../admin/authorization/client/fixtureClient'
import type { AuthorizationTraceRead } from './AuthorizationTrace'

function mount(read: (id: string) => Promise<AuthorizationTraceRead>) {
  const view = render(<CapabilityBindingEditor definition={CAPABILITY_DEFINITIONS[0]} roleDefinitions={ROLE_DEFINITIONS}
    onNarrow={vi.fn()} onSaved={vi.fn()} decisionTrace={{ auditId: fixture.auditId, read }} />)
  const details = screen.getByText('Why can I do this?').closest('details')!
  details.open = true
  fireEvent(details, new Event('toggle'))
  return view
}

describe('Authorization trace at binding feedback', () => {
  it.each(['allowed', 'denied'])('renders the four ordered parts and deciding grant for %s from the shared fixture', async verdict => {
    const result = structuredClone(fixture.read)
    if (verdict === 'denied') {
      result.steps[3].facts = ['verdict:denied', 'refusal:NoEffectiveRole', 'version:1']
      result.steps[1].facts = ['roles:none', 'deciding:none']
    }
    const read = vi.fn().mockResolvedValue(result)
    const view = mount(read)
    const list = await screen.findByRole('list', { name: 'Authorization trace' })
    expect(read).toHaveBeenCalledWith(fixture.auditId)
    expect(within(list).getAllByRole('heading').map(node => node.textContent)).toEqual([
      'Act kind', 'Roles in force for the subject with scope and dates', 'Standings', 'Verdict',
    ])
    const parts = within(list).getAllByRole('listitem')
    result.steps.forEach((step, index) => step.facts.forEach(fact => expect(parts[index]).toHaveTextContent(fact)))
    expect(parts[3]).toHaveTextContent(`Deciding grant: ${verdict === 'allowed' ? 'grant-163@1' : 'None recorded'}`)
    expect(list.querySelectorAll('[class]')).toHaveLength(0)
    view.unmount()
  })

  it.each([2, 1, 99, -1])('shows availability %s without disclosing any trace facts and retries refusal/errors', async availability => {
    const read = vi.fn().mockImplementationOnce(() => availability === -1
      ? Promise.reject(new Error('private transport detail'))
      : Promise.resolve({ ...fixture.read, availability })).mockResolvedValue(fixture.read)
    mount(read)
    const messages: Record<number, string> = {
      2: 'You do not have permission to read this authorization trace.',
      1: 'No authorization trace was recorded for this decision.',
      99: 'The recorded authorization trace is incomplete or unsupported.',
      '-1': 'Unable to read the authorization trace. Try again.',
    }
    expect(await screen.findByText(messages[availability])).toBeInTheDocument()
    expect(screen.queryByRole('list', { name: 'Authorization trace' })).not.toBeInTheDocument()
    expect(screen.queryByText('deciding:grant:grant-163@1')).not.toBeInTheDocument()
    if (availability === 2 || availability === -1) {
      fireEvent.click(screen.getByRole('button', { name: 'Retry trace read' }))
      expect(await screen.findByRole('list', { name: 'Authorization trace' })).toBeInTheDocument()
    }
  })
})
