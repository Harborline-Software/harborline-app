import { act, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import holders from '../../../../tests/fixtures/access-holders.json'
import navigation from '../../../../tests/fixtures/access-navigation.json'
import refusal from '../../../../tests/fixtures/access-holders-refused.json'

const media = window.matchMedia
const nativeFetch = globalThis.fetch
const liveOrigin = process.env.HARBORLINE_LIVE_API_ORIGIN

afterEach(() => { vi.unstubAllEnvs(); vi.unstubAllGlobals(); vi.resetModules(); window.matchMedia = media })
async function mount(request: typeof fetch) {
  vi.stubEnv('VITE_FORMS_FIXTURE', '1')
  vi.stubEnv('VITE_AUTHORIZATION_API_ORIGIN', liveOrigin ?? 'http://127.0.0.1:7322')
  window.matchMedia = query => ({ ...media(query), matches: !query.includes('max-width') })
  vi.stubGlobal('fetch', request)
  const { App } = await import('../App')
  const result = render(<App />)
  await waitFor(() => expect(result.container.textContent).toContain('Access'))
  await act(async () => screen.getByRole('link', { name: 'Access' }).click())
  const item = await screen.findByText('access.holders')
  await act(async () => item.closest<HTMLElement>('button, a')!.click())
  return result
}
function replay(body: unknown, status = 200): typeof fetch {
  return vi.fn(async input => {
    const path = String(input)
    return new Response(JSON.stringify(path.endsWith('/navigation/workspaces') ? navigation : path.endsWith('/authorization/holders') ? body : []),
      { status: path.endsWith('/authorization/holders') ? status : 200 })
  })
}
it('renders every captured holder field in wire order, including unattributed failure and finite/unbounded dates', async () => {
  const request = replay(holders)
  await mount(request)
  const articles = await screen.findAllByRole('article')
  expect(articles).toHaveLength(holders.holders.length)
  for (const [index, row] of holders.holders.entries()) {
    const expected = [row.partyId, row.source, ...(row.attributionFailure ? [row.attributionFailure] : []),
      `${row.role.vocabulary} / ${row.role.name}`, row.grantId, row.granter, row.scope, row.effectiveFrom, row.effectiveTo ?? 'No end date']
    expect(Array.from(articles[index].querySelectorAll('dd'), cell => cell.textContent)).toEqual(expected)
  }
  expect(holders.holders.some(row => row.partyId === 'UNATTRIBUTED' && row.attributionFailure)).toBe(true)
  expect(holders.holders.some(row => row.partyId !== 'UNATTRIBUTED')).toBe(true)
  expect(vi.mocked(request).mock.calls.some(([input]) => String(input).endsWith('/authorization/holders'))).toBe(true)
})
it('shows a refused read instead of empty holders and retries through the same HTTP route', async () => {
  const request = replay(refusal, 403)
  const result = await mount(request)
  expect(await screen.findByRole('alert')).toHaveTextContent('authorization.permission_required')
  expect(screen.queryAllByRole('article')).toHaveLength(0)
  expect(result.container.textContent).not.toContain('No active holders.')
  vi.mocked(request).mockImplementation(replay(holders))
  await act(async () => screen.getByRole('button', { name: 'Retry holders' }).click())
  expect(await screen.findAllByRole('article')).toHaveLength(holders.holders.length)
  expect(screen.queryByRole('alert')).toBeNull()
})
it.skipIf(!liveOrigin)('LIVE: shipping API declaration renders Access and its holders item, then reads live holders', async () => {
  const paths: string[] = []
  const request: typeof fetch = async (input, init) => {
    paths.push(String(input))
    return nativeFetch(new URL(String(input), liveOrigin), { ...init, headers: { Authorization: `Bearer ${process.env.HARBORLINE_LIVE_API_TOKEN}` } })
  }
  const result = await mount(request)
  await waitFor(() => expect(result.container.querySelectorAll('article').length).toBeGreaterThan(0))
  expect(within(result.container).getByRole('heading', { name: 'Holders', level: 1 })).toBeVisible()
  expect(paths.some(path => path.endsWith('/navigation/workspaces'))).toBe(true)
  expect(paths.some(path => path.endsWith('/authorization/holders'))).toBe(true)
  console.log(`LIVE React: ${liveOrigin}; Access workspace + access.holders item; ${result.container.querySelectorAll('article').length} holder rows`)
})
