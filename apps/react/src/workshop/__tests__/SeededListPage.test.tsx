import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { SeededListPage } from '../SeededListPage'

afterEach(() => vi.unstubAllGlobals())

describe('seeded Workshop list', () => {
  it('renders the compiled Forms plan and forwards its inspect action', async () => {
    const plan = { definitionHash: 'hash', definitionId: 'platform.list.forms', definitionVersion: '1.0.0', packKey: 'harborline.platform', packVersion: '1.0.0', definitionKind: 'ViewDefinition', bindings: { viewKind: 'views.entity-list/grid', parameters: { fields: [{ id: 'formId', label: 'Key' }, { id: 'title', label: 'Title' }, { id: 'version', label: 'Version' }, { id: 'cascadeLayer', label: 'Cascade layer' }] } } }
    vi.stubGlobal('fetch', vi.fn(async (input: string) => new Response(JSON.stringify(input.includes('/ViewDefinition/')
      ? { renderPlan: plan }
      : { entries: [{ id: 'work-order', version: '1.0.0', status: 'Published', title: { defaultLocale: 'en', values: { en: 'Work order' } }, body: { cascadeLayer: 'Tenant' } }], kindsUnavailable: [] }), { status: 200 })))
    render(<SeededListPage itemId="forms" />)
    expect(await screen.findByRole('grid')).toHaveAttribute('aria-label', 'View results')
    expect(screen.getAllByRole('columnheader').map(node => node.textContent)).toEqual(['Key', 'Title', 'Version', 'Cascade layer'])
    expect(screen.getByText('work-order')).toBeInTheDocument()
    fireEvent.doubleClick(document.querySelector('[data-row-id="work-order@1.0.0"]')!)
    await waitFor(() => expect(screen.getByRole('complementary', { name: 'Definition inspector' })).toHaveTextContent('Work order'))
  })
})
