import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { useState } from 'react'
import { AuthorizationAdminClientProvider } from '../AuthorizationAdminClientContext'
import { AuthorizationAdminPage } from '../AuthorizationAdminPage'
import { CapabilityBindingEditor } from '../CapabilityBindingEditor'
import { createFixtureAuthorizationAdminClient, CAPABILITY_DEFINITIONS, ROLE_DEFINITIONS } from '../client/fixtureClient'
import { AuthorizationAdminError, type AuthorizationAdminClient, type AuthorizationCapabilityDefinition } from '../client/types'

describe('Authorization administration surface', () => {
  it('enumerates every seeded operation exactly once with an open editor and ordered binding facts', async () => {
    render(<AuthorizationAdminClientProvider client={createFixtureAuthorizationAdminClient()}><AuthorizationAdminPage /></AuthorizationAdminClientProvider>)

    expect(new Set(CAPABILITY_DEFINITIONS.map(definition => definition.atom.operation)).size).toBe(CAPABILITY_DEFINITIONS.length)
    for (const definition of CAPABILITY_DEFINITIONS) {
      const title = `${definition.atom.operation} · ${definition.atom.scopeType}:${definition.atom.scopeValue}`
      const headings = await screen.findAllByRole('heading', { name: title })
      expect(headings).toHaveLength(1)
      expect(within(headings[0].closest('article')!).getByRole('button', { name: /binding/ })).toBeInTheDocument()
    }
    const first = screen.getByRole('heading', {
      name: `${CAPABILITY_DEFINITIONS[0].atom.operation} · ${CAPABILITY_DEFINITIONS[0].atom.scopeType}:${CAPABILITY_DEFINITIONS[0].atom.scopeValue}`,
    }).closest('article')!
    expect([...first.querySelectorAll('dt')].map(term => term.textContent)).toEqual([
      'Operation', 'Scoped atom', 'Publisher-offered roles', 'Effective roles', 'Source',
    ])
  })

  it('requires destructive confirmation for clear-all, posts [], and renders the 2xx warning as an alert', async () => {
    const user = userEvent.setup()
    const client = createFixtureAuthorizationAdminClient()
    const narrow = vi.spyOn(client, 'narrowCapabilityBinding')
    const definition = CAPABILITY_DEFINITIONS[0]
    function Harness() {
      const [current, setCurrent] = useState(definition)
      return <CapabilityBindingEditor
        definition={current}
        roleDefinitions={ROLE_DEFINITIONS}
        onNarrow={client.narrowCapabilityBinding}
        onSaved={result => setCurrent(row => ({ ...row, binding: { revision: result.revision, effectiveRoles: result.effectiveRoles, warning: result.warning } }))}
      />
    }
    render(<Harness />)

    await user.click(screen.getByRole('button', { name: /Clear all roles/ }))
    expect(narrow).not.toHaveBeenCalled()
    const dialog = screen.getByRole('dialog')
    expect(dialog).toHaveTextContent('Removed roles, including the last role, cannot be restored through this definition revision.')
    await user.click(within(dialog).getByRole('button', { name: 'Clear all roles' }))

    expect(narrow).toHaveBeenCalledWith(definition.definitionId, [], expect.any(String))
    expect(await screen.findByRole('alert')).toHaveTextContent('Binding saved with warning')
  })

  it('renders removed roles as disabled history and never as selectable standings', () => {
    const removed: AuthorizationCapabilityDefinition = {
      ...CAPABILITY_DEFINITIONS[0],
      binding: { revision: 3, effectiveRoles: [{ vocabulary: 'tax.roles', name: 'author' }], warning: null },
    }
    render(<CapabilityBindingEditor definition={removed} roleDefinitions={ROLE_DEFINITIONS} onNarrow={vi.fn()} onSaved={vi.fn()} />)

    expect(screen.getByRole('checkbox', { name: /Administrator/ })).toBeDisabled()
    expect(screen.getByText(/Removed; history only/)).toBeInTheDocument()
    expect(screen.queryByRole('checkbox', { name: /Filed/ })).not.toBeInTheDocument()
  })

  it('removes one role and renders the effective result', async () => {
    const user = userEvent.setup()
    render(<AuthorizationAdminClientProvider client={createFixtureAuthorizationAdminClient()}><AuthorizationAdminPage /></AuthorizationAdminClientProvider>)
    const editor = (await screen.findByRole('heading', { name: /tax\.return\.write/ })).closest('article')!

    await user.click(within(editor).getByRole('checkbox', { name: /Administrator/ }))
    await user.click(within(editor).getByRole('button', { name: 'Save narrower binding…' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Save narrower binding' }))

    await vi.waitFor(() => expect(within(editor).getByRole('checkbox', { name: /Administrator/ })).toBeDisabled())
    const effective = [...editor.querySelectorAll('dt')].find(term => term.textContent === 'Effective roles')?.nextElementSibling
    expect(effective).toHaveTextContent('Tax author')
    expect(effective).not.toHaveTextContent('Administrator')
  })

  it('shows lane-owned widening copy when a save is refused', async () => {
    const user = userEvent.setup()
    render(<CapabilityBindingEditor
      definition={CAPABILITY_DEFINITIONS[0]}
      roleDefinitions={ROLE_DEFINITIONS}
      onNarrow={vi.fn().mockRejectedValue(new AuthorizationAdminError(409, 'authorization.binding_widening_refused'))}
      onSaved={vi.fn()}
    />)

    await user.click(screen.getByRole('checkbox', { name: /Administrator/ }))
    await user.click(screen.getByRole('button', { name: 'Save narrower binding…' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Save narrower binding' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('A removed role cannot be restored through this definition revision.')
  })


  it('lists every carrying record type and warns for a field with none', async () => {
    render(<AuthorizationAdminClientProvider client={createFixtureAuthorizationAdminClient()}><AuthorizationAdminPage /></AuthorizationAdminClientProvider>)

    const field = await screen.findByRole('region', { name: 'Standing field returnId' })
    expect(within(field).getByText('tax.return')).toBeInTheDocument()
    expect(within(field).getByText('tax.return.amendment')).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Standing field legacyReference' })).toHaveTextContent('No carrying record types are declared for this field.')
  })

  it('shows loading, empty-catalogue, and narrow-width-safe states', async () => {
    const client = createFixtureAuthorizationAdminClient()
    client.listCapabilityDefinitions = async () => []
    render(<div style={{ width: 320 }}><AuthorizationAdminClientProvider client={client}><AuthorizationAdminPage /></AuthorizationAdminClientProvider></div>)

    expect(screen.getByText('Loading authorization settings')).toBeInTheDocument()
    expect(await screen.findByText('No authorization capability definitions are available.')).toBeInTheDocument()
    expect(screen.getByText('Standing Catalogue').closest('div')).toHaveStyle({ minWidth: '0' })
  })

  it('offers retry after a load error', async () => {
    const client = createFixtureAuthorizationAdminClient()
    const list = vi.spyOn(client, 'listRoleVocabulary').mockRejectedValueOnce(new Error('offline'))
    render(<AuthorizationAdminClientProvider client={client}><AuthorizationAdminPage /></AuthorizationAdminClientProvider>)

    expect(await screen.findByRole('alert')).toHaveTextContent('offline')
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(await screen.findByRole('heading', { name: 'Authorization capability bindings' })).toBeInTheDocument()
    expect(list).toHaveBeenCalledTimes(2)
  })

  it('renders a definition installed between catalogue loads without a code change', async () => {
    const client = createFixtureAuthorizationAdminClient()
    const first = CAPABILITY_DEFINITIONS[0]
    const installed: AuthorizationCapabilityDefinition = {
      ...first,
      definitionId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
      atom: { ...first.atom, operation: 'installed.package.read' },
      binding: { ...first.binding, effectiveRoles: [...first.binding.effectiveRoles] },
    }
    let calls = 0
    client.listCapabilityDefinitions = async () => ++calls === 1 ? [first] : [first, installed]
    const getEffectiveBinding = client.getEffectiveBinding
    client.getEffectiveBinding = async definitionId => definitionId === installed.definitionId
      ? installed.binding
      : getEffectiveBinding(definitionId)

    const firstView = render(<AuthorizationAdminClientProvider client={client}><AuthorizationAdminPage /></AuthorizationAdminClientProvider>)
    await screen.findByRole('heading', { name: /tax\.return\.write/ })
    expect(screen.queryByRole('heading', { name: /installed\.package\.read/ })).not.toBeInTheDocument()
    firstView.unmount()
    render(<AuthorizationAdminClientProvider client={client}><AuthorizationAdminPage /></AuthorizationAdminClientProvider>)

    expect(await screen.findByRole('heading', { name: /installed\.package\.read/ })).toBeInTheDocument()
    expect(calls).toBe(2)
  })

  it('aborts all in-flight catalogue requests when unmounted', () => {
    const signals: AbortSignal[] = []
    const pending = (signal?: AbortSignal) => {
      if (signal) signals.push(signal)
      return new Promise<never>(() => undefined)
    }
    const client: AuthorizationAdminClient = {
      listHolders: async () => { throw new Error("Not used by this page") },
      listRoleVocabulary: signal => pending(signal),
      listCapabilityDefinitions: signal => pending(signal),
      getEffectiveBinding: (_definitionId, signal) => pending(signal),
      narrowCapabilityBinding: (_definitionId, _roles, _reason, signal) => pending(signal),
      listStandingCatalogue: signal => pending(signal),
    }
    const view = render(<AuthorizationAdminClientProvider client={client}><AuthorizationAdminPage /></AuthorizationAdminClientProvider>)

    view.unmount()

    expect(signals).toHaveLength(3)
    expect(signals.every(signal => signal.aborted)).toBe(true)
  })
})
