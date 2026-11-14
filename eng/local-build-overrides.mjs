// Local files that change what eng/verify.sh builds but are never in HEAD.
//
// MSBuild imports <Project>.csproj.user (Microsoft.Common.targets, $(MSBuildProjectFullPath).user), and
// vite build reads .env.local and .env.<mode>.local. .gitignore hides both kinds so they cannot be
// committed (the Vite files can hold LOCAL_NODE_SESSION_TOKEN), and a global ignore file or
// status.showUntrackedFiles=no can hide them too, so `git status` cannot be trusted to show them.
// eng/verify-receipt.mjs therefore looks on disk, independent of git, before it records a pass.
import {readdirSync} from 'node:fs'
import path from 'node:path'

// Directories that hold dependencies or build output, never a project's own override file, and the
// agent folders whose worktrees are separate checkouts with builds of their own.
const SKIPPED_DIRECTORIES = new Set(['.git', 'node_modules', 'bin', 'obj', 'artifacts', 'TestResults', 'StrykerOutput', '.claude', '.codex'])

// Case-insensitive: Windows and macOS file systems are, so MSBuild and Vite read these at any case.
const MSBUILD_USER_FILE = /\.[a-z]*proj\.user$/i
const VITE_LOCAL_ENV_FILE = /^\.env(\.[^/\\]+)?\.local$/i

export function isLocalBuildOverride(fileName) {
  return MSBUILD_USER_FILE.test(fileName) || VITE_LOCAL_ENV_FILE.test(fileName)
}

// Repository-relative paths, with forward slashes, sorted so the refusal message is stable.
export function findLocalBuildOverrides(root) {
  const found = []
  const walk = (directory) => {
    for (const entry of readdirSync(directory, {withFileTypes: true})) {
      const full = path.join(directory, entry.name)
      if (entry.isDirectory()) {
        if (!SKIPPED_DIRECTORIES.has(entry.name)) walk(full)
      } else if (isLocalBuildOverride(entry.name)) {
        found.push(path.relative(root, full).split(path.sep).join('/'))
      }
    }
  }
  walk(root)
  return found.sort()
}
