import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ConfigurationProposalPage } from '../configuration/ConfigurationProposalPage'
import {
  createHttpConfigurationProposalClient,
  type ConfigurationProposalClient,
  type ProposedChange,
} from '../configuration/client/proposalClient'
// The SAME released definition and the SAME conformance example the platform's own React and Blazor
// renderers drive, copied out of the pinned platform checkout by scripts/build-local-feed.mjs.
// tests/Harborline.App.Blazor.Tests/ConfigurationProposalPageTests.cs walks this same script, step
// for step, so the two app lanes complete ONE Records-and-Forms example rather than two similar ones.
import definitions from '../../../.feed/platform/configuration-proposal.json'
import example from '../../../.feed/platform/proposal-cases.json'

const STATUSES = definitions.statuses as Readonly<Record<string, { values: Readonly<Record<string, string>> }>>

type Case = { step: string; status: string; values: Record<string, string> }
const step = (name: string): Case => (example.cases as Case[]).find(entry => entry.step === name)!

/** Turns one fixture case into the api answer the surface renders. */
function answer(name: string): ProposedChange {
  const { status, values } = step(name)
  return {
    status,
    tenantKey: values.tenantKey,
    proposalId: values.proposalId,
    baselineDigest: values.baselineDigest,
    effectiveDigest: values.effectiveDigest,
    workingDigest: values.workingDigest,
    edits: values.editedDefinitions === ''
      ? []
      : values.editedDefinitions.split('\n').map(line => ({
        definitionKey: line.slice(0, line.indexOf(' (')),
        packageKey: line.slice(line.indexOf('(') + 1, -1),
      })),
    savedVersionCount: values.savedVersion === '' ? 0 : Number(values.savedVersion.split(':')[0]),
    savedVersion: null,
    check: values.checkState === 'No check recorded.'
      ? null
      : { receiptId: 'receipt-1', checkedDigest: values.workingDigest, isCurrent: values.checkState.startsWith('Current') },
    releasedPackage: null,
    refusals: [],
    detail: values,
  }
}

describe('the Records-and-Forms proposed change surface', () => {
  it('renders every step of the example through the released platform definition', async () => {
    for (const entry of example.cases as Case[]) {
      const client: ConfigurationProposalClient = {
        ...unusable(),
        read: () => Promise.resolve(answer(entry.step)),
      }
      const view = render(<ConfigurationProposalPage client={client} proposalId="proposal-1" />)
      expect(await screen.findByRole('form', { name: 'Proposed change' })).toBeInTheDocument()

      // Acceptance 6: every label on the surface is the platform's released vocabulary.
      expect(view.container.querySelector('#status')?.textContent).toBe(STATUSES[entry.status].values.en)
      for (const [name, value] of Object.entries(entry.values)) {
        expect(view.container.querySelector(`#${name}`)?.textContent).toBe(value)
      }
      // Acceptance 1: the baseline the proposed change recorded is on the surface, and the effective
      // generation matches it except in the one case where the tenant moved on underneath the author.
      expect(view.container.querySelector('#baselineDigest')?.textContent).toBe(example.baselineDigest)
      if (entry.step === 'released-against-a-stale-baseline') {
        expect(view.container.querySelector('#effectiveDigest')?.textContent).not.toBe(example.baselineDigest)
        expect(view.container.querySelector('#refusals')?.textContent).toContain('configuration-baseline-stale')
      } else {
        expect(view.container.querySelector('#effectiveDigest')?.textContent).toBe(example.baselineDigest)
      }
      view.unmount()
    }
  })

  it('walks the example from proposed through saved to released', async () => {
    const user = userEvent.setup()
    const saveVersion = vi.fn(() => Promise.resolve(answer('saved')))
    const release = vi.fn(() => Promise.resolve(released()))
    const client: ConfigurationProposalClient = { ...unusable(), read: () => Promise.resolve(answer('proposed')), saveVersion, release }
    const view = render(<ConfigurationProposalPage client={client} proposalId="proposal-1" />)
    await screen.findByRole('form', { name: 'Proposed change' })

    // Acceptance 2: the two autosaved edits are on the surface before any version is saved, and no
    // Saved version, author or rationale is shown yet.
    expect(view.container.querySelector('#editedDefinitions')?.textContent).toBe(step('proposed').values.editedDefinitions)
    expect(view.container.querySelector('#savedVersion')?.textContent).toBe('')
    expect(view.container.querySelector('#savedBy')?.textContent).toBe('')
    // Nothing may be released before a version is saved and checked.
    expect(screen.getByRole('button', { name: 'Released package' })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Saved version' }))
    await waitFor(() => expect(view.container.querySelector('#status')?.textContent).toBe('Saved version'))
    expect(view.container.querySelector('#savedBy')?.textContent).toBe('dana.okafor')
    expect(view.container.querySelector('#rationale')?.textContent).toBe(step('saved').values.rationale)
    expect(view.container.querySelector('#checkState')?.textContent).toBe('Current (receipt-1)')

    await user.click(screen.getByRole('button', { name: 'Released package' }))
    await waitFor(() => expect(view.container.querySelector('#status')?.textContent).toBe('Released package'))
    // Acceptance 5: the digest on the surface is the exported artifact's own digest.
    expect(view.container.querySelector('#releasedPackage')?.textContent).toContain(example.releasedPackageDigest)
    expect(view.container.querySelector('#refusals')?.textContent).toBe('')
    expect(saveVersion).toHaveBeenCalledOnce()
    expect(release).toHaveBeenCalledOnce()
  })

  it('does not offer a release once a later edit has invalidated the check', async () => {
    const invalidated = answer('check-invalidated-by-a-later-edit')
    const client: ConfigurationProposalClient = { ...unusable(), read: () => Promise.resolve(invalidated) }
    const view = render(<ConfigurationProposalPage client={client} proposalId="proposal-1" />)
    await screen.findByRole('form', { name: 'Proposed change' })

    // Acceptance 3: the surface reports the invalidation the api bound, and the act is not offered.
    expect(view.container.querySelector('#checkState')?.textContent).toContain('Invalidated by a later edit')
    expect(view.container.querySelector('#status')?.textContent).not.toBe('Released package')
    expect(view.container.querySelector('#releasedPackage')?.textContent).toBe('')
    expect(screen.getByRole('button', { name: 'Released package' })).toBeDisabled()
  })

  it('reads a 422 release refusal rather than throwing it, because that body is the refused detail', async () => {
    const refusal = step('check-invalidated-by-a-later-edit')
    const fetchImpl = vi.fn(async () => new Response(JSON.stringify({ ...answer(refusal.step), status: 'saved' }),
      { status: 422, headers: { 'Content-Type': 'application/json' } })) as unknown as typeof fetch
    const client = createHttpConfigurationProposalClient({ fetchImpl })
    const read = await client.release('proposal-1', 1, 'acme.invoice', '1.1.0')
    expect(read.detail.checkState).toContain('Invalidated by a later edit')
    expect(read.detail.refusals).toContain('configuration-check-invalidated')
  })
})

function released(): ProposedChange {
  const base = answer('released')
  return { ...base, releasedPackage: { digest: example.releasedPackageDigest, packageKey: 'tenant-a.invoice-purchase-order',
    revision: '1.1.0', proposalId: 'proposal-1', savedVersionDigest: base.workingDigest, baselineDigest: base.baselineDigest,
    releasedBy: 'dana.okafor', releasedAt: '2026-09-20T09:00:00Z', signature: '{}' } }
}

/** Every act this test does not exercise; calling one is the failure, not a silent no-op. */
function unusable(): ConfigurationProposalClient {
  const reject = () => Promise.reject(new Error('not used'))
  return { start: reject, read: reject, autosave: reject, saveVersion: reject, recordCheck: reject, release: reject, offered: reject }
}
