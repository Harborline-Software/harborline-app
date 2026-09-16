import assert from 'node:assert/strict'
import { createServer as createNodeServer } from 'node:http'
import { test } from 'node:test'
import { createServer, preview } from 'vite'

for (const mode of ['dev', 'preview']) test(`${mode} host reaches selected sessions without inheriting bootstrap credentials`, async () => {
  const received = []
  const node = createNodeServer((request, response) => {
    received.push({ path: request.url, headers: request.headers })
    response.writeHead(request.headers.cookie?.includes('holder') ? 403 : 200, {
      'Content-Type': 'application/json', 'X-Harborline-Audit-Id': 'native-audit',
    })
    response.end(JSON.stringify({ cookie: request.headers.cookie }))
  })
  await new Promise(resolve => node.listen(0, '127.0.0.1', resolve))
  const previousOrigin = process.env.VITE_FORMS_API_ORIGIN
  const previousToken = process.env.LOCAL_NODE_SESSION_TOKEN
  process.env.VITE_FORMS_API_ORIGIN = `http://127.0.0.1:${node.address().port}`
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
    assert.ok(received.every(item => item.headers.authorization === undefined))
    if (mode === 'preview') {
      const count = received.length
      await fetch(origin + '/api/local-node/example')
      assert.equal(received.length, count, 'Preview must not inherit the legacy bearer proxy from server.proxy.')
    }
  } finally {
    if (previousOrigin === undefined) delete process.env.VITE_FORMS_API_ORIGIN
    else process.env.VITE_FORMS_API_ORIGIN = previousOrigin
    if (previousToken === undefined) delete process.env.LOCAL_NODE_SESSION_TOKEN
    else process.env.LOCAL_NODE_SESSION_TOKEN = previousToken
    await app?.close()
    await new Promise(resolve => node.close(resolve))
  }
})
