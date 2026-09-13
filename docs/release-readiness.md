# M4 Workshop documentation readiness

This page records the evidence still needed to publish the candidate guides as release-supported instructions. It is a documentation assessment, not a replacement for the release acceptance runbook. The [solution overview](solution-overview.md) explains the product; the [first-use guide](getting-started.md) describes the intended learning flow.

## Candidate scope

The selected candidate is named **M4 Workshop candidate**. Its acceptance goal is one clean-node scenario in which a person establishes the first administrator, opens the seeded Workshop, uses one synthetic package containing a record type and bound form, validates and transports it, creates and reads a validated and authorized record, and inspects the resulting explanation in React and Blazor.

The candidate is still active: its immutable App, API and Platform pin set and complete person-driven two-lane acceptance record are pending. Current source evidence for browser reference hosts, seeded Workshop lists and shared projection contracts supports candidate testing but does not establish release support.

### Host and operating-system status

The candidate has not yet recorded release-supported hosts or operating-system versions. The [host manifest](../hosts/host-manifest.json) is the sole authority for intended targets and implementation state; registration there does not mean a target is runnable or supported. The [lesson readiness table](#lesson-readiness) records which parts of the first-use lesson are source-reviewed and which still need acceptance evidence.

The first release notes must name the hosts, operating-system versions and scenario actually exercised. A source build, fixture run or registered envelope is insufficient evidence for release support.

### Known candidate limitations

- [Repository metadata](../repository.yaml) records no package distribution authority and says stable distribution is not authorized.
- The unfamiliar-reader exercise and the complete M4 acceptance run have not been recorded.
- Development fixtures are isolated substitutes for selected clients; they do not start a node, persist the Workshop exercise or prove a connected runtime integration.
- Browser development credentials are supplied by the development host or server-side Blazor process. A built React bundle does not inherit the Vite development proxy or its bearer-token handling.
- Native desktop and mobile entries in the host manifest are implementation envelopes or migration targets rather than released applications.
- Screenshots are intentionally omitted until they can be sourced from the immutable candidate used for the acceptance run.

## Lesson readiness

| Part of the lesson | Current evidence | Status in this guide |
|---|---|---|
| App and node prerequisites | SDK, package and source-build manifests exist. | Source-reviewed [development setup](development-setup.md). |
| Installation account and first administrator | A local credential ceremony creates the installation account and root installation grant; a separate installer path establishes or projects the signed live-roster party as administrator. | Boundaries documented; the complete release invocation still needs acceptance evidence. |
| Seeded Workshop lists | React and Blazor source render package-provided list definitions through their projection lanes. | Candidate behavior; full lane run pending. |
| Validate, export, verify, install and activate a small pack | The node routes, operator commands and synthetic Notes fixture exist; the interactive workflow is still changing in the active M4 task. | Sequence documented; do not treat it as a passed exercise. |
| Create and read a form-bound record | Candidate implementation and review are still in progress. | Expected flow only; exact UI and response examples await the accepted candidate. |
| Authorization and validation explanation | The candidate intends to return an audit identifier for an accepted write and field pointers for validation refusals. | Expected flow only; example output awaits the accepted candidate. |

## Publication evidence

Before promoting these guides, record the immutable App, API and Platform revisions, supported environments, exact startup and cleanup procedures, accepted sample package, observed action labels and response examples. Use screenshots from that same candidate. Confirm that an unfamiliar reader can follow the public instructions in both React and Blazor.

The startup evidence must cover the signed live-roster member, installer-establishment result, disposable data directory and exact node launch command. Creating installation credentials alone does not establish the administrator.

## Walkthrough observations

For each lane, record the release identifier, App/API/Platform pins from release notes, operating system, browser, exact starting page, action labels used and whether the final record and explanation were visible. Do not include the password hash, session token, root seed, personal data or record body.

Report confusing steps, broken public links and sanitized errors in [GitHub Issues](https://github.com/Harborline-Software/harborline-app/issues). Functional M4 defects remain with the implementation task; documentation observations should describe the point of confusion without silently changing the acceptance sequence.
