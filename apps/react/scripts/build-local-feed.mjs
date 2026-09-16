#!/usr/bin/env node
// Packs the platform's React UI package into a local content-addressed feed and installs it.
//
// Why a packed tarball rather than a workspace or path reference: the app must consume the
// PACKAGE surface, not the source tree. A source reference resolves dist/ and style.css from
// disk and would render correctly while the published package carried neither - which is
// exactly the defect the Blazor lane shipped (assets present in obj/, absent from the served
// app). Packing proves the files listed in "files" are the files the app actually gets.
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { gunzipSync } from 'node:zlib'

const here = path.dirname(fileURLToPath(import.meta.url))
const app = path.resolve(here, '..')
const platform = process.env.HARBORLINE_PLATFORM_REPO
  ?? path.resolve(app, '../../../harborline-platform')
// The rail's role gate (app-shell revision 2, control ticket 220) types its vocabulary through
// the platform's contracts package, so the feed carries that package beside the UI one.
const packages = [
  path.join(platform, 'projections/react/ui/hlp.ui.button'),
  path.join(platform, 'projections/typescript/contracts/hlp.contracts.forms'),
]
const feed = path.join(app, '.feed')

rmSync(feed, { recursive: true, force: true })
mkdirSync(feed, { recursive: true })

// BUILD before packing, always. The package declares "files": ["dist", ...] and has no prepack
// script, and dist/ is gitignored in the platform — so on a fresh checkout `npm pack` alone
// produces a tarball containing no code whatsoever, and the app installs an empty package. It
// only ever appeared to work here because a developer machine has a dist/ left over from earlier.
// Building unconditionally also removes the subtler version of the same hazard: packing a STALE
// dist and shipping yesterday's component while today's source sits beside it (control ticket 089).
// npm is a .cmd shim on Windows, and since CVE-2024-27980 Node refuses to spawn one without
// shell: true (EINVAL). The shell then CONCATENATES arguments rather than escaping them, so any
// argument that could contain a space is quoted here explicitly.
const shell = process.platform === 'win32'
const quote = value => (shell && /\s/.test(value) ? `"${value}"` : value)
const run = (cwd, ...args) =>
  execFileSync('npm', args.map(quote), { cwd, stdio: 'inherit', shell })
for (const pkg of packages) {
  if (existsSync(path.join(pkg, 'pnpm-lock.yaml'))) {
    execFileSync('pnpm', ['install', '--frozen-lockfile'], { cwd: pkg, stdio: 'inherit', shell })
  } else run(pkg, 'ci')
  run(pkg, 'run', 'build')
  run(pkg, 'pack', '--pack-destination', feed)
}
const tarballs = readdirSync(feed).filter(name => name.endsWith('.tgz'))
if (tarballs.length !== packages.length) throw new Error(`npm pack produced ${tarballs.length} tarballs in ${feed}, expected ${packages.length}`)

// Fail closed on the exact defect this script exists to prevent: a tarball whose dist/ is absent.
// The listing is read with Node rather than by shelling out to tar, because GNU tar on Git Bash
// reads a Windows path as a remote host spec and answers "Cannot connect to C".
for (const tarball of tarballs) {
  const distEntries = listEntries(path.join(feed, tarball)).filter(name => name.startsWith('package/dist/'))
  if (distEntries.length === 0) {
    throw new Error(`${tarball} contains no package/dist/ entries — the package would install with no code.`)
  }
}

/**
 * Lists the member names of a gzipped tar archive.
 * @param {string} archivePath Path to the .tgz file.
 * @returns {string[]} Every member name in the archive.
 */
function listEntries(archivePath) {
  const raw = gunzipSync(readFileSync(archivePath))
  const names = []
  for (let offset = 0; offset + 512 <= raw.length; ) {
    const name = raw.toString('utf8', offset, offset + 100).replace(/\0.*$/, '')
    if (name === '') break // two zero blocks terminate the archive
    const size = Number.parseInt(raw.toString('ascii', offset + 124, offset + 136).replace(/\0.*$/, '').trim(), 8) || 0
    names.push(name)
    offset += 512 + Math.ceil(size / 512) * 512
  }

  return names
}

// Unpack each tarball into .feed/<name>/ and point the manifest's file: dependency at that
// DIRECTORY. pnpm records a directory dependency as a link with no integrity hash, so a rebuilt
// feed never invalidates the lockfile; a tarball dependency carries an integrity hash that changes
// with every rebuild, and the refresh that fixed that under npm (--package-lock-only) dirtied the
// attested tree under pnpm. The bytes are built locally from platform sources whose real pin is the
// git commit in eng/platform-pin.json, not a checksum.
for (const tarball of tarballs) {
  const target = path.join(feed, tarball.replace(/^harborline-software-/, '').replace(/-\d.*$/, ''))
  rmSync(target, { recursive: true, force: true })
  extract(path.join(feed, tarball), target)
}

/**
 * Extracts a gzipped tar archive, stripping the leading package/ segment.
 * @param {string} archivePath Path to the .tgz file.
 * @param {string} target Directory to write into.
 */
function extract(archivePath, target) {
  const raw = gunzipSync(readFileSync(archivePath))
  for (let offset = 0; offset + 512 <= raw.length; ) {
    const name = raw.toString('utf8', offset, offset + 100).replace(/\0.*$/, '')
    if (name === '') break
    const size = Number.parseInt(raw.toString('ascii', offset + 124, offset + 136).replace(/\0.*$/, '').trim(), 8) || 0
    const type = raw.toString('ascii', offset + 156, offset + 157)
    const relative = name.replace(/^package\//, '')
    // The archives are packed here from the pinned platform checkout, but an extractor still refuses
    // an entry that would land outside its target (tar slip) and writes only regular files.
    const file = path.resolve(target, relative)
    if (path.isAbsolute(relative) || relative.split('/').includes('..') || !file.startsWith(path.resolve(target) + path.sep)) {
      throw new Error(`${path.basename(archivePath)}: entry escapes the feed directory: ${name}`)
    }
    if (type === '0' || type === '\0' || type === '') {
      mkdirSync(path.dirname(file), { recursive: true })
      writeFileSync(file, raw.subarray(offset + 512, offset + 512 + size))
    }
    offset += 512 + Math.ceil(size / 512) * 512
  }
}

process.stdout.write(`${JSON.stringify({ feed, tarballs, sources: packages }, null, 2)}\n`)
process.stdout.write('\nInstall them with:\n  pnpm install --frozen-lockfile\n')
