# Harborline solution overview

> **Pre-release:** See [candidate documentation readiness](release-readiness.md) for validation status and limitations. APIs, schemas, storage formats and package names may change. Source is licensed under [Apache-2.0](../LICENSE); see [NOTICE](../NOTICE) and the [trademark policy](../TRADEMARKS.md).

Harborline is intended to let domain experts define and operate enterprise work through composable, versioned models. Those models bring records, capture, rules, authority and supporting evidence into one system so an organization can change how work is done without hiding the meaning of earlier decisions.

## Who Harborline is for

| Person | What they need from Harborline | Start here |
|---|---|---|
| Operational user | Capture information, perform assigned work and inspect an accepted or refused result. | [First-use guide](getting-started.md) |
| Domain author | Define records, forms and other domain behavior, then package and validate those definitions. | [Domain-author foundations](training/domain-author-foundations.md), then the [evidence interpretation exercise](training/evidence-interpretation-exercise.md) and [candidate workflow](getting-started.md#the-candidate-workflow). |
| Reviewer | Reconstruct what happened, which evidence supported it and which authorization or validation decision applied. | [First-use guide](getting-started.md#6-read-the-record-and-its-explanation) |
| Operator | Prepare the host, establish the first administrator, manage packages and preserve the environment. | [First-release operator guide](https://github.com/Harborline-Software/harborline-api/blob/docs/solution-purpose/docs/operations/first-release-operator-guide.md), [first-use preparation](development-setup.md) and the [local-node host guide](https://github.com/Harborline-Software/harborline-api/blob/main/apps/local-node-host/README.md) |
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

## Authorities for changing facts

| Changing fact | Authoritative public location |
|---|---|
| Released version, immutable repository pins, supported scenario and downloads | The matching entry on [GitHub Releases](https://github.com/Harborline-Software/harborline-app/releases). |
| Intended host targets and implementation state | [hosts/host-manifest.json](../hosts/host-manifest.json) |
| App phase, package authority and distribution authority | [repository.yaml](../repository.yaml) |
| React application and dependency versions | [apps/react/package.json](../apps/react/package.json) and its lockfile |
| Required .NET SDK | [global.json](../global.json) |
| Platform source used to build local App feeds | [eng/platform-pin.json](../eng/platform-pin.json) |
| App package production and publication conditions | [package workflow](../.github/workflows/packages.yml) |
| Platform module and projection inventory | Platform [module catalog](https://github.com/Harborline-Software/harborline-platform/blob/main/catalog/modules.yaml) and [projection catalog](https://github.com/Harborline-Software/harborline-platform/blob/main/catalog/projections.yaml) |

For the candidate’s supported scope and outstanding evidence, see [documentation readiness](release-readiness.md). For questions and defect reports, see [support](../SUPPORT.md).
