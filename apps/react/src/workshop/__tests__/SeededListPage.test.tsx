import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { StrictMode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { SeededListPage } from '../SeededListPage'

const literal = (value: string) => ({ kind: 'Literal', value })

const actionPlan = {
  definitionHash: 'hash', definitionId: 'platform.list.forms', definitionVersion: '1.0.0',
  packKey: 'harborline.platform', packVersion: '1.0.0', definitionKind: 'ViewDefinition',
  bindings: {
    viewKind: 'layout.table',
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

// Public wire literals: API Catalogue.Map and RequestAuthorization.Write at API 2496ab5d.
// The renderer owns the public copy; Diagnostic and tenant/owner members are not public fields.
const catalogueReadRefusal = {
  code: 'authorization.permission_required', permission: 'catalogue:read',
  title: 'You do not have permission for this action.',
  detail: 'The action was refused because a required permission is missing.',
  remediation: 'Ask an administrator to grant the permission this action requires.',
  auditId: '11111111-1111-1111-1111-111111111111',
  correlationId: '22222222-2222-2222-2222-222222222222',
  Diagnostic: 'classified-diagnostic', tenant: 'private-tenant', owner: 'private-owner',
  pointers: ['/private-field'], trace: ['private-stage'],
}

async function openRecord(create: () => Response | Promise<Response>, read?: (path: string) => Response | Promise<Response>) {
  const requests: string[] = []
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => {
    requests.push(path)
    if (path.includes('/ViewDefinition/')) return Response.json({ renderPlan: actionPlan })
    if (path === '/api/local-node/catalogue/definitions?kind=FormDefinition') return Response.json({ entries: [], kindsUnavailable: [] })
    if (path.endsWith('/FormDefinition/platform.pack.author')) return Response.json(authorForm)
    if (path.endsWith('/packs/export?validateOnly=true')) return Response.json({ valid: true })
    if (path.endsWith('/packs/export')) return new Response(new Uint8Array([1]), { headers: { 'content-type': 'application/octet-stream' } })
    if (path.endsWith('/packs/verify')) return Response.json({ verdict: 'Verified' })
    if (path.endsWith('/packs/install')) return Response.json({ installed: true })
    if (path.endsWith('/packs/activate')) return Response.json({ activated: true })
    if (path.endsWith('/asset-registry/types/acme.asset')) return Response.json({ id: 'acme.asset', displayName: 'Asset', propertyForm: { definition: 'acme.capture', version: '2.0.0' } })
    if (path.endsWith('/FormDefinition/acme.capture?version=2.0.0')) return Response.json(propertyForm)
    if (path.endsWith('/asset-registry/entities') && init?.method === 'POST') return create()
    if (read && (path.includes('/asset-registry/entities/') || path.includes('/authorization/traces/'))) return read(path)
    return new Response('unexpected request', { status: 500 })
  }))
  vi.stubGlobal('URL', { ...URL, createObjectURL: vi.fn(() => 'blob:pack'), revokeObjectURL: vi.fn() })
  const cut = render(<SeededListPage itemId="forms" />)
  fireEvent.click(await screen.findByRole('button', { name: 'Draft check' }))
  fireEvent.change(await screen.findByRole('textbox', { name: 'Pack document' }), { target: { value: JSON.stringify(candidate) } })
  fireEvent.click(screen.getAllByRole('button', { name: 'Draft check' }).at(-1)!)
  await screen.findByText(/"valid": true/)
  fireEvent.click(screen.getByRole('button', { name: 'Make bundle' }))
  await screen.findByRole('link')
  fireEvent.click(screen.getByRole('button', { name: 'Check signature' }))
  await screen.findByText(/"verdict": "Verified"/)
  fireEvent.click(screen.getByRole('button', { name: 'Stage bundle' }))
  await screen.findByText(/"installed": true/)
  fireEvent.click(screen.getByRole('button', { name: 'Go live' }))
  await screen.findByText(/"activated": true/)
  fireEvent.click(screen.getByRole('button', { name: 'Add item' }))
  const title = await screen.findByRole('textbox', { name: 'Asset title' })
  fireEvent.change(title, { target: { value: 'Dock asset' } })
  return { cut, requests, title, submit: () => fireEvent.click(screen.getAllByRole('button', { name: 'Add item' }).at(-1)!) }
}

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('seeded Workshop list', () => {
  it.each(['health', 'browse'])('loads the exact declared %s view and preserves ordered fields', async surface => {
    const viewId = `platform.${surface}.forms`
    const plan = { ...actionPlan, definitionId: viewId, bindings: { ...actionPlan.bindings,
      parameters: { fields: [{ id: 'status', label: 'Lifecycle' }, { id: 'formId', label: 'Key' }] }, actions: [],
    } }
    const fetchMock = vi.fn(async (input: string) => Response.json(input.includes('/ViewDefinition/')
      ? { renderPlan: plan }
      : { entries: [{ id: 'inspection', version: '1.0.0', status: 'Published' }], kindsUnavailable: [] }))
    vi.stubGlobal('fetch', fetchMock)
    const restored = vi.fn()
    render(<SeededListPage itemId="forms" {...{ viewId }} selectedRowId="inspection@1.0.0" onSelectionRestored={restored} />)
    await screen.findByRole('grid')
    expect(fetchMock.mock.calls.map(([input]) => input)).toEqual([
      `/api/local-node/catalogue/definitions/ViewDefinition/${viewId}`,
      '/api/local-node/catalogue/definitions?kind=FormDefinition',
    ])
    expect(screen.getAllByRole('columnheader').map(node => node.textContent)).toEqual(['Lifecycle', 'Key'])
    await waitFor(() => expect(restored).toHaveBeenCalledWith(expect.objectContaining({ id: 'inspection@1.0.0' })))
  })

  it.each([[0], ['FormDefinition']])('keeps an explicitly unavailable catalogue distinct from an available empty list: %j', async unavailable => {
    vi.stubGlobal('fetch', vi.fn(async (input: string) => Response.json(input.includes('/ViewDefinition/')
      ? { renderPlan: actionPlan }
      : { entries: [], kindsUnavailable: [unavailable] })))
    const view = render(<SeededListPage itemId="forms" />)
    expect(await screen.findByText('FormDefinition is unavailable in this host.')).toBeInTheDocument()
    expect(view.container.querySelector('button,form,[role=grid]')).toBeNull()
    expect(screen.queryByText('No definitions.')).not.toBeInTheDocument()
  })

  it('keeps catalogue coordinates and provenance outside the mutable body', async () => {
    const entry = { id: 'inspection', version: '2.0.0', kind: 0, status: 'Published', sealed: true,
      provenance: { packKey: 'acme.assets', packVersion: '1.0.0', kind: 'pack' },
      definitionHash: 'sha256:definition', updatedAt: '2026-09-15T00:00:00Z',
      body: { id: 'body-id', version: 'body-version', provenance: { kind: 'body' } } }
    vi.stubGlobal('fetch', vi.fn(async (input: string) => Response.json(input.includes('/ViewDefinition/')
      ? { renderPlan: actionPlan } : { entries: [entry], kindsUnavailable: [] })))
    const restored = vi.fn()
    render(<SeededListPage itemId="forms" selectedRowId="inspection@2.0.0" onSelectionRestored={restored} />)
    await waitFor(() => expect(restored).toHaveBeenCalledWith(expect.objectContaining({
      id: 'inspection@2.0.0', version: '2.0.0', provenance: entry.provenance, catalogue: entry, body: entry.body,
    })))
  })

  it.each(['forms', 'reports', 'views', 'data-exchanges', 'schedules']
    .flatMap(item => ['health', 'browse'].flatMap(surface => [403, 404].map(status => ({ item, surface, status })))))
    ('keeps $item $surface absent when its seed returns $status', async ({ item, surface, status }) => {
    const viewId = `platform.${surface}.${item}`
    const fetchMock = vi.fn(async (input: string) => input.includes('/ViewDefinition/')
      ? new Response('View refused or absent', { status }) : Response.json({ entries: [], kindsUnavailable: [] }))
    vi.stubGlobal('fetch', fetchMock)
    render(<SeededListPage itemId={item} viewId={viewId} />)
    expect(await screen.findByRole('alert')).toHaveTextContent(String(status))
    expect(screen.queryByRole('grid')).toBeNull()
    expect(screen.queryByRole('button')).toBeNull()
    expect(fetchMock.mock.calls.filter(([input]) => input.includes('/ViewDefinition/')).map(([input]) => input))
      .toEqual([`/api/local-node/catalogue/definitions/ViewDefinition/${viewId}`])
  })

  it('keeps an available empty view actionable and suppresses restoration for an inert plan', async () => {
    let inert = false
    vi.stubGlobal('fetch', vi.fn(async (input: string) => Response.json(input.includes('/ViewDefinition/')
      ? { renderPlan: inert ? { ...actionPlan, definitionKind: 'UnknownDefinition' } : actionPlan }
      : { entries: inert ? [{ id: 'inspection', version: '1.0.0', status: 'Published' }] : [], kindsUnavailable: [] })))
    const restored = vi.fn()
    const cut = render(<SeededListPage itemId="forms" viewId="platform.health.forms" />)
    expect(await screen.findByText('No definitions.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Draft check' })).toBeEnabled()
    inert = true
    cut.rerender(<SeededListPage itemId="forms" viewId="platform.browse.forms" selectedRowId="inspection@1.0.0" onSelectionRestored={restored} />)
    await waitFor(() => expect(cut.container.innerHTML).toBe(''))
    expect(restored).not.toHaveBeenCalled()
  })

  it.each(['view', 'list'])('preserves only public refusal fields from a failed catalogue %s read', async target => {
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      expect(init?.credentials).toBe('include')
      expect(init?.signal).toBeInstanceOf(AbortSignal)
      if (input.includes('/ViewDefinition/') === (target === 'view')) return Response.json(catalogueReadRefusal, { status: 403 })
      return Response.json(input.includes('/ViewDefinition/') ? { renderPlan: actionPlan }
        : { entries: [{ id: 'inspection', version: '1.0.0', status: 'Published' }], kindsUnavailable: [] })
    })
    vi.stubGlobal('fetch', fetchMock)
    const restored = vi.fn()
    const cut = render(<SeededListPage itemId="forms" selectedRowId="inspection@1.0.0" onSelectionRestored={restored} />)
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('authorization.permission_required')
    expect(alert).toHaveTextContent('catalogue:read')
    expect(alert).toHaveTextContent('You do not have permission for this action.')
    expect(alert).toHaveTextContent('The action was refused because a required permission is missing.')
    expect(alert).toHaveTextContent('Ask an administrator to grant the permission this action requires.')
    expect(alert).toHaveTextContent('Audit ID: 11111111-1111-1111-1111-111111111111')
    expect(alert).toHaveTextContent('Correlation ID: 22222222-2222-2222-2222-222222222222')
    expect(alert.textContent).not.toMatch(/classified-diagnostic|private-tenant|private-owner|private-field|private-stage/)
    expect(cut.container.querySelector('button,form,[role=grid],a')).toBeNull()
    expect(restored).not.toHaveBeenCalled()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it.each([
    { status: 400, code: 'catalogue.kind_unknown' },
    { status: 404, code: 'catalogue.definition_not_found' },
  ])('preserves the existing catalogue $status code without unrelated payload members', async ({ status, code }) => {
    vi.stubGlobal('fetch', vi.fn(async (input: string) => input.includes('/ViewDefinition/')
      ? Response.json({ code, detail: 'private-detail', Diagnostic: 'private-diagnostic' }, { status })
      : Response.json({ entries: [], kindsUnavailable: [] })))
    const cut = render(<SeededListPage itemId="forms" />)
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(code)
    expect(alert.textContent).not.toMatch(/private-detail|private-diagnostic/)
    expect(cut.container.querySelector('button,form,[role=grid]')).toBeNull()
  })

  it('retains a server-owned authorization reason and does not invent absent receipt identifiers', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: string) => input.includes('/ViewDefinition/')
      ? Response.json({ code: 'authorization.grant.attenuation_failed', permission: 'catalogue:read',
        title: 'You do not have permission for this action.',
        detail: 'Further details of this refusal are withheld because they are not yours to read.',
        remediation: 'Ask an administrator to grant the permission this action requires.' }, { status: 403 })
      : Response.json({ entries: [], kindsUnavailable: [] })))
    render(<SeededListPage itemId="forms" />)
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('authorization.grant.attenuation_failed')
    expect(alert).toHaveTextContent('Further details of this refusal are withheld because they are not yours to read.')
    expect(alert.textContent).not.toMatch(/Audit ID:|Correlation ID:|11111111|22222222/)
  })

  it.each([
    'private raw response', '{"code":', 'null', '[]',
    '{"code":"future.unknown","detail":"private-detail"}',
    JSON.stringify({ ...catalogueReadRefusal, permission: 'records:read' }),
    JSON.stringify({ ...catalogueReadRefusal, detail: { owner: 'private-owner' } }),
    JSON.stringify({ ...catalogueReadRefusal, auditId: 'private-identifier' }),
    JSON.stringify({ ...catalogueReadRefusal, correlationId: null }),
  ])('uses status fallback for an unrecognized or malformed refusal body: %s', async body => {
    vi.stubGlobal('fetch', vi.fn(async (input: string) => input.includes('/ViewDefinition/')
      ? new Response(body, { status: 403 }) : Response.json({ entries: [], kindsUnavailable: [] })))
    const cut = render(<SeededListPage itemId="forms" />)
    expect(await screen.findByRole('alert')).toHaveTextContent(/^Workshop catalogue request failed \(403\)\.$/)
    expect(cut.container.querySelector('button,form,[role=grid]')).toBeNull()
  })

  it('renders public refusal text as text, without interpreting markup', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: string) => input.includes('/ViewDefinition/')
      ? Response.json({ ...catalogueReadRefusal, detail: '<img src="private" onerror="alert(1)">' }, { status: 403 })
      : Response.json({ entries: [], kindsUnavailable: [] })))
    const cut = render(<SeededListPage itemId="forms" />)
    expect(await screen.findByRole('alert')).toHaveTextContent('<img src="private" onerror="alert(1)">')
    expect(cut.container.querySelector('img')).toBeNull()
  })

  it('discards a previous surface refusal and cancels its requests when the surface changes', async () => {
    let finish!: (response: Response) => void
    const pending = new Promise<Response>(resolve => { finish = resolve })
    const signals: AbortSignal[] = []
    vi.stubGlobal('fetch', vi.fn(async (input: string, init?: RequestInit) => {
      if (init?.signal) signals.push(init.signal)
      if (input.endsWith('/platform.health.forms')) return pending
      return Response.json(input.includes('/ViewDefinition/')
        ? { renderPlan: { ...actionPlan, definitionId: 'platform.browse.forms' } } : { entries: [], kindsUnavailable: [] })
    }))
    const cut = render(<SeededListPage itemId="forms" viewId="platform.health.forms" />)
    cut.rerender(<SeededListPage itemId="forms" viewId="platform.browse.forms" />)
    await screen.findByRole('grid')
    expect(signals.slice(0, 2).every(signal => signal.aborted)).toBe(true)
    await act(async () => finish(Response.json(catalogueReadRefusal, { status: 403 })))
    expect(screen.queryByRole('alert')).toBeNull()
    expect(cut.container.textContent).not.toContain('11111111-1111-1111-1111-111111111111')
  })

  it('ignores a late response for the previously selected surface', async () => {
    let finish!: (response: Response) => void
    const pending = new Promise<Response>(resolve => { finish = resolve })
    vi.stubGlobal('fetch', vi.fn(async (input: string) => {
      if (input.endsWith('/platform.health.forms')) return pending
      if (input.includes('/ViewDefinition/')) return Response.json({ renderPlan: { ...actionPlan, definitionId: 'platform.browse.forms' } })
      return Response.json({ entries: [], kindsUnavailable: [] })
    }))
    const cut = render(<SeededListPage itemId="forms" viewId="platform.health.forms" />)
    cut.rerender(<SeededListPage itemId="forms" viewId="platform.browse.forms" />)
    await screen.findByRole('grid')
    await act(async () => finish(Response.json({ renderPlan: { ...actionPlan, definitionId: 'platform.health.forms' } })))
    expect(cut.container.querySelector('[data-definition-source]')).toHaveAttribute('data-definition-source', expect.stringContaining('platform.browse.forms'))
  })

  it.each([false, true])('disables declared actions during a delayed request and restores them after refusal=%s', async refuse => {
    let release!: () => void
    const pending = new Promise<void>(resolve => { release = resolve })
    let formReads = 0
    vi.stubGlobal('fetch', vi.fn(async (input: string) => {
      if (input.includes('/ViewDefinition/')) return Response.json({ renderPlan: actionPlan })
      if (input.endsWith('/FormDefinition/platform.pack.author')) {
        formReads += 1
        await pending
        if (refuse && formReads === 1) return new Response('Form temporarily unavailable', { status: 503 })
        return Response.json(authorForm)
      }
      return Response.json({ entries: [], kindsUnavailable: [] })
    }))
    const view = render(<SeededListPage itemId="forms" />)
    await screen.findByRole('grid')
    const actionButtons = () => [...view.container.querySelectorAll<HTMLButtonElement>('.hl-view-runtime__actions button')]
    expect(actionButtons()).toHaveLength(7)
    fireEvent.click(screen.getByRole('button', { name: 'Draft check' }))
    await waitFor(() => {
      for (const button of actionButtons()) {
        expect(button).toBeDisabled()
        expect(button).toHaveAttribute('aria-disabled', 'true')
      }
    })
    fireEvent.click(screen.getByRole('button', { name: 'Draft check' }))
    expect(formReads).toBe(1)
    await act(async () => release())
    await waitFor(() => { for (const button of actionButtons()) expect(button).toBeEnabled() })
    if (refuse) expect(await screen.findByRole('alert')).toHaveTextContent('Form temporarily unavailable')
    else expect(await screen.findByRole('textbox', { name: 'Pack document' })).toBeInTheDocument()
    fireEvent.click(within(screen.getByRole('group', { name: 'View results' })).getByRole('button', { name: 'Draft check' }))
    await waitFor(() => expect(formReads).toBe(2))
    expect(await screen.findByRole('textbox', { name: 'Pack document' })).toBeInTheDocument()
    await waitFor(() => { for (const button of actionButtons()) expect(button).toBeEnabled() })
  })

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

  it('restores selection without activation, then forwards an explicit inspect action on the same row', async () => {
    const plan = { definitionHash: 'hash', definitionId: 'platform.list.forms', definitionVersion: '1.0.0', packKey: 'harborline.platform', packVersion: '1.0.0', definitionKind: 'ViewDefinition', bindings: { viewKind: 'layout.table', parameters: { fields: [{ id: 'formId', label: 'Key' }, { id: 'title', label: 'Title' }, { id: 'version', label: 'Version' }, { id: 'cascadeLayer', label: 'Cascade layer' }] } } }
    const fetchMock = vi.fn(async (input: string) => new Response(JSON.stringify(input.includes('/ViewDefinition/')
      ? { renderPlan: plan }
      : { entries: [{ id: 'work-order', version: '1.0.0', status: 'Published', title: { defaultLocale: 'en', values: { en: 'Work order' } }, body: { cascadeLayer: 'Tenant' } }], kindsUnavailable: [] }), { status: 200 }))
    const activated = vi.fn()
    const restored = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    render(<SeededListPage itemId="forms" selectedRowId="work-order@1.0.0" onRowActivate={activated} onSelectionRestored={restored} />)
    expect(await screen.findByRole('grid')).toHaveAttribute('aria-label', 'View results')
    expect(fetchMock.mock.calls.map(([input]) => input)).toEqual([
      '/api/local-node/catalogue/definitions/ViewDefinition/platform.list.forms',
      '/api/local-node/catalogue/definitions?kind=FormDefinition',
    ])
    expect(screen.getAllByRole('columnheader').map(node => node.textContent)).toEqual(['Key', 'Title', 'Version', 'Cascade layer'])
    expect(screen.getByText('work-order')).toBeInTheDocument()
    await waitFor(() => expect(restored).toHaveBeenCalledWith(expect.objectContaining({ id: 'work-order@1.0.0' })))
    expect(activated).not.toHaveBeenCalled()
    fireEvent.doubleClick(document.querySelector('[data-row-id="work-order@1.0.0"]')!)
    await waitFor(() => expect(activated).toHaveBeenCalledWith(expect.objectContaining({ id: 'work-order@1.0.0', title: 'Work order', cascadeLayer: 'Tenant' })))
  })

  it.each([false, true])('executes seed-declared commands and keeps a refresh refusal closed=%s', async refuseRefresh => {
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
        if (refuseRefresh && listReads > 1) return Response.json(catalogueReadRefusal, { status: 403 })
        return Response.json({
          entries: listReads === 1 && !refuseRefresh ? [] : [{ id: 'acme.capture', version: '2.0.0', status: 'Published', title: { defaultLocale: 'en', values: { en: 'Capture asset' } } }],
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
        if (createAttempts === 1) return Response.json({ stage: 'validate', refusals: [{ code: 'entity.validation.body_invalid', pointer: '/title' }] }, { status: 409 })
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

    const restored = vi.fn()
    const cut = render(<SeededListPage itemId="forms" onSelectionRestored={restored} />)
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
    if (refuseRefresh) {
      expect(await screen.findByRole('alert')).toHaveTextContent('authorization.permission_required')
      expect(screen.getByRole('alert')).toHaveTextContent('Audit ID: 11111111-1111-1111-1111-111111111111')
      expect(screen.queryByRole('grid')).toBeNull()
      expect(screen.queryByRole('button')).toBeNull()
      expect(screen.queryByRole('link')).toBeNull()
      expect(requests.some(request => request.url.includes('/authorization/traces/'))).toBe(false)
      cut.rerender(<SeededListPage itemId="forms" selectedRowId="acme.capture@2.0.0" onSelectionRestored={restored} />)
      expect(restored).not.toHaveBeenCalled()
      return
    }
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
    await waitFor(() => expect(createAttempts).toBe(2))
    await waitFor(() => expect(screen.queryByText('Working…')).not.toBeInTheDocument())
    expect(await screen.findByRole('alert')).toHaveTextContent('The node accepted the request but returned no complete record and audit receipt. The record may exist; do not submit again without checking.')
    expect(screen.queryByText(/"id": " "/)).not.toBeInTheDocument()
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

  it.each([
    'entity.validation.body_invalid', 'entity.validation.schema_unknown',
    'entity.validation.rule_refused', 'entity.validation.rules_unavailable',
  ])('projects only canonical Records diagnostics for %s', async code => {
    const { cut, requests, title, submit } = await openRecord(() => Response.json({
      stage: 'validate', auditId: 'refusal-audit-7',
      refusals: [{ code, pointer: '/title', message: 'private-rule-message', target: 'hidden-definition', detail: { observed: 'private-sensitive-value' } }],
      message: 'private-exception-stack', trace: 'hidden-audit', submitted: 'rejected-token',
    }, { status: 409 }))
    submit()
    expect(await screen.findByRole('alert')).toHaveTextContent('Request refused.')
    await screen.findByText(/"stage": "validate"/)
    expect(title).toHaveAttribute('aria-invalid', 'true')
    const result = screen.getAllByRole('article').at(-1)!
    expect(JSON.parse(within(result).getByText(/"stage": "validate"/).textContent!)).toEqual({
      stage: 'validate', refusals: [{ code, pointer: '/title' }], auditId: 'refusal-audit-7',
    })
    expect(cut.container.textContent).not.toMatch(/private-|hidden-|rejected-token/)
    expect(cut.container.querySelector('a[href*="definition"]')).toBeNull()
    const count = requests.length
    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    expect(requests).toHaveLength(count)
    expect(requests.filter(path => path.includes('/FormDefinition/'))).toEqual([
      '/api/local-node/catalogue/definitions/FormDefinition/platform.pack.author',
      '/api/local-node/catalogue/definitions/FormDefinition/acme.capture?version=2.0.0',
    ])
  })

  it.each(['', '/', '/nested/title', '/items/0/title', '/a~1b~0c', '/values/title'])
    ('preserves actual document-relative RFC6901 pointer %j without a definition fetch', async pointer => {
    const { requests, submit } = await openRecord(() => Response.json({
      stage: 'validate', refusals: [{ code: 'entity.validation.body_invalid', pointer }],
    }, { status: 409 }))
    submit()
    const result = await screen.findByText(/"stage": "validate"/)
    expect(JSON.parse(result.textContent!)).toEqual({
      stage: 'validate', refusals: [{ code: 'entity.validation.body_invalid', pointer }],
    })
    expect(requests.filter(path => path.includes('/FormDefinition/'))).toHaveLength(2)
  })

  it('keeps individual refusal locations and an omitted refusal audit receipt', async () => {
    const { submit } = await openRecord(() => Response.json({ stage: 'validate', refusals: [
      { code: 'entity.validation.body_invalid', pointer: '/title' },
      { code: 'entity.validation.body_invalid', pointer: '/count' },
    ] }, { status: 409 }))
    submit()
    const result = await screen.findByText(/"stage": "validate"/)
    expect(JSON.parse(result.textContent!)).toEqual({ stage: 'validate', refusals: [
      { code: 'entity.validation.body_invalid', pointer: '/title' },
      { code: 'entity.validation.body_invalid', pointer: '/count' },
    ] })
    expect(screen.getByRole('textbox', { name: 'Asset title' })).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByRole('spinbutton', { name: 'Count' })).toHaveAttribute('aria-invalid', 'true')
  })

  const canonical = { stage: 'validate', refusals: [{ code: 'entity.validation.body_invalid', pointer: '/title' }] }
  const malformed: readonly { name: string; body: unknown; status?: number }[] = [
    { name: 'legacy', body: { code: 'entity.validation.body_invalid', pointers: ['/title'], detail: 'private-body' } },
    { name: 'mixed legacy code', body: { ...canonical, code: 'entity.validation.body_invalid' } },
    { name: 'mixed legacy pointers', body: { ...canonical, pointers: ['/title'] } },
    ...[undefined, null, 1, '', 'author', 'bind', 'render', 'Validate'].map(stage => ({ name: `stage ${stage}`, body: { ...canonical, stage } })),
    ...[undefined, null, {}, [], [null], ['private-body']].map(refusals => ({ name: `refusals ${JSON.stringify(refusals)}`, body: { stage: 'validate', refusals } })),
    ...[undefined, null, 1, '', 'Entity.validation.body_invalid', 'entity.validation.', 'entity.validation.body_invalid.extra', 'entity.validation.body_invalid private-body'].map(code => ({
      name: `code ${code}`, body: { stage: 'validate', refusals: [{ code, pointer: '/title' }] },
    })),
    ...[undefined, null, 1, 'title', '#/title', '/bad~2', '/bad~'].map(pointer => ({
      name: `pointer ${pointer}`, body: { stage: 'validate', refusals: [{ code: 'entity.validation.body_invalid', pointer }] },
    })),
    { name: 'invalid later refusal', body: { stage: 'validate', refusals: [{ code: 'entity.validation.body_invalid', pointer: '/title' }, {}] } },
    ...[null, 1, '', ' '].map(auditId => ({ name: `audit ${JSON.stringify(auditId)}`, body: { ...canonical, auditId } })),
    ...[400, 403, 422, 500].map(status => ({ name: `status ${status}`, status, body: canonical })),
    { name: 'nonobject', body: ['private-body'] },
    { name: 'null', body: null },
  ]
  it.each(malformed)('rejects $name as canonical Records validation without raw fallback', async ({ body, status = 409 }) => {
    const { cut, requests, title, submit } = await openRecord(() => Response.json(body, { status }))
    submit()
    expect(await screen.findByRole('alert')).toHaveTextContent(`Request failed (${status}).`)
    expect(title).not.toHaveAttribute('aria-invalid', 'true')
    expect(screen.queryByText(/"stage": "validate"/)).not.toBeInTheDocument()
    expect(cut.container.textContent).not.toMatch(/entity\.validation|private-body|"refusals"/)
    const count = requests.length
    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    expect(requests).toHaveLength(count)
  })

  it.each(['response', 'exception'])('withholds unstructured Records %s text', async kind => {
    const { cut, submit } = await openRecord(() => {
      if (kind === 'exception') throw new Error('private-exception-stack')
      return new Response('private-response-token', { status: 500 })
    })
    submit()
    expect(await screen.findByRole('alert')).toHaveTextContent(kind === 'exception'
      ? 'The command could not be completed.' : 'Request failed (500).')
    expect(cut.container.textContent).not.toMatch(/private-/)
  })

  it.each([
    {}, { id: 'entity-7' }, { auditId: 'audit-9' }, { id: ' ', auditId: 'audit-9' },
    { id: 7, auditId: 'audit-9' }, { id: 'entity-7', auditId: '' },
    { id: 'entity-7', auditId: ' ' }, { id: 'entity-7', auditId: null }, { id: 'entity-7', auditId: 9 },
  ])('warns that an incomplete accepted receipt may already have persisted: %j', async receipt => {
    const { cut, requests, submit } = await openRecord(() => Response.json({ ...receipt, detail: 'private-receipt-body' }, { status: 201 }))
    submit()
    expect(await screen.findByRole('alert')).toHaveTextContent('The node accepted the request but returned no complete record and audit receipt. The record may exist; do not submit again without checking.')
    expect(screen.queryByText('Request refused.')).not.toBeInTheDocument()
    expect(cut.container.textContent).not.toMatch(/entity-7|audit-9|private-receipt-body/)
    const count = requests.length
    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    expect(requests).toHaveLength(count)
  })

  it.each(['edit', 'reopen', 'refused retry', 'export', 'verify', 'install', 'activate', 'pack reopen', 'pack edit'])
    ('clears accepted record and trace evidence after %s', async invalidation => {
    let attempts = 0
    const { cut, requests, title, submit } = await openRecord(() => ++attempts === 1
      ? Response.json({ id: 'entity-7', auditId: 'audit-9', detail: 'private-accepted-extra' }, { status: 201 })
      : Response.json({ stage: 'validate', refusals: [{ code: 'entity.validation.body_invalid', pointer: '/title' }] }, { status: 409 }),
    path => Response.json(path.includes('/authorization/traces/')
      ? { AuditId: 'audit-9', Outcome: 'Allowed', stages: [{ stage: 'grant', result: 'allow' }] }
      : { id: 'entity-7', values: { title: 'Stored authorized record' } }))
    submit()
    await screen.findByText(/"id": "entity-7"/)
    expect(cut.container.textContent).not.toContain('private-accepted-extra')
    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    await screen.findByText(/"Outcome": "Allowed"/)
    expect(screen.getByText(/Stored authorized record/)).toBeInTheDocument()
    const readCount = requests.filter(path => path.includes('/asset-registry/entities/') || path.includes('/authorization/traces/')).length
    switch (invalidation) {
      case 'edit': fireEvent.change(title, { target: { value: 'Changed title' } }); break
      case 'reopen': fireEvent.click(screen.getAllByRole('button', { name: 'Add item' })[0]); break
      case 'refused retry': submit(); await screen.findByText(/"stage": "validate"/); break
      case 'export': fireEvent.click(screen.getByRole('button', { name: 'Make bundle' })); break
      case 'verify': fireEvent.click(screen.getByRole('button', { name: 'Check signature' })); break
      case 'install': fireEvent.click(screen.getByRole('button', { name: 'Stage bundle' })); break
      case 'activate': fireEvent.click(screen.getByRole('button', { name: 'Go live' })); break
      case 'pack reopen':
        fireEvent.click(screen.getByRole('button', { name: 'Draft check' }))
        await screen.findByRole('textbox', { name: 'Pack document' })
        break
      case 'pack edit':
        fireEvent.click(screen.getByRole('button', { name: 'Draft check' }))
        fireEvent.change(await screen.findByRole('textbox', { name: 'Pack document' }), { target: { value: '{' } })
        break
    }
    await waitFor(() => expect(screen.queryByText('Working…')).not.toBeInTheDocument())
    expect(cut.container.textContent).not.toMatch(/entity-7|audit-9|Stored authorized record|"Outcome"/)
    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    expect(requests.filter(path => path.includes('/asset-registry/entities/') || path.includes('/authorization/traces/'))).toHaveLength(readCount)
  })

  it.each(['entity', 'trace'])('withholds partial data and stale evidence when a record %s read fails', async failed => {
    let refuse = false
    const { cut, requests, submit } = await openRecord(() => Response.json({ id: 'entity-7', auditId: 'audit-9' }, { status: 201 }), path => {
      const trace = path.includes('/authorization/traces/')
      if (refuse && trace === (failed === 'trace')) return Response.json({ detail: 'private-read-error', trace: 'hidden-audit' }, { status: 403 })
      return Response.json(trace ? { outcome: 'Allowed' } : { displayName: refuse ? 'private-partial-read' : 'Authorized record' })
    })
    submit()
    await screen.findByText(/"id": "entity-7"/)
    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    await screen.findByText(/"outcome": "Allowed"/)
    refuse = true
    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Request failed (403).')
    expect(cut.container.textContent).not.toMatch(/private-|hidden-audit|entity-7|audit-9|Authorized record|"outcome"/)
    const count = requests.length
    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    expect(requests).toHaveLength(count)
  })

  it.each(['create', 'read'].flatMap(operation => ['success', 'refusal'].map(outcome => ({ operation, outcome }))))
    ('ignores a late record $operation $outcome after values invalidate its evidence', async ({ operation, outcome }) => {
    let release: (response: Response) => void = () => { throw new Error('Request not started.') }
    const pending = new Promise<Response>(resolve => { release = resolve })
    const { cut, requests, title, submit } = await openRecord(() => operation === 'create'
      ? pending : Response.json({ id: 'entity-7', auditId: 'audit-9' }, { status: 201 }),
    path => path.includes('/authorization/traces/') ? pending : Response.json({ displayName: 'Late authorized record' }))
    submit()
    if (operation === 'read') {
      await screen.findByText(/"id": "entity-7"/)
      fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    }
    await screen.findByText('Working…')
    fireEvent.change(title, { target: { value: 'Changed while pending' } })
    await act(async () => release(outcome === 'refusal'
      ? Response.json({ stage: 'validate', refusals: [{ code: 'entity.validation.body_invalid', pointer: '/title' }] }, { status: 409 })
      : Response.json(operation === 'create' ? { id: 'entity-7', auditId: 'audit-9' } : { outcome: 'Allowed', detail: 'Late trace' }, { status: 201 })))
    await waitFor(() => expect(screen.queryByText('Working…')).not.toBeInTheDocument())
    expect(cut.container.textContent).not.toMatch(/entity-7|audit-9|Late authorized record|Late trace|"outcome"|"stage": "validate"|Request failed|Request refused/)
    const count = requests.length
    fireEvent.click(screen.getByRole('button', { name: 'Inspect evidence' }))
    expect(requests).toHaveLength(count)
  })

  it('preserves the separate pack validation error contract', async () => {
    vi.stubGlobal('fetch', vi.fn(async (path: string) => {
      if (path.includes('/ViewDefinition/')) return Response.json({ renderPlan: actionPlan })
      if (path.endsWith('/FormDefinition/platform.pack.author')) return Response.json(authorForm)
      if (path.endsWith('/packs/export?validateOnly=true')) return Response.json({ errors: [{ jsonPointer: '/packJson', message: 'Authored pack check', code: 'pack.fixture.invalid' }] }, { status: 422 })
      return Response.json({ entries: [], kindsUnavailable: [] })
    }))
    render(<SeededListPage itemId="forms" />)
    fireEvent.click(await screen.findByRole('button', { name: 'Draft check' }))
    const input = await screen.findByRole('textbox', { name: 'Pack document' })
    fireEvent.change(input, { target: { value: JSON.stringify(candidate) } })
    fireEvent.click(screen.getAllByRole('button', { name: 'Draft check' }).at(-1)!)
    expect((await screen.findAllByText(/Authored pack check/)).length).toBeGreaterThan(0)
    expect(input).toHaveAttribute('aria-invalid', 'true')
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

  it.each([false, true])('drops retained workflow state for unknown view kind=%s', async unknownViewKind => {
    let unsupported = false
    const fetchMock = vi.fn(async (input: string) => {
      if (input.includes('/ViewDefinition/')) return Response.json({
        renderPlan: unsupported ? (unknownViewKind
          ? { ...actionPlan, bindings: { ...actionPlan.bindings, viewKind: 'views.future-kind' } }
          : { ...actionPlan, definitionKind: 'UnknownDefinition' }) : actionPlan,
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
