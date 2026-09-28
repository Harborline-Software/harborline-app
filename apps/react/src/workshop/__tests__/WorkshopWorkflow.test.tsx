import { fireEvent, render, screen, within } from '@testing-library/react'
import { SchemaForm } from '@harborline-software/ui-react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { WorkshopWorkflow, formViewFromPlan } from '../WorkshopWorkflow'

const literal = (value: string) => ({ kind: 'Literal', value })

// T-752: the shape harborline-api's render plan now emits for the released access-administration grant form.
// The signed overlay said controlHint "select" on `residency`; the plan carries the runtime's editor and
// permitted values instead. `person` has no value domain and keeps its authored hint.
const grantPlan = {
  definitionId: 'access.grant-a-role', definitionVersion: '1.0.0', definitionKind: 'FormDefinition',
  bindings: {
    fields: {
      person: { type: 'text', required: true, options: null },
      residency: { type: 'select', required: true, options: ['cache', 'online-only'] },
    },
    overlay: {
      title: literal('Grant a role'),
      fields: {
        person: { label: literal('Person'), controlHint: 'textarea', piiSensitivity: 'Direct' },
        residency: { label: literal('Residency'), controlHint: 'RadioGroup', permittedValues: ['cache', 'online-only'], piiSensitivity: 'None' },
      },
      sections: [{ id: 'grant', title: literal('Grant a role'), fields: ['person', 'residency'] }],
    },
  },
}

const authorForm = {
  id: 'platform.pack.author', version: '1.0.0',
  renderPlan: {
    definitionId: 'platform.pack.author', definitionVersion: '1.0.0', definitionKind: 'FormDefinition',
    bindings: {
      fields: { packJson: { type: 'textarea', required: true } },
      overlay: { fields: { packJson: { label: literal('Pack document'), controlHint: 'textarea' } } },
    },
  },
}

const authoringPlan = {
  definitionId: 'platform.list.forms', definitionVersion: '1.0.0', definitionKind: 'ViewDefinition',
  bindings: {
    viewKind: 'layout.table', parameters: {
      fields: [], actions: [{ id: 'author', label: 'Validate pack', operation: 'pack.validate', inputForm: 'platform.pack.author' }],
    },
    actions: [{ id: 'author', label: 'Validate pack' }],
  },
}

afterEach(() => vi.unstubAllGlobals())

describe('T-752: workshop form from the catalogue render plan', () => {
  it("renders the runtime's editor with its permitted values on a value-domain field", () => {
    const view = formViewFromPlan(grantPlan)
    const residency = view.sections[0].fields.find(field => field.name === 'residency')!
    expect(residency.controlHint).toBe('RadioGroup')
    expect(residency.permittedValues).toEqual(['cache', 'online-only'])

    render(<SchemaForm view={view} onSubmit={() => undefined} />)
    const group = screen.getByRole('radiogroup')
    expect(within(group).getAllByRole('radio').map(radio => (radio as HTMLInputElement).value)).toEqual(['cache', 'online-only'])
  })

  it('keeps the authored hint on a field with no value domain', () => {
    const person = formViewFromPlan(grantPlan).sections[0].fields.find(field => field.name === 'person')!
    expect(person.controlHint).toBe('textarea')
    expect(person.permittedValues).toBeUndefined()
  })

  it('preserves field binding constraints and sensitivity in the public form view', () => {
    const view = formViewFromPlan(grantPlan)
    const person = view.sections[0].fields.find(field => field.name === 'person')!
    const residency = view.sections[0].fields.find(field => field.name === 'residency')!
    expect(person).toMatchObject({ name: 'person', valueKind: 'string', required: true, isSensitive: false, isReadable: true })
    expect(residency).toMatchObject({ name: 'residency', valueKind: 'string', required: true, isSensitive: false, isReadable: true })
    expect(residency.options).toEqual([{ value: 'cache', label: 'cache' }, { value: 'online-only', label: 'online-only' }])

    const sensitive = formViewFromPlan({ ...grantPlan, bindings: { ...grantPlan.bindings, overlay: {
      ...grantPlan.bindings.overlay, fields: { ...grantPlan.bindings.overlay.fields, person: { ...grantPlan.bindings.overlay.fields.person, piiSensitivity: 'Sensitive' } },
    } } })
    expect(sensitive.sections[0].fields.find(field => field.name === 'person')?.isSensitive).toBe(true)
  })

  it.each([
    [{ valid: false, code: 'pack.pointer.invalid', pointers: ['/values/packJson'] }, 'pack.pointer.invalid'],
    [{ valid: false, errors: [{ jsonPointer: '/values/packJson', message: 'Pack key is reserved', code: 'pack.key.reserved' }] }, 'Pack key is reserved'],
    [{ valid: false, errors: [{ pointer: '/values/packJson', message: 'Pointer aliases target the authored field', code: 'pack.pointer.alias' }] }, 'Pointer aliases target the authored field'],
    [{ valid: false, codes: [{ code: 'pack.schema.invalid', target: 'packJson' }] }, 'pack.schema.invalid packJson'],
  ])('shows a server refusal on the authored field: %j', async (refusal, message) => {
    vi.stubGlobal('fetch', vi.fn(async (input: string) => input.endsWith('/FormDefinition/platform.pack.author')
      ? Response.json(authorForm)
      : Response.json(refusal)))
    render(<WorkshopWorkflow plan={authoringPlan as never} rows={[]} onRowActivate={() => undefined} onActivated={async () => undefined} />)
    fireEvent.click(screen.getByRole('button', { name: 'Validate pack' }))
    const input = await screen.findByRole('textbox', { name: 'Pack document' })
    fireEvent.change(input, { target: { value: '{"key":"reserved","version":"1.0.0","contents":[]}' } })
    fireEvent.click(screen.getAllByRole('button', { name: 'Validate pack' }).at(-1)!)
    expect((await screen.findAllByText(message)).length).toBeGreaterThan(0)
    expect(input).toHaveAttribute('aria-invalid', 'true')
  })

  it('marks a required sensitive field as constrained before submission', () => {
    const view = formViewFromPlan({
      ...grantPlan,
      bindings: {
        ...grantPlan.bindings,
        overlay: {
          ...grantPlan.bindings.overlay,
          fields: {
            ...grantPlan.bindings.overlay.fields,
            person: { ...grantPlan.bindings.overlay.fields.person, piiSensitivity: 'Sensitive' },
          },
        },
      },
    })
    const person = view.sections[0].fields.find(field => field.name === 'person')!
    expect(person).toMatchObject({ required: true, isSensitive: true })
  })

  it('rejects malformed pack text without retaining an exportable candidate', async () => {
    const fetchMock = vi.fn(async (input: string) => input.endsWith('/FormDefinition/platform.pack.author')
      ? Response.json(authorForm) : Response.json({ valid: true }))
    vi.stubGlobal('fetch', fetchMock)
    render(<WorkshopWorkflow plan={authoringPlan as never} rows={[]} onRowActivate={() => undefined} onActivated={async () => undefined} />)
    fireEvent.click(screen.getByRole('button', { name: 'Validate pack' }))
    const input = await screen.findByRole('textbox', { name: 'Pack document' })
    fireEvent.change(input, { target: { value: '{' } })
    fireEvent.click(screen.getAllByRole('button', { name: 'Validate pack' }).at(-1)!)
    expect((await screen.findAllByText(/Expected property name/)).length).toBeGreaterThan(0)
    expect(fetchMock.mock.calls.some(([path]) => String(path).includes('validateOnly=true'))).toBe(false)
  })
})
