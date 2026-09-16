import assert from 'node:assert/strict'
import { createServer } from 'node:http'
import { test } from 'node:test'
import { preview } from 'vite'

test('built preview serves real assets and selected sessions without inheriting development bearer proxy', async () => {
  const received = []
  const node = createServer((request, response) => {
    received.push(request.headers)
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
    app = await preview({ preview: { host: '127.0.0.1', port: 0, strictPort: false }, logLevel: 'error' })
    const origin = `http://127.0.0.1:${app.httpServer.address().port}`
    const html = await (await fetch(origin)).text()
    const asset = html.match(/src="(\/assets\/[^"]+\.js)"/)?.[1]
    assert.ok(asset, 'Preview must serve the built entry point, not a development module.')
    assert.equal((await fetch(origin + asset)).status, 200)
    const bundle = await (await fetch(origin + asset)).text()
    assert.ok(!bundle.includes('access-holders-heading'), 'The legacy compiled Access page must not enter the production bundle.')
    assert.ok(!bundle.includes('Retry holders'), 'Only the generic runtime may own the production holder flow.')
    for (const [principal, status] of [['administrator', 200], ['holder', 403]]) {
      const response = await fetch(origin + '/api/selected-node/session/example', { headers: {
        Origin: origin, Cookie: `__Host-hl-selected=${principal}`, Authorization: 'Bearer injected-bootstrap',
      } })
      assert.equal(response.status, status)
      assert.equal(response.headers.get('X-Harborline-Audit-Id'), 'native-audit')
      assert.equal((await response.json()).cookie, `__Host-hl-selected=${principal}`)
    }
    const count = received.length
    await fetch(origin + '/api/local-node/example')
    assert.equal(received.length, count, 'Preview must not inherit the legacy bearer proxy from server.proxy.')
    assert.ok(received.every(headers => headers.authorization === undefined))
  } finally {
    if (previousOrigin === undefined) delete process.env.VITE_FORMS_API_ORIGIN
    else process.env.VITE_FORMS_API_ORIGIN = previousOrigin
    if (previousToken === undefined) delete process.env.LOCAL_NODE_SESSION_TOKEN
    else process.env.LOCAL_NODE_SESSION_TOKEN = previousToken
    await app?.close()
    await new Promise(resolve => node.close(resolve))
  }
})
