import { render, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import workshop from '../../../../tests/fixtures/workshop-navigation.json'

// T-585 item 1, app lane. DES-0007 `platform-package-eng-2`: one shell mounts every M11 member; no
// primitive builds another compiled admin surface.
//
// The member list is DERIVED from the pack navigation the api serves, not written here. The
// neighbouring packNavigation suite spells its thirteen members out by hand, which is fine for the
// census it asserts and useless for this one: a fourteenth member would join the pack and this file
// would still say the property holds. Reading the declaration means a member is covered the moment
// the pack declares it.
const declaredMembers = workshop.pack.seedWorkspaces
  .filter(workspace => workspace.id === 'workshop')
  .flatMap(workspace => workspace.groups ?? [])
  .flatMap(group => group.itemIds)

const listPlan = (member: string) => ({
  definitionHash: `${member}-hash`,
  definitionId: `platform.list.${member}`,
  definitionVersion: '1.0.0',
  packKey: 'harborline.platform',
  packVersion: '1.0.0',
  definitionKind: 'ViewDefinition',
  bindings: { viewKind: 'views.entity-list/grid', parameters: { fields: [{ id: 'title', label: 'Title' }] } },
})

function shellFetch(member: string, declaration: unknown = workshop) {
  return vi.fn(async (url: string | URL | Request) => {
    const path = String(url)
    if (path.endsWith('/api/local-node/navigation/workspaces')) return Response.json(declaration)
    if (path.includes('/ViewDefinition/')) return Response.json({ renderPlan: listPlan(member) })
    if (path.includes('/catalogue/definitions?')) return Response.json({ entries: [], kindsUnavailable: [] })
    return Response.json([])
  })
}

async function mountMember(member: string, request: ReturnType<typeof shellFetch>) {
  vi.stubEnv('VITE_FORMS_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', 'http://localhost:7308')
  window.history.replaceState({}, '', `/?item=${member}`)
  vi.stubGlobal('fetch', request)
  const { App } = await import('../App')
  return render(<App />)
}

afterEach(() => {
  vi.unstubAllEnvs()
  vi.unstubAllGlobals()
  vi.resetModules()
  window.history.replaceState({}, '', '/')
})

it('the pack declares members, so there is something to enumerate', () => {
  // Without this the case below is vacuously green on an empty declaration.
  expect(declaredMembers.length).toBeGreaterThan(0)
})

it.each(declaredMembers)(
  'member %s reaches its runtime through the shared Workshop shell and the shared catalogue path',
  async member => {
    const request = shellFetch(member)
    const view = await mountMember(member, request)

    // The shared shell resolves the member's list view by the one addressing rule and lists its
    // definitions through the one catalogue route. A member that carried its own shell would render
    // its own surface and ask for neither.
    await waitFor(() => expect(request.mock.calls.some(([url]) =>
      String(url) === `/api/local-node/catalogue/definitions/ViewDefinition/platform.list.${member}`)).toBe(true))
    await waitFor(() => expect(request.mock.calls.some(([url]) =>
      String(url).includes('/api/local-node/catalogue/definitions?kind='))).toBe(true))
    await waitFor(() => expect(view.container.querySelector('main [role=grid]')).not.toBeNull())
  },
)

it('a member the shared shell cannot mount is refused by name rather than rendered elsewhere', async () => {
  // The negative control. `ledgers` is declared by the pack and unknown to the shell, which is what
  // "a member that does not use the shared runtime shell" looks like from this lane. The shell must
  // say so about that member and must not fall back to a compiled surface of its own.
  const declaration = structuredClone(workshop)
  const group = declaration.pack.seedWorkspaces.find(workspace => workspace.id === 'workshop')!.groups[0]
  group.itemIds = [...group.itemIds, 'ledgers']
  group.items = [...group.items, { id: 'ledgers', labelKey: 'workshop.ledgers', label: 'Ledgers' }]

  const request = shellFetch('ledgers', declaration)
  const view = await mountMember('ledgers', request)

  await waitFor(() => expect(view.container.querySelector('[role=alert]'))
    .toHaveTextContent('This Workshop list is not declared by the platform pack.'))
  expect(request.mock.calls.some(([url]) => String(url).includes('/ViewDefinition/'))).toBe(false)
  expect(view.container.querySelector('main [role=grid]')).toBeNull()
})
