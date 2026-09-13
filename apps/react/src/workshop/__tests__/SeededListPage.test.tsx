import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { StrictMode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { SeededListPage } from '../SeededListPage'

const literal = (value: string) => ({ kind: 'Literal', value })

const actionPlan = {
  definitionHash: 'hash', definitionId: 'platform.list.forms', definitionVersion: '1.0.0',
  packKey: 'harborline.platform', packVersion: '1.0.0', definitionKind: 'ViewDefinition',
  bindings: {
    viewKind: 'views.entity-list/grid',
    parameters: {
      entityType: 'FormDefinition',
      fields: [{ id: 'formId', label: 'Key' }, { id: 'title', label: 'Title' }],
      actions: [
        { id: 'author', label: 'Draft check', operation: 'pack.validate', inputForm: 'platform.pack.author' },
        { id: 'export', label: 'Make bundle', operation: 'pack.export' },
        { id: 'verify', label: 'Check signature', operation: 'pack.verify' },
        { id: 'install', label: 'Stage bundle', operation: 'pack.install' },
        { id: 'activate', label: 'Go live', operation: 'pack.activate' },
        { id: 'create', label: 'Add item', operation: 'record.create', input: 'active-pack.property-form' },
        { id: 'read', label: 'Inspect evidence', operation: 'record.read' },
      ],
    },
    actions: [
      { id: 'author', label: 'Draft check' }, { id: 'export', label: 'Make bundle' },
      { id: 'verify', label: 'Check signature' }, { id: 'install', label: 'Stage bundle' },
      { id: 'activate', label: 'Go live' }, { id: 'create', label: 'Add item' },
      { id: 'read', label: 'Inspect evidence' },
    ],
  },
}

const authorForm = {
  id: 'platform.pack.author', version: '1.0.0', status: 'Published',
  renderPlan: {
    definitionHash: 'author-hash', definitionId: 'platform.pack.author', definitionVersion: '1.0.0',
    packKey: 'harborline.platform', packVersion: '1.0.0', definitionKind: 'FormDefinition',
    bindings: {
      fields: { packJson: { type: 'textarea', required: true } },
      overlay: {
        title: literal('Author a pack'), description: literal('Paste the candidate.'),
        fields: { packJson: { label: literal('Pack document'), controlHint: 'textarea', piiSensitivity: 'None' } },
        sections: [{ id: 'document', title: literal('Pack'), fields: ['packJson'] }], rules: [],
      },
    },
  },
}

const propertyForm = {
  id: 'acme.capture', version: '2.0.0', status: 'Published',
  renderPlan: {
    definitionHash: 'property-hash', definitionId: 'acme.capture', definitionVersion: '2.0.0',
    packKey: 'acme.assets', packVersion: '1.0.0', definitionKind: 'FormDefinition',
    bindings: {
      fields: { title: { type: 'text', required: true }, count: { type: 'number', required: false } },
      overlay: {
        title: literal('Capture asset'), description: literal('Record an asset.'),
        fields: {
          title: { label: literal('Asset title'), controlHint: 'text', piiSensitivity: 'None' },
          count: { label: literal('Count'), controlHint: 'number', piiSensitivity: 'None' },
        },
        sections: [{ id: 'main', title: literal('Asset'), fields: ['title', 'count'] }], rules: [],
      },
    },
  },
}

const candidate = {
  key: 'acme.assets', version: '1.0.0', name: 'Acme assets', description: 'Fixture', scopeTier: 'Horizontal',
  contents: [
    { key: 'acme.asset', kind: 'AssetTypeDefinition', version: '1.0.0', content: { id: 'acme.asset', displayName: 'Asset', propertyFormBinding: 'acme.capture' } },
    { key: 'acme.capture', kind: 'FormDefinition', version: '2.0.0', content: {} },
  ],
  dependencies: [], capabilityRequirements: [],
}

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('seeded Workshop list', () => {
  it('loads the declared author form after StrictMode replays mount effects', async () => {
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      if (init?.signal?.aborted) throw new DOMException('The operation was aborted.', 'AbortError')
      if (input.includes('/ViewDefinition/')) return Response.json({ renderPlan: actionPlan })
      if (input.endsWith('/FormDefinition/platform.pack.author')) return Response.json(authorForm)
      return Response.json({ entries: [], kindsUnavailable: [] })
    })
    vi.stubGlobal('fetch', fetchMock)

    render(<StrictMode><SeededListPage itemId="forms" /></StrictMode>)
    await screen.findByRole('grid')
    fireEvent.click(screen.getByRole('button', { name: 'Draft check' }))

    expect(await screen.findByRole('textbox', { name: 'Pack document' })).toBeInTheDocument()
  })

  it('renders the compiled Forms plan and forwards its inspect action', async () => {
    const plan = { definitionHash: 'hash', definitionId: 'platform.list.forms', definitionVersion: '1.0.0', packKey: 'harborline.platform', packVersion: '1.0.0', definitionKind: 'ViewDefinition', bindings: { viewKind: 'views.entity-list/grid', parameters: { fields: [{ id: 'formId', label: 'Key' }, { id: 'title', label: 'Title' }, { id: 'version', label: 'Version' }, { id: 'cascadeLayer', label: 'Cascade layer' }] } } }
    const fetchMock = vi.fn(async (input: string) => new Response(JSON.stringify(input.includes('/ViewDefinition/')
      ? { renderPlan: plan }
      : { entries: [{ id: 'work-order', version: '1.0.0', status: 'Published', title: { defaultLocale: 'en', values: { en: 'Work order' } }, body: { cascadeLayer: 'Tenant' } }], kindsUnavailable: [] }), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)
    render(<SeededListPage itemId="forms" />)
    expect(await screen.findByRole('grid')).toHaveAttribute('aria-label', 'View results')
    expect(fetchMock.mock.calls.map(([input]) => input)).toEqual([
      '/api/local-node/catalogue/definitions/ViewDefinition/platform.list.forms',
      '/api/local-node/catalogue/definitions?kind=FormDefinition',
    ])
    expect(screen.getAllByRole('columnheader').map(node => node.textContent)).toEqual(['Key', 'Title', 'Version', 'Cascade layer'])
    expect(screen.getByText('work-order')).toBeInTheDocument()
    fireEvent.doubleClick(document.querySelector('[data-row-id="work-order@1.0.0"]')!)
    await waitFor(() => expect(screen.getByRole('complementary', { name: 'Definition inspector' })).toHaveTextContent('Work order'))
  })

  it('executes only seed-declared commands through the full pack and record workflow', async () => {
    const packBytes = new Uint8Array([1, 2, 3, 4])
    const requests: Array<{ url: string; init?: RequestInit }> = []
    let createAttempts = 0
    let activationAttempts = 0
    let listReads = 0
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      requests.push({ url: input, init })
      if (input.includes('/ViewDefinition/')) return Response.json({ renderPlan: actionPlan })
      if (input === '/api/local-node/catalogue/definitions?kind=FormDefinition') {
        listReads += 1
        return Response.json({
          entries: listReads === 1 ? [] : [{ id: 'acme.capture', version: '2.0.0', status: 'Published', title: { defaultLocale: 'en', values: { en: 'Capture asset' } } }],
          kindsUnavailable: [],
        })
      }
      if (input.endsWith('/FormDefinition/platform.pack.author')) return Response.json(authorForm)
      if (input.endsWith('/packs/export?validateOnly=true')) return Response.json({ valid: true, codes: [] })
      if (input.endsWith('/packs/export')) return new Response(packBytes, { status: 200, headers: { 'content-type': 'application/octet-stream', 'content-disposition': 'attachment; filename="acme.assets-1.0.0.pack"' } })
      if (input.endsWith('/packs/verify')) return Response.json({ verdict: 'Verified', details: [], manifestKey: 'acme.assets' })
      if (input.endsWith('/packs/install')) return Response.json({ installed: true, action: 'Installed', packKey: 'acme.assets', version: '1.0.0' })
      if (input.endsWith('/packs/activate')) {
        activationAttempts += 1
        return Response.json({
          activated: true, packKey: 'acme.assets', version: '1.0.0',
          projectionRefusals: activationAttempts === 1 ? [] : [{ code: 'pack.form.invalid', pointer: '/contents/1' }],
          platformRefusals: [],
        })
      }
      if (input.endsWith('/asset-registry/types/acme.asset')) return Response.json({ id: 'acme.asset', displayName: 'Asset', propertyForm: { definition: 'acme.capture', version: '2.0.0' } })
      if (input.endsWith('/FormDefinition/acme.capture?version=2.0.0')) return Response.json(propertyForm)
      if (input.endsWith('/asset-registry/entities') && init?.method === 'POST') {
        createAttempts += 1
        if (createAttempts === 1) return new Response('{"code":"entity.validation.body_invalid","pointers":["/values/title"]}', { status: 422 })
        if (createAttempts === 2) return Response.json({ id: ' ', auditId: 'audit-9' }, { status: 201 })
        if (createAttempts === 3) return Response.json({ id: 'entity-7', auditId: 'audit-9' }, { status: 201 })
        return new Response('{"code":"unexpected_duplicate_write","pointers":[]}', { status: 422 })
      }
      if (input.endsWith('/asset-registry/entities/entity-7')) return Response.json({ id: 'entity-7', type: 'acme.asset', displayName: 'Dock asset', values: { title: 'Dock asset', count: 2 } })
      if (input.endsWith('/authorization/traces/audit-9')) return Response.json({ auditId: 'audit-9', outcome: 'Allowed', stages: [{ stage: 'grant', result: 'allow' }] })
      return new Response('unexpected request', { status: 500 })
    })
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('URL', { ...URL, createObjectURL: vi.fn(() => 'blob:pack'), revokeObjectURL: vi.fn() })

    render(<SeededListPage itemId="forms" />)
    await screen.findByRole('grid')
    expect(screen.queryByRole('button', { name: 'Export signed pack' })).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Draft check' }))
    const packInput = await screen.findByRole('textbox', { name: 'Pack document' })
    fireEvent.change(packInput, { target: { value: JSON.stringify(candidate) } })
    fireEvent.click(screen.getAllByRole('button', { name: 'Draft check' }).at(-1)!)
    await screen.findByText(/"valid": true/)

    fireEvent.click(screen.getByRole('button', { name: 'Make bundle' }))
    const download = await screen.findByRole('link', { name: 'acme.assets-1.0.0.pack' })
    expect(download).toHaveAttribute('href', 'blob:pack')
    fireEvent.click(screen.getByRole('button', { name: 'Check signature' }))
    await screen.findByText(/"verdict": "Verified"/)
    fireEvent.click(screen.getByRole('button', { name: 'Stage bundle' }))
    await screen.findByText(/"installed": true/)
    fireEvent.click(screen.getByRole('button', { name: 'Go live' }))
    await screen.findByText(/"activated": true/)
    await waitFor(() => expect(document.querySelector('[data-row-id="acme.capture@2.0.0"]')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: 'Add item' }))
    const title = await screen.findByRole('textbox', { name: 'Asset title' })
    fireEvent.change(title, { target: { value: 'Dock asset' } })
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Count' }), { target: { value: '2' } })
    fireEvent.click(screen.getAllByRole('button', { name: 'Add item' }).at(-1)!)
    expect((await screen.findAllByText(/entity.validation.body_invalid/)).length).toBeGreaterThan(0)
    expect(title).toHaveAttribute('aria-invalid', 'true')
    fireEvent.click(screen.getAllByRole('button', { name: 'Add item' }).at(-1)!)
    expect((await screen.findAllByText(/"id": " "/)).length).toBeGreaterThan(0)
    const readsBeforeReceipt = requests.filter(entry => entry.url.includes('/asset-registry/entities/')).length
    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    expect(requests.filter(entry => entry.url.includes('/asset-registry/entities/'))).toHaveLength(readsBeforeReceipt)
    fireEvent.click(screen.getAllByRole('button', { name: 'Add item' }).at(-1)!)
    await screen.findByText(/"id": "entity-7"/)

    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    await screen.findByText(/"outcome": "Allowed"/)

    const request = (suffix: string) => requests.find(entry => entry.url.endsWith(suffix))!
    expect(JSON.parse(String(request('/packs/export?validateOnly=true').init?.body))).toEqual(candidate)
    expect(JSON.parse(String(request('/packs/activate').init?.body))).toEqual({ packKey: 'acme.assets', version: '1.0.0' })
    expect(JSON.parse(String(request('/asset-registry/entities').init?.body))).toEqual({
      type: 'acme.asset', displayName: 'Dock asset', values: { title: 'Dock asset', count: 2 },
    })
    const verifyBody = request('/packs/verify').init?.body as Blob
    const installBody = request('/packs/install').init?.body as Blob
    expect({ size: verifyBody.size, type: verifyBody.type }).toEqual({ size: 4, type: 'application/octet-stream' })
    expect(Array.from(new Uint8Array(await verifyBody.arrayBuffer()))).toEqual([1, 2, 3, 4])
    expect({ size: installBody.size, type: installBody.type }).toEqual({ size: 4, type: 'application/octet-stream' })
    expect(Array.from(new Uint8Array(await installBody.arrayBuffer()))).toEqual([1, 2, 3, 4])

    fireEvent.click(screen.getByRole('button', { name: 'Go live' }))
    await screen.findByText(/Activation completed with projection refusals/)
    const writesAfterRefusal = requests.filter(entry => entry.url.endsWith('/asset-registry/entities') && entry.init?.method === 'POST').length
    fireEvent.click(screen.getAllByRole('button', { name: 'Add item' }).at(-1)!)
    expect(requests.filter(entry => entry.url.endsWith('/asset-registry/entities') && entry.init?.method === 'POST')).toHaveLength(writesAfterRefusal)
    const entityReadCount = requests.filter(entry => entry.url.endsWith('/asset-registry/entities/entity-7')).length
    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    expect((await screen.findAllByRole('alert')).some(alert => alert.textContent?.includes('Add item'))).toBe(true)
    expect(requests.filter(entry => entry.url.endsWith('/asset-registry/entities/entity-7'))).toHaveLength(entityReadCount)

    fireEvent.click(screen.getByRole('button', { name: 'Draft check' }))
    const nextPackInput = await screen.findByRole('textbox', { name: 'Pack document' })
    expect(nextPackInput).toHaveValue(JSON.stringify(candidate))
    fireEvent.change(nextPackInput, { target: { value: '{' } })
    fireEvent.click(screen.getAllByRole('button', { name: 'Draft check' }).at(-1)!)
    expect(screen.queryByRole('link', { name: 'acme.assets-1.0.0.pack' })).not.toBeInTheDocument()
  })

  it('guards command prerequisites without making an undeclared request', async () => {
    const fetchMock = vi.fn(async (input: string) => Response.json(input.includes('/ViewDefinition/')
      ? { renderPlan: actionPlan }
      : { entries: [], kindsUnavailable: [] }))
    vi.stubGlobal('fetch', fetchMock)
    render(<SeededListPage itemId="forms" />)
    await screen.findByRole('grid')

    fireEvent.click(screen.getByRole('button', { name: 'Make bundle' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Draft check')
    expect(fetchMock.mock.calls.some(([input]) => String(input).endsWith('/packs/export'))).toBe(false)
  })

  it('drops retained workflow state when the compiled view kind becomes unsupported', async () => {
    let unsupported = false
    const fetchMock = vi.fn(async (input: string) => {
      if (input.includes('/ViewDefinition/')) return Response.json({
        renderPlan: unsupported ? { ...actionPlan, definitionKind: 'UnknownDefinition' } : actionPlan,
      })
      if (input.includes('/FormDefinition/platform.pack.author')) return Response.json(authorForm)
      return Response.json({ entries: [], kindsUnavailable: [] })
    })
    vi.stubGlobal('fetch', fetchMock)
    const view = render(<SeededListPage itemId="forms" />)
    await screen.findByRole('grid')
    fireEvent.click(screen.getByRole('button', { name: 'Draft check' }))
    await screen.findByRole('textbox', { name: 'Pack document' })

    unsupported = true
    view.rerender(<SeededListPage itemId="asset-types" />)

    await waitFor(() => expect(screen.queryByRole('textbox', { name: 'Pack document' })).not.toBeInTheDocument())
    expect(screen.queryByRole('button', { name: 'Draft check' })).not.toBeInTheDocument()
    expect(fetchMock.mock.calls.filter(([input]) => String(input).includes('/packs/'))).toHaveLength(0)
  })
})
