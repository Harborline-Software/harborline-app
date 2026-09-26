import { render, screen, within } from '@testing-library/react'
import { SchemaForm } from '@harborline-software/ui-react'
import { describe, expect, it } from 'vitest'
import { formViewFromPlan } from '../WorkshopWorkflow'

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
})
