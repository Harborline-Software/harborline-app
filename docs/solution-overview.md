# Harborline solution overview

> **Release status:** Harborline is pre-release and is not ready for production use. The selected first-release candidate is the active **M4 Workshop candidate**; it has no immutable version or release artifacts yet, and its person-driven acceptance run is still pending. APIs, schemas, storage formats and package names may change without notice. Source is licensed under [Apache-2.0](../LICENSE); see [NOTICE](../NOTICE) and the [trademark policy](../TRADEMARKS.md).

Harborline is intended to let domain experts define and operate enterprise work through composable, versioned models. Those models bring records, capture, rules, authority and supporting evidence into one system so an organization can change how work is done without hiding the meaning of earlier decisions.

## Who Harborline is for

| Person | What they need from Harborline | Start here |
|---|---|---|
| Operational user | Capture information, perform assigned work and inspect an accepted or refused result. | [First-use guide](getting-started.md) |
| Domain author | Define records, forms and other domain behavior, then package and validate those definitions. | [Domain-author foundations](training/domain-author-foundations.md), then the [evidence interpretation exercise](training/evidence-interpretation-exercise.md) and [candidate workflow](getting-started.md#the-candidate-workflow). |
| Reviewer | Reconstruct what happened, which evidence supported it and which authorization or validation decision applied. | [First-use guide](getting-started.md#6-read-the-record-and-its-explanation) |
| Operator | Prepare the host, establish the first administrator, manage packages and preserve the environment. | [First-release operator guide](https://github.com/Harborline-Software/harborline-api/blob/docs/solution-purpose/docs/operations/first-release-operator-guide.md), [first-use preparation](getting-started.md#prepare-a-source-development-host) and the [local-node host guide](https://github.com/Harborline-Software/harborline-api/blob/main/apps/local-node-host/README.md) |
| App or integration developer | Submit governed commands and queries through API contracts and consume authoritative outcomes. | [Harborline API](https://github.com/Harborline-Software/harborline-api) |

These descriptions explain responsibilities and do not create authorization roles. One person may have several responsibilities, while the running system remains responsible for deciding what that person may do.

## How the model is intended to work

A domain author combines a bounded set of concepts such as record types, forms, workflows, rules, views, reports and schedules. Definitions are versioned and transported in packages so their origin and compatibility can remain visible.

An operational action passes through the runtime interfaces that own authorization, binding, validation, persistence and the recorded result. A user interface or optional operator tool must not invent a successful outcome that the runtime did not return.

Records support claims rather than proving them automatically. Validation establishes only what its declared checks cover, and a stored claim may still need review, corroboration or later correction.

Harborline aims to preserve the source, context and interpretation of evidence so a reviewer can understand the basis of an outcome. Syntax, structural validity, shared meaning, authorization and evidential sufficiency remain separate questions.

The design favors bounded composition over unrestricted customization. A model should remain understandable and safely changeable by someone other than its original author; needs outside the bounded model may require a reusable extension or integration with a specialized system.

This is a product direction, not a claim of universal enterprise coverage. Representative-domain exercises, multi-user operation, model evolution and evidence reconstruction still need release-specific proof before broader coverage claims are made.

## Product responsibilities

| Repository | Product responsibility |
|---|---|
| [App](https://github.com/Harborline-Software/harborline-app) | Human interaction with the API: domain authoring, operational work, evidence inspection and directed Pilot assistance. React and Blazor are projection lanes for the human experience. |
| [API](https://github.com/Harborline-Software/harborline-api) | Governed programmatic access and runtime composition: commands, queries, package operations and authoritative execution results for people, integrations and automation. |
| [Platform](https://github.com/Harborline-Software/harborline-platform) | Reusable behavioral contracts and their language- and framework-specific implementations, with conformance fixtures shared across projections. |
| [Toolbox](https://github.com/Harborline-Software/harborline-toolbox) | Optional operator interface for installation, health, diagnostics and management of Harborline applications and their environment. |

Toolbox may make desktop operation more convenient, but it must not own exclusive authority or state required to run Harborline. Essential operations remain available through supported product interfaces independently of Toolbox.

## M4 Workshop candidate

The selected candidate is named **M4 Workshop candidate**. Its acceptance goal is one clean-node scenario in which a person establishes the first administrator, opens the seeded Workshop, uses one synthetic package containing a record type and bound form, validates and transports it, creates and reads a validated and authorized record, and inspects the resulting explanation in React and Blazor.

The candidate is still active: its immutable App, API and Platform pin set and complete person-driven two-lane acceptance record are pending. Current source evidence for browser reference hosts, seeded Workshop lists and shared projection contracts supports candidate testing but does not establish release support.

### Host and operating-system status

The candidate has not yet recorded release-supported hosts or operating-system versions. The [host manifest](../hosts/host-manifest.json) is the sole authority for intended targets and implementation state; registration there does not mean a target is runnable or supported. The [candidate readiness page](getting-started.md#what-is-available-now) records which parts of the first-use lesson are source-reviewed and which still need acceptance evidence.

The first release notes must name the hosts, operating-system versions and scenario actually exercised. A source build, fixture run or registered envelope is insufficient evidence for release support.

### Known candidate limitations

- [Repository metadata](../repository.yaml) records no package distribution authority and says stable distribution is not authorized.
- The unfamiliar-reader exercise and the complete M4 acceptance run have not been recorded.
- Development fixtures are isolated substitutes for selected clients; they do not start a node, persist the Workshop exercise or prove a connected runtime integration.
- Browser development credentials are supplied by the development host or server-side Blazor process. A built React bundle does not inherit the Vite development proxy or its bearer-token handling.
- Native desktop and mobile entries in the host manifest are implementation envelopes or migration targets rather than released applications.
- Screenshots are intentionally omitted until they can be sourced from the immutable candidate used for the acceptance run.

## Choose the right entry point

To evaluate the active candidate from source, use the [first-use guide and candidate readiness page](getting-started.md). It identifies which preparation is source-reviewed now and which steps still require M4 acceptance evidence.

For React or Blazor application development, use the [React guide](../apps/react/README.md) or the [Blazor development-host instructions](../README.md#run-the-blazor-development-host). Those guides describe development composition and are not end-user installation guides.

For headless node operation and package commands, use the [first-release operator guide](https://github.com/Harborline-Software/harborline-api/blob/docs/solution-purpose/docs/operations/first-release-operator-guide.md), [local-node host guide](https://github.com/Harborline-Software/harborline-api/blob/main/apps/local-node-host/README.md) and [operator CLI guide](https://github.com/Harborline-Software/harborline-api/blob/main/apps/node-operator-cli/README.md). The operator guide identifies which release-specific run, backup and recovery evidence remains pending.

For platform module authors, use the [module specifications and catalog](https://github.com/Harborline-Software/harborline-platform#specifications-and-inventory). Catalog registration describes inventory, while conformance and release evidence establish behavior.

For non-sensitive usage questions or reproducible defects, use [GitHub Issues](https://github.com/Harborline-Software/harborline-app/issues). Remove credentials, root seeds, session tokens, personal data and customer records before posting diagnostic context.

## Authorities for changing facts

| Changing fact | Authoritative public location |
|---|---|
| Released version, immutable repository pins, supported scenario and downloads | The matching entry on [GitHub Releases](https://github.com/Harborline-Software/harborline-app/releases); none exists for the M4 candidate yet. |
| Intended host targets and implementation state | [hosts/host-manifest.json](../hosts/host-manifest.json) |
| App phase, package authority and distribution authority | [repository.yaml](../repository.yaml) |
| React application and dependency versions | [apps/react/package.json](../apps/react/package.json) and its lockfile |
| Required .NET SDK | [global.json](../global.json) |
| Platform source used to build local App feeds | [eng/platform-pin.json](../eng/platform-pin.json) |
| App package production and publication conditions | [package workflow](../.github/workflows/packages.yml) |
| Platform module and projection inventory | Platform [module catalog](https://github.com/Harborline-Software/harborline-platform/blob/main/catalog/modules.yaml) and [projection catalog](https://github.com/Harborline-Software/harborline-platform/blob/main/catalog/projections.yaml) |

This overview does not copy version numbers from those authorities. Release notes should link to the exact manifests and receipts they used so a changing fact has one maintained home.

## Documentation validation status

The earlier App README routed readers directly to implementation guides without a release-specific decision point. This overview and the first-use guide resolve that ambiguity by separating role-based entry paths, intended targets, source-development evidence and release-supported behavior.

Link and command targets in these two guides were checked against the current source trees. The required unfamiliar-reader exercise remains pending and must be recorded against the immutable M4 candidate before the first release can mark this guide supported.
