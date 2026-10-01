# Exact package consumption and promotion - 2026-10-01

Scope: the four NuGet packages in `eng/verify-packages.sh` and `.github/workflows/packages.yml`.

Packing remains once per project. The unchanged package consumer now runs in fresh scratch space with isolated global-packages and HTTP caches, using source mapping for the four shipped IDs. Every archive restored into that cache must match the staged SHA-256. Only consumer success writes a source/version/workflow/run/attempt-bound receipt. The uploaded distribution manifest includes that receipt's hash, and the existing publication job verifies both before authority checks and pushing the same files.

## Evidence

- Eight guard tests pass for missing/wrong-version packages, staged and restored substitutions, restore/run failure, manifest/source/workflow/run/attempt mismatch and copied bundle verification.
- Four workflow-policy tests pass. The artifact expectation now includes the existing nupkg glob plus both evidence files; the original event, concurrency, proof-owner and authority assertions remain.
- A real NuGet same-ID/version poisoned ambient cache probe passes with synthetic packages and a minimal consumer. This measures cache selection, separately from the shipped consumer behavior.
- Actual four-package `bash eng/verify-packages.sh` passed for `0.1.0-preview.exactproof.20261001` on base `7a4b935`, with the unchanged secure-store assertion. The log contains pre-existing CA1014 analyzer warnings; the command exited 0.
- Oracle: byte equality, literal fixture IDs/version and the unchanged shipped secure-store assertion. No mutation evidence is claimed.

- Harborline.App.Abstractions SHA-256: `b29fbf02e8944ac5bde7495ba754778c32c5533a4b057d0cf61d3e1b810029f2`.
- Harborline.App.Blazor SHA-256: `ced6c607ec3ae765b86d160b3388beaed9a9f594e8a669acfa004a78b787b54e`.
- Harborline.App.Blazor.Hybrid SHA-256: `7c6fcffe6c2b3324081d9581dff08a3c2f313d703cb82ded513a2caa4738c898`.
- Harborline.App.Testing SHA-256: `6fd5d6ad1e5752bc4b98050b7bebd4f5425c0ae387614a39e5938a1492ecf005`.

## Limits

The receipt is same-run integrity evidence, not signing or release provenance. No package publication, release, tag, permission, credential or repository-setting action was performed. T-705/T-670 release and seed policies are not implemented by this change. Hosted CI execution is reported separately from local checks.

Existing `--skip-duplicate` behavior is preserved. This check binds the bytes supplied to the push command; it does not verify bytes already stored in a registry when a duplicate version is skipped.
