import { spawnSync } from 'node:child_process'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
const root = fileURLToPath(new URL('../', import.meta.url))
const react = spawnSync(process.execPath, ['node_modules/vitest/vitest.mjs', 'run', 'src/__tests__/accessGrant.test.tsx', '--silent=false', '-t', 'pack field order'], { cwd: resolve(root, 'apps/react'), encoding: 'utf8' })
const blazor = spawnSync('dotnet', ['test', 'tests/Harborline.App.Blazor.Tests/Harborline.App.Blazor.Tests.csproj', '-c', 'Release', '--no-build', '-nodeReuse:false', '-maxcpucount:6', '--filter', 'FullyQualifiedName~AccessGrantTests.Pack_field_order', '--logger', 'console;verbosity=detailed'], { cwd: root, encoding: 'utf8' })
for (const run of [react, blazor]) { process.stdout.write(run.stdout ?? ''); process.stderr.write(run.stderr ?? ''); if (run.error) throw run.error }
const left = react.stdout?.match(/PARITY React body: (\{[^\r\n]+\})/)?.[1]
const right = blazor.stdout?.match(/PARITY Blazor body: (\{[^\r\n]+\})/)?.[1]
if (react.status !== 0 || blazor.status !== 0 || !left || !right || !Buffer.from(left).equals(Buffer.from(right))) {
  throw new Error('Access grant submission parity failed: both successful lane captures must be byte-identical.')
}
console.log(`Access grant submission parity: PASS (${Buffer.byteLength(left)} UTF-8 bytes)`)
