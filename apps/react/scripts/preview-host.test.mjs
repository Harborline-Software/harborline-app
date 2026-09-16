import assert from 'node:assert/strict'
import { createServer as createNodeServer } from 'node:http'
import { test } from 'node:test'
import { createServer, preview } from 'vite'

for (const service of ['FORMS', 'AUTHORIZATION']) for (const mode of ['dev', 'preview']) test(`${mode} host reaches selected sessions with ${service} origin without inheriting bootstrap credentials`, async () => {
  const received = []
  const node = createNodeServer((request, response) => {
    received.push({ path: request.url, headers: request.headers })
    response.writeHead(request.headers.cookie?.includes('holder') ? 403 : 200, {
      'Content-Type': 'application/json', 'X-Harborline-Audit-Id': 'native-audit',
    })
    response.end(JSON.stringify({ cookie: request.headers.cookie }))
  })
  await new Promise(resolve => node.listen(0, '127.0.0.1', resolve))
  const originKeys = ['FORMS', 'REPORTS', 'VIEWS', 'DATA_EXCHANGE', 'SCHEDULING', 'AUTHORIZATION']
    .map(name => `VITE_${name}_API_ORIGIN`)
  const previousOrigins = Object.fromEntries(originKeys.map(key => [key, process.env[key]]))
  const previousToken = process.env.LOCAL_NODE_SESSION_TOKEN
  for (const key of originKeys) process.env[key] = ''
  process.env[`VITE_${service}_API_ORIGIN`] = `http://127.0.0.1:${node.address().port}`
  process.env.LOCAL_NODE_SESSION_TOKEN = 'bootstrap-must-not-flow'
  let app
  try {
    app = mode === 'preview'
      ? await preview({ preview: { host: '127.0.0.1', port: 0, strictPort: false }, logLevel: 'error' })
      : await createServer({ server: { host: '127.0.0.1', port: 0, strictPort: false }, logLevel: 'error' })
    if (mode === 'dev') await app.listen()
    const origin = `http://127.0.0.1:${app.httpServer.address().port}`
    if (mode === 'preview') {
      const html = await (await fetch(origin)).text()
      const asset = html.match(/src="(\/assets\/[^\"]+\.js)"/)?.[1]
      assert.ok(asset, 'Preview must serve the built entry point, not a development module.')
      assert.equal((await fetch(origin + asset)).status, 200)
      const bundle = await (await fetch(origin + asset)).text()
      assert.ok(!bundle.includes('access-holders-heading'), 'The legacy compiled Access page must not enter the production bundle.')
      assert.ok(!bundle.includes('Retry holders'), 'Only the generic runtime may own the production holder flow.')
    }
    await Promise.all([['administrator', 200], ['holder', 403]].map(async ([principal, status]) => {
      const response = await fetch(origin + '/api/selected-node/session/example', { headers: {
        Origin: origin, Cookie: `__Host-hl-selected=${principal}`, Authorization: 'Bearer injected-bootstrap',
      } })
      assert.equal(response.status, status)
      assert.equal(response.headers.get('X-Harborline-Audit-Id'), 'native-audit')
      assert.equal(response.headers.get('Cache-Control'), 'no-store')
      assert.equal((await response.json()).cookie, `__Host-hl-selected=${principal}`)
    }))
    assert.deepEqual(received.map(item => item.path), ['/api/session/example', '/api/session/example'])
    const vocabulary = await fetch(origin + '/api/selected-node/local-node/authorization/role-vocabulary', { headers: {
      Origin: origin, Cookie: '__Host-hl-selected=administrator', Authorization: 'Bearer injected-bootstrap',
    } })
    assert.equal(vocabulary.status, 200)
    assert.deepEqual(await vocabulary.json(), { cookie: '__Host-hl-selected=administrator' })
    assert.equal(received.at(-1).path, '/api/local-node/authorization/role-vocabulary')
    assert.ok(received.every(item => item.headers.authorization === undefined))
    const countBeforeMissingSession = received.length
    const missingSession = await fetch(origin + '/api/selected-node/local-node/authorization/role-vocabulary', { headers: { Origin: origin } })
    assert.equal(missingSession.status, 401)
    assert.equal(received.length, countBeforeMissingSession, 'Authorization reads require a selected session even when a bootstrap token is configured.')
    if (mode === 'preview') {
      const count = received.length
      await fetch(origin + '/api/local-node/example')
      assert.equal(received.length, count, 'Preview must not inherit the legacy bearer proxy from server.proxy.')
    }
  } finally {
    for (const [key, value] of Object.entries(previousOrigins)) {
      if (value === undefined) delete process.env[key]
      else process.env[key] = value
    }
    if (previousToken === undefined) delete process.env.LOCAL_NODE_SESSION_TOKEN
    else process.env.LOCAL_NODE_SESSION_TOKEN = previousToken
    await app?.close()
    await new Promise(resolve => node.close(resolve))
  }
})
