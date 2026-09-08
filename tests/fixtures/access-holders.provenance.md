# Access fixtures provenance

Captured from the shipping API host at http://127.0.0.1:7322 on 2026-09-08T00:22:16.313272+00:00.
API origin/main: 1bdc5219dec5347330c0ea720c176efc7586633c, tree 09a044fe2cd67fbbb5fc0ae73733fae41bb58c7c.
The existing t329 Release host was built from this identical tree (commit 4b4218ec78ad1ef9b1e3385af1bda46b14e25529); the main working checkout was stale and was left untouched.

- access-navigation.json: GET /api/local-node/navigation/workspaces, HTTP 200, ordinary Access package 1.1.0 preload. Null fields are retained.
- access-holders-refused.json: GET /api/local-node/authorization/holders, HTTP 403 before seeding local administrator authority.
- access-holders.json: GET /api/local-node/authorization/holders, HTTP 200, Cache-Control: no-store. Response captured once after setup and only JSON whitespace normalized to LF. The shared rows are authoritative for both lane tests.

Setup used an isolated scratch database, injected test root seed, desktop session token, and the shipping store APIs: two durable administrator grants (root/unbounded and record/finite), one canonical Party + principal-user binding for local. The shipping definition seed contributes two additional grants; all four returned rows are retained. No API source changed. Dates are captured wire strings, not locale-dependent projections. The finite grant may expire; fixture replay remains deterministic. Live tests only require a separately started host and declaration, and do not replay expired capture dates.

SHA256 (LF bytes):
- access-navigation.json: 244461ec6f746049071e46ab37153e93397324dd4d59f5b01dbc59e419cee8b1
- access-holders-refused.json: 2f0527390e45484b1936de9d251faad42b08a6d14b60a64288e196235ec4d841
- access-holders.json: 260d1483844ecf9e646c7bd6df1c23dda8cf1c65fd8b88361b3e3f5df131f18a
