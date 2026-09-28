import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { WorkshopWorkflow, formViewFromPlan } from '../WorkshopWorkflow'

const literal = (value: string) => ({ kind: 'Literal', value })
const actions = [
  { id: 'author', label: 'Draft check', operation: 'pack.validate', inputForm: 'platform.pack.author' },
  { id: 'export', label: 'Make bundle', operation: 'pack.export' },
  { id: 'verify', label: 'Check signature', operation: 'pack.verify' },
  { id: 'install', label: 'Stage bundle', operation: 'pack.install' },
  { id: 'activate', label: 'Go live', operation: 'pack.activate' },
  { id: 'create', label: 'Add item', operation: 'record.create', input: 'active-pack.property-form' },
  { id: 'read', label: 'Inspect evidence', operation: 'record.read' },
]
const plan = {
  definitionId: 'round3.workshop', definitionVersion: '1', definitionKind: 'ViewDefinition',
  bindings: { viewKind: 'layout.table', parameters: { fields: [], actions }, actions: actions.map(({ id, label }) => ({ id, label })) },
}
const authorForm = { id: 'platform.pack.author', version: '1', renderPlan: {
  definitionId: 'platform.pack.author', definitionVersion: '1', definitionKind: 'FormDefinition',
  bindings: { fields: { packJson: { type: 'textarea', required: true } }, overlay: { fields: { packJson: { label: literal('Pack document') } } } },
} }
const propertyForm = { id: 'acme.capture', version: '2', renderPlan: {
  definitionId: 'acme.capture', definitionVersion: '2', definitionKind: 'FormDefinition',
  bindings: { fields: { title: { type: 'text', required: true } }, overlay: { fields: { title: { label: literal('Asset title') } } } },
} }
const candidate = { key: 'acme.assets', version: '1', contents: [{ key: 'acme.asset', kind: 'AssetTypeDefinition', version: '1', content: { displayName: 'Fallback asset name' } }] }

afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks() })

function renderWorkflow() {
  return render(<WorkshopWorkflow plan={plan as never} rows={[]} onRowActivate={() => undefined} onActivated={async () => undefined} />)
}

async function submitPack(value = JSON.stringify(candidate)) {
  fireEvent.click(screen.getByRole('button', { name: 'Draft check' }))
  const input = await screen.findByRole('textbox', { name: 'Pack document' })
  fireEvent.change(input, { target: { value } })
  fireEvent.click(screen.getAllByRole('button', { name: 'Draft check' }).at(-1)!)
  return input
}

describe('T-767 Round 3 workshop boundaries', () => {
  it('publishes required constraints from a compiled form plan', () => {
    const view = formViewFromPlan({
      definitionId: 'asset', definitionVersion: '1', definitionKind: 'FormDefinition',
      bindings: { fields: { title: { type: 'text', required: true } }, overlay: { fields: { title: { label: literal('Title') } } } },
    })
    expect(view.sections[0].fields[0]).toMatchObject({ name: 'title', required: true })
  })

  it('maps a legacy pointer refusal onto the authored field with its code', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: string) => input.includes('/FormDefinition/')
      ? Response.json(authorForm)
      : new Response(JSON.stringify({ errors: [{ pointer: '/values/packJson', message: 'Reserved pack key', code: 'pack.key.reserved' }] }), { status: 422 })))
    renderWorkflow()
    const input = await submitPack()
    expect((await screen.findAllByText('Reserved pack key')).length).toBeGreaterThan(0)
    expect(input).toHaveAttribute('aria-invalid', 'true')
  })

  it('keeps an empty server refusal visible and records its fallback text', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: string) => input.includes('/FormDefinition/')
      ? Response.json(authorForm) : new Response('', { status: 503 })))
    renderWorkflow()
    await submitPack()
    expect((await screen.findAllByRole('alert')).some(alert => alert.textContent?.includes('Request failed (503).'))).toBe(true)
    expect(within(screen.getByRole('region', { name: 'Workshop command results' })).getByText('null')).toBeInTheDocument()
  })

  it('refuses a declared author command whose form lacks a render plan', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ id: 'platform.pack.author', version: '1' })))
    renderWorkflow()
    fireEvent.click(screen.getByRole('button', { name: 'Draft check' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('no active compiled render plan')
  })

  it('refuses prerequisites before their corresponding requests', async () => {
    const fetchMock = vi.fn(async () => Response.json({}))
    vi.stubGlobal('fetch', fetchMock)
    renderWorkflow()
    fireEvent.click(screen.getByRole('button', { name: 'Stage bundle' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Complete')
    fireEvent.click(screen.getByRole('button', { name: 'Go live' }))
    fireEvent.click(screen.getByRole('button', { name: 'Add item' }))
    await waitFor(() => expect(fetchMock).not.toHaveBeenCalled())
  })

  it('discards a validated candidate when its authored document changes', async () => {
    const requests: string[] = []
    vi.stubGlobal('fetch', vi.fn(async (input: string) => {
      requests.push(input)
      return input.includes('/FormDefinition/') ? Response.json(authorForm) : Response.json({ valid: true })
    }))
    renderWorkflow()
    const input = await submitPack()
    await screen.findByText(/"valid": true/)
    fireEvent.change(input, { target: { value: '{' } })
    fireEvent.click(screen.getByRole('button', { name: 'Make bundle' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Draft check')
    expect(requests.some(path => path.endsWith('/packs/export'))).toBe(false)
  })

  it('uses the fulfilled record evidence and exact receipt route', async () => {
    const requests: string[] = []
    vi.stubGlobal('URL', { ...URL, createObjectURL: vi.fn(() => 'blob:round3'), revokeObjectURL: vi.fn() })
    vi.stubGlobal('fetch', vi.fn(async (input: string, init?: RequestInit) => {
      requests.push(input)
      if (input.includes('/FormDefinition/platform.pack.author')) return Response.json(authorForm)
      if (input.endsWith('/packs/export?validateOnly=true')) return Response.json({ valid: true })
      if (input.endsWith('/packs/export')) return new Response('pack', { status: 200 })
      if (input.endsWith('/packs/verify')) return Response.json({ verdict: 'Verified' })
      if (input.endsWith('/packs/install')) return Response.json({ installed: true })
      if (input.endsWith('/packs/activate')) return Response.json({ activated: true, projectionRefusals: [], platformRefusals: [] })
      if (input.endsWith('/asset-registry/types/acme.asset')) return Response.json({ id: 'acme.asset', displayName: 'Asset', propertyForm: { definition: 'acme.capture', version: '2' } })
      if (input.includes('/FormDefinition/acme.capture')) return Response.json(propertyForm)
      if (input.endsWith('/asset-registry/entities') && init?.method === 'POST') return Response.json({ id: 'entity 8', auditId: 'audit 8' }, { status: 201 })
      if (input.endsWith('/asset-registry/entities/entity%208')) return Response.json({ id: 'entity 8', displayName: 'Evidence asset' })
      if (input.endsWith('/authorization/traces/audit%208')) return Response.json({ auditId: 'audit 8', outcome: 'Allowed' })
      return Response.json({})
    }))
    renderWorkflow()
    await submitPack(); await screen.findByText(/"valid": true/)
    fireEvent.click(screen.getByRole('button', { name: 'Make bundle' })); await screen.findByRole('link')
    fireEvent.click(screen.getByRole('button', { name: 'Check signature' })); await screen.findByText(/"verdict": "Verified"/)
    fireEvent.click(screen.getByRole('button', { name: 'Stage bundle' }))
    await screen.findByText(/"installed": true/)
    fireEvent.click(screen.getByRole('button', { name: 'Go live' }))
    await screen.findByText(/"activated": true/)
    fireEvent.click(screen.getByRole('button', { name: 'Add item' }))
    const title = await screen.findByRole('textbox', { name: 'Asset title' })
    fireEvent.change(title, { target: { value: 'Record asset' } })
    fireEvent.click(screen.getAllByRole('button', { name: 'Add item' }).at(-1)!)
    await screen.findByText(/"id": "entity 8"/)
    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    await waitFor(() => expect(requests).toContain('/api/local-node/asset-registry/entities/entity%208'))
    await waitFor(() => expect(screen.getByRole('region', { name: 'Workshop command results' })).toHaveTextContent('Evidence asset'))
    expect(requests).toContain('/api/local-node/asset-registry/entities/entity%208')
    expect(requests).toContain('/api/local-node/authorization/traces/audit%208')
    const create = requests.find(path => path.endsWith('/asset-registry/entities'))
    expect(create).toBeDefined()
  })
})
