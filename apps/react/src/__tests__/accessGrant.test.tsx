import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { expect, it, vi } from 'vitest'
import { readFileSync } from 'node:fs'
const expected = readFileSync('../../tests/fixtures/access-grant-body.json', 'utf8')
import { AccessHoldersPage } from '../admin/authorization/AccessHoldersPage'
import { AuthorizationAdminClientProvider } from '../admin/authorization/AuthorizationAdminClientContext'
import { createHttpAuthorizationAdminClient } from '../admin/authorization/client/httpClient'
import { FormsAdminClientProvider } from '../admin/forms/FormsAdminClientContext'
import { createHttpFormsAdminClient } from '../admin/forms/client/httpClient'
import form from '../../../../tests/fixtures/access-grant-form.json'
import holderFixture from '../../../../tests/fixtures/access-holders.json'

const values = JSON.parse(expected) as Record<string, string>
const origin = process.env.HARBORLINE_LIVE_API_ORIGIN
function mount(request: typeof fetch) {
  return render(<AuthorizationAdminClientProvider client={createHttpAuthorizationAdminClient({ fetchImpl: request })}>
    <FormsAdminClientProvider client={createHttpFormsAdminClient({ fetchImpl: request })}><AccessHoldersPage /></FormsAdminClientProvider>
  </AuthorizationAdminClientProvider>)
}
async function fill() {
  fireEvent.click(screen.getByRole('button', { name: 'Grant a role' }))
  const element = await screen.findByRole('form', { name: 'Grant a role' })
  expect(Array.from(element.querySelectorAll('input'), input => input.name)).toEqual(['person', 'role', 'scope', 'residency', 'effectiveFrom', 'effectiveTo', 'reason'])
  for (const input of element.querySelectorAll('input')) fireEvent.change(input, { target: { value: values[input.name] } })
  fireEvent.submit(element)
}
function replay(status: number) {
  const bodies: string[] = []; let reads = 0
  const request = vi.fn<typeof fetch>(async (input, init) => {
    const path = String(input)
    if (path.endsWith('/submit')) {
      bodies.push(String(init?.body))
      return new Response(JSON.stringify(status === 201 ? { instanceId: 'submission-301' } : { code: 'authorization.permission_required', auditId: '30100000-0000-4000-8000-000000000003' }), { status })
    }
    if (path.endsWith('/forms/access.grant-a-role')) return new Response(JSON.stringify(form))
    if (path.endsWith('/holders')) { reads++; return new Response(JSON.stringify({ holders: bodies.length && status === 201 ? [{ ...holderFixture.holders[0], partyId: values.person }] : [] })) }
    throw new Error(`Unexpected request ${path}`)
  })
  return { request, bodies, reads: () => reads }
}
it('pack field order and UTF-8 submission bytes match the other lane contract; accepted submission refreshes holders', async () => {
  const transport = replay(201); mount(transport.request); await screen.findByText('No active holders.')
  await fill()
  expect(await screen.findByText('Submission saved.')).toBeVisible()
  expect(await screen.findAllByRole('article')).toHaveLength(1)
  expect(screen.getByRole('article')).toHaveTextContent(values.person)
  expect(Buffer.from(transport.bodies[0])).toEqual(Buffer.from(expected))
  expect(transport.reads()).toBe(2)
  process.stdout.write(`PARITY React body: ${transport.bodies[0]}\n`)
})
it('refused submission retains values, exposes the audit affordance, and never refreshes holders or reports success', async () => {
  const transport = replay(403); mount(transport.request); await screen.findByText('No active holders.')
  await fill()
  expect(await screen.findByRole('alert')).toHaveTextContent('permission')
  expect(screen.getByText('Why can I do this?')).toBeVisible()
  expect(screen.queryByText('Submission saved.')).toBeNull()
  expect(screen.getByLabelText('Person')).toHaveValue(values.person)
  expect(transport.reads()).toBe(1)
  expect(transport.bodies[0]).toBe(expected)
})
it.skipIf(!origin)('LIVE: grant through the surface then find the submitted party in holders', async () => {
  mount(async (input, init) => {
    const response = await fetch(new URL(String(input), origin), { ...init, headers: { ...init?.headers, Authorization: `Bearer ${process.env.HARBORLINE_LIVE_API_TOKEN}` } })
    process.stdout.write(`LIVE React ${init?.method} ${input}: ${response.status} ${await response.clone().text()}\n`)
    return response
  })
  await fill()
  await waitFor(() => expect(screen.getAllByRole('article').some(row => row.textContent?.includes(values.person))).toBe(true))
})
