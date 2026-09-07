import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { AuthorizationAdminClientProvider } from '../AuthorizationAdminClientContext'
import { AuthorizationAdminPage } from '../AuthorizationAdminPage'
import { AuditorAccessReviewPanel } from '../AuditorAccessReviewPanel'
import { createFixtureAuthorizationAdminClient, CAPABILITY_DEFINITIONS } from '../client/fixtureClient'
import type { AuthorizationCapabilityDefinition } from '../client/types'

const AUDITOR = { vocabulary: 'sys.platform-roles', name: 'auditor' } as const
const seeded = CAPABILITY_DEFINITIONS.filter(row => row.binding.effectiveRoles.some(
  role => role.vocabulary === AUDITOR.vocabulary && role.name === AUDITOR.name))

const panel = () => screen.getByRole('region', { name: 'Access review · Auditor' })

describe('Access review · Auditor', () => {
  it('derives exactly one row from the seeded catalogue, with ceiling, effective roles and revision', async () => {
    // The seed is the fixture catalogue, not a compiled list: the row the panel shows must be the
    // one the catalogue itself derives.
    expect(seeded).toHaveLength(1)
    render(<AuthorizationAdminClientProvider client={createFixtureAuthorizationAdminClient()}><AuthorizationAdminPage /></AuthorizationAdminClientProvider>)

    const region = await screen.findByRole('region', { name: 'Access review · Auditor' })
    const rows = within(region).getAllByRole('article')
    expect(rows).toHaveLength(1)
    expect(within(region).queryByRole('alert')).toBeNull()
    expect([...rows[0].querySelectorAll('dt')].map(term => term.textContent)).toEqual([
      'Operation', 'Publisher ceiling', 'Effective roles', 'Binding revision',
    ])
    const values = [...rows[0].querySelectorAll('dd')].map(value => value.textContent)
    expect(values[0]).toBe(`${seeded[0].atom.operation} · ${seeded[0].atom.scopeType}:${seeded[0].atom.scopeValue}`)
    expect(values[1]).toBe(`${seeded[0].publisherPackageId} offers sys.platform-roles:auditor, sys.platform-roles:node-operator`)
    expect(values[2]).toBe('sys.platform-roles:auditor, sys.platform-roles:node-operator')
    expect(values[3]).toBe(String(seeded[0].binding.revision))
  })

  it('warns instead of hiding when the catalogue derives no Auditor capability', () => {
    render(<AuditorAccessReviewPanel definitions={CAPABILITY_DEFINITIONS.filter(row => !seeded.includes(row))} />)

    expect(within(panel()).getByRole('alert')).toHaveTextContent(
      'the Auditor should hold exactly one authorization capability, but the catalogue derives 0')
    expect(within(panel()).queryAllByRole('article')).toHaveLength(0)
  })

  it('warns and still shows both rows when the catalogue derives two Auditor capabilities', () => {
    const second: AuthorizationCapabilityDefinition = {
      ...seeded[0],
      definitionId: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
      atom: { ...seeded[0].atom, operation: 'contacts:read' },
    }
    render(<AuditorAccessReviewPanel definitions={[...CAPABILITY_DEFINITIONS, second]} />)

    expect(within(panel()).getByRole('alert')).toHaveTextContent(
      'the Auditor should hold exactly one authorization capability, but the catalogue derives 2')
    expect(within(panel()).getAllByRole('article')).toHaveLength(2)
    expect(within(panel()).getByRole('heading', { name: 'contacts:read' })).toBeInTheDocument()
  })
})
