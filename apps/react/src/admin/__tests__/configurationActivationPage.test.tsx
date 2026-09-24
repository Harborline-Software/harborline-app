import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ConfigurationActivationPage } from '../configuration/ConfigurationActivationPage'
import {
  createHttpConfigurationActivationClient,
  type ConfigurationActivationClient,
  type ConfigurationActivationDetail,
} from '../configuration/client/httpClient'
// The SAME released definition and the SAME conformance cases the platform's own React and Blazor
// renderers drive, copied out of the pinned platform checkout by scripts/build-local-feed.mjs.
// The two app lanes and the two platform lanes therefore assert one fixture and cannot drift.
import definitions from '../../../.feed/platform/configuration-activation.json'
import cases from '../../../.feed/platform/activation-cases.json'

const STATUSES = definitions.statuses as Readonly<Record<string, { values: Readonly<Record<string, string>> }>>

function client(): ConfigurationActivationClient {
  return {
    readEffective: () => Promise.resolve({ digest: 'baseline-digest', algorithm: 'sha256', references: {} }),
    prepare: () => Promise.reject(new Error('not used')),
    activate: () => Promise.reject(new Error('not used')),
  }
}

describe('configuration activation surface', () => {
  it.each(cases.cases)('renders the released $status status through the platform SchemaForm', async ({ status, values }: {
    status: string
    values: Record<string, string>
  }) => {
    const outcome = { status, tenantKey: values.tenantKey, candidateDigest: values.candidateDigest,
      expectedBaselineDigest: values.expectedBaselineDigest, effectiveDigest: values.effectiveDigest,
      refusals: [], detail: values as ConfigurationActivationDetail }
    const view = render(<ConfigurationActivationPage client={client()} outcome={outcome} />)

    expect(await screen.findByRole('form', { name: 'Configuration activation' })).toBeInTheDocument()
    expect(view.container.querySelector('#status')?.textContent).toBe(STATUSES[status].values.en)
    for (const [name, value] of Object.entries(values)) {
      expect(view.container.querySelector(`#${name}`)?.textContent).toBe(value)
    }
    // Read-only: the surface reports, it never offers a way to change what it reports.
    expect(view.container.querySelectorAll('input,select,textarea,button')).toHaveLength(0)

    if (status === 'effective') {
      expect(values.effectiveDigest).toBe(values.candidateDigest)
      expect(values.refusals).toBe('')
    } else {
      expect(values.effectiveDigest).toBe(values.expectedBaselineDigest)
      expect(values.effectiveDigest).not.toBe(values.candidateDigest)
    }
  })

  it('never renders a refused projection as effective', async () => {
    const refused = cases.cases.find((entry: { status: string }) => entry.status === 'refused')!
    const outcome = { status: 'refused', tenantKey: refused.values.tenantKey,
      candidateDigest: refused.values.candidateDigest, expectedBaselineDigest: refused.values.expectedBaselineDigest,
      effectiveDigest: refused.values.effectiveDigest, refusals: [], detail: refused.values as ConfigurationActivationDetail }
    const view = render(<ConfigurationActivationPage client={client()} outcome={outcome} />)

    await screen.findByRole('form', { name: 'Configuration activation' })
    const rendered = view.container.querySelector('#status')?.textContent
    expect(rendered).toBe(STATUSES.refused.values.en)
    expect(rendered).not.toBe(STATUSES.effective.values.en)
    expect(view.container.querySelector('#refusals')?.textContent).toContain('projection-failed (forms/invoice)')
    // The effective generation stayed the prior baseline, and the candidate never became it.
    expect(view.container.querySelector('#effectiveDigest')?.textContent)
      .not.toBe(view.container.querySelector('#candidateDigest')?.textContent)
  })

  it('reports the effective generation the api read, and nothing more', async () => {
    render(<ConfigurationActivationPage client={client()} />)

    expect(await screen.findByTestId('effective-generation')).toHaveTextContent('baseline-digest')
    expect(screen.queryByRole('form')).not.toBeInTheDocument()
  })
})

describe('configuration activation client', () => {
  it('reads a refusal out of the api 422 rather than throwing it away', async () => {
    const refused = cases.cases.find((entry: { status: string }) => entry.status === 'refused')!
    const fetchImpl = vi.fn(async () => new Response(
      JSON.stringify({ status: 'refused', tenantKey: 'tenant-a', candidateDigest: refused.values.candidateDigest,
        expectedBaselineDigest: refused.values.expectedBaselineDigest, effectiveDigest: refused.values.effectiveDigest,
        refusals: [{ code: 'projection-failed', target: 'forms/invoice', message: 'Invoice form projection failed.' }],
        detail: refused.values }),
      { status: 422 },
    )) as unknown as typeof fetch

    const outcome = await createHttpConfigurationActivationClient({ fetchImpl }).activate({
      expectedBaselineDigest: refused.values.expectedBaselineDigest,
      candidateDigest: refused.values.candidateDigest,
      evidenceIntent: { id: 'intent-1', reason: 'T-460 proof' },
    })

    expect(outcome.status).toBe('refused')
    expect(outcome.detail?.status).toBe(STATUSES.refused.values.en)
  })

  it('throws when the api refuses the effective read', async () => {
    const fetchImpl = vi.fn(async () => new Response(JSON.stringify({ code: 'configuration-generation-missing' }), { status: 404 })) as unknown as typeof fetch

    await expect(createHttpConfigurationActivationClient({ fetchImpl }).readEffective())
      .rejects.toThrow('configuration-generation-missing')
  })
})
