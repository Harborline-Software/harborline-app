# Agent guide — harborline-app

## Mutation testing (Stryker.NET)

Read this before you add or change a test project's `stryker-config.json`, or claim mutation evidence for a ticket. `eng/stryker.mjs` is the entry point; its header documents `check` and `run [--all]`. Scores live in `eng/stryker-baselines.json`.

- **Run one project directly.** `eng/stryker.mjs` has no single-project filter, so to mutate one project, run `dotnet stryker` from its test directory. Point `--config-file` at a copy of its config with `since` removed, and pass `--msbuild-path <sdk>/MSBuild.exe`, the path the wrapper's `msbuildArgs` computes.
- **Thresholds follow `break`.** `low = max(60, break)` and `high = max(80, break)`, and `break` is at least the floor of the recorded baseline, as in the platform (ruling 96). When a score rises, raise `break` and the baseline in the same change.
- **One config mutates one direct reference.** A test project's config mutates exactly one project that test project references directly; Stryker cannot mutate a project reached only transitively.
- **MemberData theories need `"coverage-analysis": "off"`.** Per-test coverage capture misattributes theories fed by `MemberData`, and reports covered lines as `NoCoverage`. Set the option in the project's `stryker-config.json`; Stryker.NET 5 does not accept it as a command-line flag.
- **The report is the evidence, not the exit code.** Stryker can exit 0 having mutated nothing. Read the JSON report and name the killing test for each mutant a ticket lists (T-724 ruling 35). Mutant ids change between runs, so match survivors by file, line and mutator.
- **Run Stryker natively.** Inside the Codex sandbox its restore fails for lack of network; record that and leave the run to the controller.

## Test oracles (T-724 ruling 121)

Read this before you write or change a test's expected value.

- **The expected value comes from outside the code under test.** Use a literal, the authoritative spec or design record (a constant generated from that record counts; a hand-written production constant does not), a fixture corpus the code under test does not read, a stated property, or a separately written reference implementation. The assertion must fail when the covered behaviour is violated: a literal can still assert the wrong thing, a round trip can pass when both directions share a defect, and a reference implementation can repeat the same mistaken assumption.
- **Forbidden:** reading the production symbol as its own expected value; deriving the expected result from production output in a way that preserves the defect (for example, sorting the output to get the expected order); building the expected object in the test instead of running the production builder; comparing two fixtures without running production code.
- **Required for items marked `risk: silent`** under ADR-0103 (the mark on the control design item, never inferred from its description); advisory for every other test.
- **Mutation evidence does not replace this.** A test that reads the production constant kills the mutant and still pins nothing, so name the oracle as well as the killing test.
- **Reviewers ask it on the Standards axis.** A PR that adds or changes a covering test names each oracle's source on the template's Oracle line.
