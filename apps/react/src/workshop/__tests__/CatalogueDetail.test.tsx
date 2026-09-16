import { act, fireEvent, render, waitFor } from '@testing-library/react'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { createHash } from 'node:crypto'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CatalogueDetail, detailForm, detailRequests } from '../CatalogueDetail'

const fixture = () => JSON.parse(readFileSync(resolve(process.cwd(), '../../tests/fixtures/catalogue-detail.json'), 'utf8'))
afterEach(() => vi.unstubAllGlobals())

describe('released catalogue detail contract', () => {
  it('retains the canonical hash of the released seed content', () => {
    const data = fixture()
    const canonical = (value: unknown): unknown => Array.isArray(value) ? value.map(canonical)
      : value !== null && typeof value === 'object' ? Object.fromEntries(Object.entries(value).sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0).map(([key, item]) => [key, canonical(item)])) : value
    expect(createHash('sha256').update(JSON.stringify(canonical(data.definition.body))).digest('hex')).toBe(data.definition.renderPlan.definitionHash)
  })

  it.each(['1.0', '01.0.0', '2147483648.0.0', '1.0.0-preview', '1.0.0\n'])('rejects noncanonical source version %j without protected reads', version => {
    const data = fixture()
    data.source.version = version
    Object.defineProperty(data.source, 'body', { get: () => { throw new Error('protected body read') } })
    expect(() => detailRequests(data.definition, data.source)).toThrow('Unsupported detail.')
  })

  it('normalizes the same ordered labels/values as Blazor and exposes no actions', async () => {
    const data = fixture()
    const fetcher = vi.fn(async (url: string, options: RequestInit) => url.endsWith('/session/antiforgery')
      ? new Response(null, { headers: { 'X-Harborline-Antiforgery': 'selected-token' } })
      : Response.json(options.method === 'POST' ? data.response : data.definition))
    vi.stubGlobal('fetch', fetcher)
    const cut = render(<CatalogueDetail row={{ id: 'platform.pack.author@1.0.0', catalogue: data.source }} />)
    await waitFor(() => expect(cut.container.querySelectorAll('output')).toHaveLength(4))
    expect([...cut.container.querySelectorAll('.hl-form-field')].map(node => [
      node.querySelector('label')?.textContent, node.querySelector('output')?.textContent,
    ])).toEqual(data.normalized)
    expect(cut.container.querySelectorAll('input,textarea,select,button')).toHaveLength(0)
    fireEvent.submit(cut.container.querySelector('form')!)
    expect(fetcher).toHaveBeenCalledTimes(3)
    expect(fetcher.mock.calls.map(call => call[0])).toEqual([
      '/api/selected-node/local-node/catalogue/definitions/FormDefinition/platform.detail.form?version=1.0.0',
      '/api/selected-node/session/antiforgery',
      '/api/selected-node/local-node/catalogue/details/platform.detail.form/1.0.0',
    ])
    for (const [, options] of fetcher.mock.calls) {
      expect(options).toMatchObject({ credentials: 'same-origin', cache: 'no-store', redirect: 'error' })
      expect(new Headers(options.headers).has('Authorization')).toBe(false)
    }
    expect(new Headers(fetcher.mock.calls[2][1].headers).get('X-Harborline-Antiforgery')).toBe('selected-token')
    const requests = JSON.parse(fetcher.mock.calls[2][1].body as string)
    expect(requests.map((item: { coordinate: unknown }) => item.coordinate)).toEqual(['formId', 'title', 'version', 'cascadeLayer'].map(field => ({
      schemaVersion: 1, kind: 'FormDefinition', id: 'platform.pack.author', version: '1.0.0', field,
    })))
    expect(requests.every((item: { sourceBinding: unknown }) => JSON.stringify(item.sourceBinding) === JSON.stringify(data.source.catalogueFieldBinding))).toBe(true)
  })

  it('does not project or retry through another client when selected-session antiforgery refuses', async () => {
    const data = fixture()
    const fetcher = vi.fn(async (url: string) => url.endsWith('/session/antiforgery')
      ? Response.json({ code: 'session.required' }, { status: 403 }) : Response.json(data.definition))
    vi.stubGlobal('fetch', fetcher)
    const cut = render(<CatalogueDetail row={{ id: 'selected', catalogue: data.source }} />)
    await act(async () => { await new Promise(resolve => setTimeout(resolve, 0)) })
    expect(cut.container).toBeEmptyDOMElement()
    expect(fetcher.mock.calls.map(([url]) => url)).toEqual([
      '/api/selected-node/local-node/catalogue/definitions/FormDefinition/platform.detail.form?version=1.0.0',
      '/api/selected-node/session/antiforgery',
    ])
  })

  it('omits denied fields before adapting and never reads their value or row body', () => {
    const data = fixture()
    const projection = data.response.projection
    delete projection.fieldsMeta.title
    delete projection.overlay.fields.title
    let reads = 0
    Object.defineProperty(projection.values, 'title', { get: () => { reads++; throw new Error('denied getter'); } })
    Object.defineProperty(data.source, 'body', { get: () => { reads++; throw new Error('source body'); } })
    expect(detailRequests(data.definition, data.source)).toHaveLength(4)
    const form = detailForm(data.definition, data.response)
    expect(form.view.sections.flatMap(section => section.fields.map(field => field.name))).toEqual(['formId', 'version', 'cascadeLayer'])
    expect(Object.keys(form.values)).toEqual(['formId', 'version', 'cascadeLayer'])
    expect(reads).toBe(0)
  })

  it.each(['missing', 'refused', 'mapping', 'mapping-range', 'revision', 'kind', 'binding', 'value', 'localized', 'metadata'])('keeps %s definitions inert without a fallback read', async mutation => {
    const data = fixture()
    if (mutation === 'mapping') data.definition.body.catalogueFieldSource.coordinateSchemaVersion = 2
    if (mutation === 'mapping-range') data.definition.body.catalogueFieldSource.coordinateSchemaVersion = 2147483648
    if (mutation === 'revision') data.definition.version = '2.0.0'
    if (mutation === 'kind') data.definition.renderPlan.definitionKind = 'ViewDefinition'
    if (mutation === 'binding') data.response.projection.detailBinding.definitionHash = 'wrong'
    if (mutation === 'value') data.response.projection.values.formId = { unexpected: 'must-not-render' }
    if (mutation === 'localized') data.response.projection.values.title = { defaultLocale: null, values: {} }
    if (mutation === 'metadata') delete data.response.projection.fieldsMeta
    const fetcher = vi.fn(async (url: string, options: RequestInit) => url.endsWith('/session/antiforgery')
      ? new Response(null, { headers: { 'X-Harborline-Antiforgery': 'selected-token' } })
      : Response.json(options.method === 'POST' ? data.response : data.definition,
        { status: mutation === 'missing' ? 404 : mutation === 'refused' ? 403 : 200 }))
    vi.stubGlobal('fetch', fetcher)
    const cut = render(<CatalogueDetail row={{ id: 'selected', catalogue: data.source }} />)
    await act(async () => { await new Promise(resolve => setTimeout(resolve, 0)) })
    expect(cut.container).toBeEmptyDOMElement()
    expect(fetcher).toHaveBeenCalledTimes(['binding', 'value', 'localized', 'metadata'].includes(mutation) ? 3 : 1)
  })

  it('does not allow a late result to follow a different selection', async () => {
    const data = fixture()
    let deliver: (value: Response) => void = () => undefined
    vi.stubGlobal('fetch', vi.fn(async (url: string, options: RequestInit) => url.endsWith('/session/antiforgery')
      ? new Response(null, { headers: { 'X-Harborline-Antiforgery': 'selected-token' } })
      : options.method === 'POST' ? new Promise<Response>(resolve => { deliver = resolve }) : Response.json(data.definition)))
    const cut = render(<CatalogueDetail row={{ id: 'selected', catalogue: data.source }} />)
    await act(async () => { await new Promise(resolve => setTimeout(resolve, 0)) })
    cut.rerender(<CatalogueDetail row={{ id: 'unsupported' }} />)
    await act(async () => deliver(Response.json(data.response)))
    expect(cut.container).toBeEmptyDOMElement()
  })

  it('does not request projection after an aborted definition read completes', async () => {
    const data = fixture()
    let deliver!: (response: Response) => void
    const fetcher = vi.fn((_url: string, _options: RequestInit) => new Promise<Response>(resolve => { deliver = resolve }))
    vi.stubGlobal('fetch', fetcher)
    const cut = render(<CatalogueDetail row={{ id: 'selected', catalogue: data.source }} />)
    await waitFor(() => expect(fetcher).toHaveBeenCalledTimes(1))
    cut.rerender(<CatalogueDetail row={{ id: 'unsupported' }} />)
    expect(fetcher.mock.calls[0][1].signal?.aborted).toBe(true)
    await act(async () => deliver(Response.json(data.definition)))
    expect(fetcher).toHaveBeenCalledTimes(1)
    expect(cut.container).toBeEmptyDOMElement()
  })
})
