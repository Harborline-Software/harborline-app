#!/usr/bin/env node
// Packs the platform's Blazor UI adapters, and everything they depend on, into this lane's local
// NuGet feed.
//
// The Blazor twin of apps/react/scripts/build-local-feed.mjs, and it exists for the same reason:
// the app must consume the PACKAGE surface, not the source tree. A project reference would resolve
// static web assets from disk and render correctly while the published package carried none - the
// exact defect this lane once shipped (assets present in obj/, absent from the served app).
// Packing proves the files the package declares are the files the app actually gets.
//
// Until this script existed the nupkg was placed in .feed by hand, which is why CI could not build
// the lane at all: .feed is gitignored, so a clean runner had no package source and every restore
// failed NU1301 (control ticket 089).
//
// PACKING ONE PROJECT IS NOT ENOUGH. Harborline.UIAdapters.Blazor declares a dependency on
// Harborline.Foundation, which is not on nuget.org either, so a feed carrying only the adapters
// restores locally (the dependency is already in the developer's global NuGet cache from some
// earlier pack) and fails on a clean runner with NU1101. The closure is packed here, and the
// assertion at the bottom fails the build if the platform ever adds a dependency this list does
// not cover - so the next one is found here rather than in a CI restore log.
import { execFileSync } from 'node:child_process'
import { mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { inflateRawSync } from 'node:zlib'

const here = path.dirname(fileURLToPath(import.meta.url))
const app = path.resolve(here, '..')
const platform = process.env.HARBORLINE_PLATFORM_REPO
  ?? path.resolve(app, '../../../harborline-platform')
const feed = path.join(app, '.feed')

// The package ids differ from the project file names (PackageId is overridden), so both are named.
const projects = [
  'projections/blazor/ui/hlp.ui.button/Harborline.UIAdapters.Blazor.csproj',
  'projections/dotnet/foundation/hlp.ui.button/Harborline.Foundation.csproj',
  'projections/dotnet/contracts/hlp.contracts.identities/Harborline.Contracts.csproj',
]

// The version is the platform's to compute, never ours to type (platform ticket 143): every
// package version is a hash of the platform's packable content, so there is no fixed version to
// pin and a repack of a changed tree is always a new version. The function below is the SAME one
// the platform's consumer fixtures call (tooling/verify-package-fixtures.mjs -> runNugetConsumer),
// imported out of the pinned checkout — so this lane cannot resolve the feed differently from the
// platform's own consumers, and there is no copy of the hash algorithm here to drift.
const { computePackageVersion } = await import(
  pathToFileURL(path.join(platform, 'tooling/package-version.mjs')).href,
)
const packedVersion = computePackageVersion(platform)

rmSync(feed, { recursive: true, force: true })
mkdirSync(feed, { recursive: true })

for (const project of projects) {
  // No shell: true. `dotnet` is a real executable on PATH on every platform this runs on, and
  // passing args through a shell concatenates rather than escapes them (node DEP0190) - a path
  // with a space would be split rather than quoted.
  execFileSync(
    'dotnet',
    // The version goes on the command line rather than through the platform's own generated props
    // file: the pinned checkout is read-only as far as this lane is concerned, and MSBuild reads
    // the same value either way.
    ['pack', path.join(platform, project), '-c', 'Release', '--output', feed,
      `-p:HarborlinePackedVersion=${packedVersion}`],
    { stdio: 'inherit' },
  )
}

const packed = readdirSync(feed).filter(name => name.endsWith('.nupkg'))
if (packed.length === 0) throw new Error(`dotnet pack produced no nupkg in ${feed}`)

// Fail closed on the defect this closure exists to prevent: a first-party dependency that no
// packed nupkg satisfies. Restoring would succeed on a machine whose global cache happens to hold
// it and fail on a clean runner, which is the whole class of bug this script was written against.
const packedIds = new Set(packed.map(name => readNuspec(path.join(feed, name)).id))
const missing = []
for (const name of packed) {
  const { id, dependencies } = readNuspec(path.join(feed, name))
  for (const dependency of dependencies) {
    if (!/^Harborline\./.test(dependency)) continue // third-party resolves from nuget.org
    if (!packedIds.has(dependency)) missing.push(`${id} -> ${dependency}`)
  }
}

if (missing.length > 0) {
  throw new Error(
    `the local feed does not carry every first-party dependency it declares:\n  ${missing.join('\n  ')}\n`
    + 'Add the producing project to the `projects` list above. Without it a clean runner fails NU1101.',
  )
}

// Fail closed on a SECOND producer of an id this lane consumes (control ticket 105 acceptance 3).
// .feed is gitignored, so without this the only record of which repository filled it is the disk
// it was built on. Two repositories declared eight identical PackageIds; the id alone cannot tell
// them apart, but the assembly inside can. Platform now packs Harborline.* assemblies after the
// wave-1 rename; the pin makes that producer transition explicit.
// eng/platform-pin.json already pins the producing COMMIT; `producers` pins what that commit emits.
const pinPath = path.resolve(app, '../../eng/platform-pin.json')
const pin = JSON.parse(readFileSync(pinPath, 'utf8'))
const built = Object.fromEntries(
  packed
    .map(name => readNuspec(path.join(feed, name)))
    .map(({ id, assembly }) => [id, assembly])
    .sort(([left], [right]) => left.localeCompare(right)),
)
const recorded = pin.producers ?? {}
if (JSON.stringify(built) !== JSON.stringify(recorded)) {
  throw new Error(
    'the packed identities do not match eng/platform-pin.json "producers".\n'
    + `  recorded: ${JSON.stringify(recorded)}\n`
    + `  built:    ${JSON.stringify(built)}\n`
    + 'An id whose assembly changed means a different producer filled this identity. If that is\n'
    + 'intended, update "producers" in the same change that moves the pin, so the reason is legible.',
  )
}

// Fail closed when the feed does not carry the version the app is about to pin: pack honoured some
// other version (an override left in the platform tree, a stale generated props file) and every
// restore would then fail deep inside NuGet rather than here, where the cause is named.
const misversioned = packed.filter(name => !name.endsWith(`.${packedVersion}.nupkg`))
if (misversioned.length > 0) {
  throw new Error(
    `the feed was not packed at the content-derived version ${packedVersion}:\n  ${misversioned.join('\n  ')}\n`
    + "The version comes from the platform's tooling/package-version.mjs; something overrode it during pack.",
  )
}

// Record the version the feed WAS packed at where the app's MSBuild reads it (the root
// Directory.Packages.props imports this file). It lives inside the gitignored .feed because it
// describes that feed: delete the feed and the pin goes with it, so a restore can never quote a
// version no local package carries.
writeFileSync(path.join(feed, 'packed-version.props'), [
  '<!-- Generated by apps/blazor/scripts/build-local-feed.mjs. Not source; .feed is gitignored. -->',
  '<Project>',
  '  <PropertyGroup>',
  `    <HarborlinePackedVersion>${packedVersion}</HarborlinePackedVersion>`,
  '  </PropertyGroup>',
  '</Project>',
  '',
].join('\n'))

process.stdout.write(`${JSON.stringify({ feed, packed, packedVersion, projects, producers: built }, null, 2)}\n`)

/**
 * Reads a nupkg's package id, its declared dependency ids, and the assembly it carries.
 * @param {string} nupkgPath Path to the .nupkg file.
 * @returns {{id: string, dependencies: string[], assembly: string}} Identity, dependency ids, assembly file name.
 */
function readNuspec(nupkgPath) {
  const {nuspec, assembly} = readZipEntry(nupkgPath, name => name.endsWith('.nuspec') && !name.includes('/'))
  return {
    id: /<id>([^<]+)<\/id>/.exec(nuspec)?.[1] ?? path.basename(nupkgPath),
    dependencies: [...nuspec.matchAll(/<dependency\s+id="([^"]+)"/g)].map(match => match[1]),
    assembly,
  }
}

/**
 * Extracts one entry from a zip archive as UTF-8 text, plus the name of the lib assembly it
 * carries, without a zip dependency. Both come from the same walk — the assembly name is only
 * READ from the entry table, never inflated, so carrying it costs nothing.
 * @param {string} archivePath Path to the .nupkg (a zip).
 * @param {(name: string) => boolean} matches Predicate selecting the text entry.
 * @returns {{nuspec: string, assembly: string}} The entry's decoded text and the lib assembly name.
 */
function readZipEntry(archivePath, matches) {
  const raw = readFileSync(archivePath)
  let nuspec = null
  let assembly = ''
  // Walk the local file headers rather than the central directory: enough for the small, flat
  // entries a nuspec lives in, and it keeps this script dependency-free.
  for (let offset = 0; offset + 30 <= raw.length; ) {
    if (raw.readUInt32LE(offset) !== 0x04034b50) break
    const method = raw.readUInt16LE(offset + 8)
    const compressed = raw.readUInt32LE(offset + 18)
    const nameLength = raw.readUInt16LE(offset + 26)
    const extraLength = raw.readUInt16LE(offset + 28)
    const name = raw.toString('utf8', offset + 30, offset + 30 + nameLength)
    const dataStart = offset + 30 + nameLength + extraLength
    if (!assembly && /^lib\/[^/]+\/[^/]+\.dll$/.test(name)) assembly = path.posix.basename(name)
    if (nuspec === null && matches(name)) {
      const body = raw.subarray(dataStart, dataStart + compressed)
      if (method === 0) nuspec = body.toString('utf8')
      else if (method === 8) nuspec = inflateRawSync(body).toString('utf8')
      else throw new Error(`${archivePath}: entry ${name} uses unsupported compression method ${method}`)
    }

    if (compressed === 0 && method === 8) {
      // A streamed entry writes its sizes to a trailing descriptor, so the walk cannot skip it.
      throw new Error(`${archivePath}: streamed zip entry ${name} cannot be walked; repack required`)
    }

    offset = dataStart + compressed
  }

  if (nuspec === null) throw new Error(`${archivePath}: no matching entry found`)
  return {nuspec, assembly}
}
