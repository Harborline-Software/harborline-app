import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import fixture from '../../../../tests/fixtures/authorization-trace.json'
import { createHttpAuthorizationAdminClient } from '../admin/authorization/client/httpClient'
import { CAPABILITY_DEFINITIONS, ROLE_DEFINITIONS } from '../admin/authorization/client/fixtureClient'
import { AuthorizationAdminClientProvider } from '../admin/authorization/AuthorizationAdminClientContext'
import { AuthorizationAdminPage } from '../admin/authorization/AuthorizationAdminPage'
import { AccessHoldersPage } from '../admin/authorization/AccessHoldersPage'
import { CapabilityBindingEditor } from '../admin/authorization/CapabilityBindingEditor'
import type { AuthorizationTraceRead } from './AuthorizationTrace'

const headings = ['Act kind', 'Roles in force for the subject with scope and dates', 'Standings', 'Verdict']
const nativeFetch = globalThis.fetch
const media = window.matchMedia
const liveOrigin = process.env.HARBORLINE_LIVE_API_ORIGIN
afterEach(() => { vi.unstubAllEnvs(); vi.unstubAllGlobals(); vi.resetModules(); window.matchMedia = media })
async function openTrace(expected: AuthorizationTraceRead) {
  const details = (await screen.findByText('Why can I do this?')).closest('details')!
  details.open = true; fireEvent(details, new Event('toggle'))
  const list = await screen.findByRole('list', { name: 'Authorization trace' })
  expect(within(list).getAllByRole('heading').map(node => node.textContent)).toEqual(headings)
  const parts = within(list).getAllByRole('listitem')
  expected.steps.forEach((step, index) => step.facts.forEach(fact => expect(parts[index]).toHaveTextContent(fact)))
}
for (const surface of ['settings', 'holders', 'binding']) {
  it.each([false, true])(`${surface} refusal offers a live reader only when auditId is present: %s`, async linked => {
    const requests: string[] = []
    const client = createHttpAuthorizationAdminClient({ fetchImpl: async input => {
      const path = String(input); requests.push(path)
      if (path === `/api/local-node/authorization/traces/${fixture.auditId}`) return new Response(JSON.stringify(fixture.read))
      return new Response(JSON.stringify({ code: 'authorization.permission_required', ...(linked ? { auditId: fixture.auditId } : {}) }), { status: 403 })
    } })
    render(<AuthorizationAdminClientProvider client={client}>{surface === 'settings' ? <AuthorizationAdminPage />
      : surface === 'holders' ? <AccessHoldersPage /> : <CapabilityBindingEditor definition={CAPABILITY_DEFINITIONS[0]}
        roleDefinitions={ROLE_DEFINITIONS} onNarrow={client.narrowCapabilityBinding} readTrace={client.readTrace} onSaved={vi.fn()} />}</AuthorizationAdminClientProvider>)
    if (surface === 'binding') {
      const user = userEvent.setup()
      await user.click(screen.getByRole('button', { name: 'Clear all roles…' }))
      await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Clear all roles' }))
    }
    expect(await screen.findByText(/You do not have permission to administer authorization settings/)).toBeInTheDocument()
    if (linked) {
      await openTrace(fixture.read)
      expect(requests).toContain(`/api/local-node/authorization/traces/${fixture.auditId}`)
    } else {
      expect(screen.queryByText('Why can I do this?')).not.toBeInTheDocument()
      expect(requests.some(path => path.includes('/traces/'))).toBe(false)
    }
  })
}
it.skipIf(!liveOrigin)('live shipping host: Access refusal links its real auditId to four stored parts', async () => {
  let auditId: string | undefined
  let trace: AuthorizationTraceRead | undefined
  vi.stubEnv('VITE_FORMS_FIXTURE', '1'); vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', liveOrigin!)
  window.matchMedia = query => ({ ...media(query), matches: !query.includes('max-width') })
  vi.stubGlobal('fetch', async (input: RequestInfo | URL, init?: RequestInit) => {
    const response = await nativeFetch(new URL(String(input), liveOrigin), { ...init, headers: { ...init?.headers, Authorization: `Bearer ${process.env.HARBORLINE_LIVE_API_TOKEN}` } })
    if (String(input).endsWith('/authorization/holders')) {
      expect(response.status).toBe(403); auditId = (await response.clone().json()).auditId; expect(auditId).toBeTruthy()
    }
    if (String(input).includes('/authorization/traces/')) {
      expect(new URL(String(input), liveOrigin).href).toBe(`${liveOrigin}/api/local-node/authorization/traces/${auditId}`)
      expect(response.status).toBe(200); trace = await response.clone().json()
    }
    return response
  })
  const { App } = await import('../App')
  const result = render(<App />)
  await waitFor(() => expect(result.container.textContent).toContain('Access'))
  await act(async () => screen.getByRole('link', { name: 'Access' }).click())
  const item = await screen.findByText('access.holders')
  await act(async () => item.closest<HTMLElement>('button, a')!.click())
  const details = (await screen.findByText('Why can I do this?')).closest('details')!
  details.open = true; fireEvent(details, new Event('toggle'))
  const list = await screen.findByRole('list', { name: 'Authorization trace' })
  expect(trace?.availability).toBe(0); expect(trace?.version).toBe(1)
  expect(trace?.steps.map(step => [step.ordinal, step.stage])).toEqual([[1, 'act'], [2, 'effective-roles'], [3, 'standings'], [4, 'verdict']])
  expect(within(list).getAllByRole('heading').map(node => node.textContent)).toEqual(headings)
  trace!.steps.forEach((step, index) => step.facts.forEach(fact => expect(within(list).getAllByRole('listitem')[index]).toHaveTextContent(fact)))
  expect(list).toHaveTextContent('verdict:denied')
  console.log(`LIVE React: ${liveOrigin}; Access → holders 403; auditId=${auditId}; trace 200; act → effective-roles → standings → verdict`)
})
