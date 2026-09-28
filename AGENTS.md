# Agent guide — harborline-app

## Mutation testing (Stryker.NET)

Read this before you add or change a test project's `stryker-config.json`, or claim mutation evidence for a ticket. `eng/stryker.mjs` is the entry point; its header documents `check` and `run [--all]`. Scores live in `eng/stryker-baselines.json`.

- **Run one project directly.** `eng/stryker.mjs` has no single-project filter, so to mutate one project, run `dotnet stryker` from its test directory. Point `--config-file` at a copy of its config with `since` removed, and pass `--msbuild-path <sdk>/MSBuild.exe`, the path the wrapper's `msbuildArgs` computes.
- **Thresholds follow `break`.** `low = max(60, break)` and `high = max(80, break)`, and `break` is at least the floor of the recorded baseline, as in the platform (ruling 96). When a score rises, raise `break` and the baseline in the same change.
- **One config mutates one direct reference.** A test project's config mutates exactly one project that test project references directly; Stryker cannot mutate a project reached only transitively.
- **MemberData theories need `"coverage-analysis": "off"`.** Per-test coverage capture misattributes theories fed by `MemberData`, and reports covered lines as `NoCoverage`. Set the option in the project's `stryker-config.json`; Stryker.NET 5 does not accept it as a command-line flag.
- **The report is the evidence, not the exit code.** Stryker can exit 0 having mutated nothing. Read the JSON report and name the killing test for each mutant a ticket lists (T-724 ruling 35). Mutant ids change between runs, so match survivors by file, line and mutator.
- **Run Stryker natively.** Inside the Codex sandbox its restore fails for lack of network; record that and leave the run to the controller.
