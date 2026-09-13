# Domain-author foundations

> **Source-reviewed candidate draft:** this lesson describes concepts and surfaces present in the current source, but the selected M4 candidate has not completed its person-driven React and Blazor acceptance run.

Use this lesson before the [evidence interpretation exercise](evidence-interpretation-exercise.md); allow about 20 minutes for the lesson and 30 minutes for the exercise.

## What a domain author is deciding

A domain author gives shared names and structure to work so that two people, two screens, and two releases mean the same thing when they read the same data.

Authoring does not make a business conclusion true; it defines what can be captured, how it can be presented, and which later decision has enough evidence to proceed.

## Five distinctions

### Definition and record

A **definition** is versioned reusable meaning, such as the fields and constraints of an equipment inspection, while a **record** is one captured instance, such as inspection `inspection-1042` for asset `pump-17` at a stated time.

Changing a definition does not rewrite the meaning of an older record, so keep the definition identity and version available when interpreting retained evidence.

The current Forms surface can list form definitions, show published and historical versions, and create a new draft derived from a chosen version; its confirmation explicitly says that history is not modified and nothing is published ([React Forms surface](../../apps/react/src/admin/forms/FormsAdminPage.tsx)).

### Pack and library

A **pack** carries declarative domain content that can be identified and versioned, such as record, form, and view definitions for a line of work.

A **library** carries reusable implementation that understands a general contract, such as the code that renders a form or grid; a library is not the tenant's domain model and installing code is not evidence that a particular domain definition is active.

When reviewing a screen, ask which pack and definition supplied the meaning, then which library rendered it; those are different provenance questions.

### Capture and interpretation

A form **captures** values into a record under a definition, while a view **presents** selected recorded values for a task.

An interpretation is a separate conclusion drawn from those values, and it must name its rule, threshold, human judgement, or other authority rather than borrowing confidence from the form's successful submission.

The current Views surface exposes a view's key, version, kind, cascade layer, parameters, provenance, and version history, but it does not turn the displayed rows into a domain conclusion ([React Views surface](../../apps/react/src/admin/views/ViewDefinitionDetail.tsx)).

### Permission and validation

**Permission** answers whether this caller may attempt an operation in this scope, while **validation** answers whether the submitted information satisfies the active definition.

Permission is checked before record-body validation on the supported record-write path, so an authorization refusal does not establish whether the hidden body was valid or invalid.

A valid body can still be refused for lack of permission, and a permitted caller can still receive a validation refusal with a reason code and pointers to failing members.

### Evidence and conclusion

Captured evidence establishes only what its provenance, fields, timestamps, signatures, and validation result actually support.

A saved value does not by itself prove that the value was observed accurately, that omitted facts were false, that conditions have not changed, that one event caused another, or that a consequential action is safe.

Treat `unknown`, `not observed`, and `not applicable` as meanings rather than empty space when the definition distinguishes them; do not convert a blank or unavailable value into `no`.

## Synthetic worked example

The names and values in this example are synthetic training material and are not shipped Harborline definitions.

Pack `facility-safety@2.4.0` carries record definition `equipment-inspection@1.2.0`, form definition `daily-inspection@3.0.0`, and view definition `inspection-review@1.1.0`; the rendering libraries know how to present the form and view without owning those facility-specific meanings.

The form requires `assetId`, `inspectedAt`, `inspectorId`, and `guardStatus`, where `guardStatus` is one of `present`, `absent`, or `unknown`; `photoReference` and `note` are optional.

Record `inspection-1042` contains `assetId=pump-17`, `inspectedAt=2026-09-12T14:10:00Z`, `inspectorId=operator-8`, `guardStatus=unknown`, no photo reference, and `note="access panel was locked"`.

The record establishes that a permitted caller captured a definition-valid inspection record with an explicit unknown guard status and a note at the stated time, assuming the retained provenance identifies the governing definition version.

The record does not establish that the guard was present, absent, or defective; it also does not establish that the pump was safe to operate, because the observer could not inspect the relevant area and no separate safety decision is present.

The review view may correctly display this record in an "attention needed" list, but that presentation still does not prove why the guard was unknown or authorize an operational response.

A rule that automatically stops the pump, a workflow that assigns an investigator, a calculation that scores risk, or a schedule that repeats the inspection is **conceptual in this lesson** and must not be represented as selected-release behavior without separate release evidence.

## Inspect a refusal without over-reading it

1. Record the operation, UTC time, tenant or scope, and the visible stable reason code without copying secrets or the refused record body.
2. Decide whether the refusal is authorization or validation before changing data: `authorization.permission_required` describes authority, while an entity-validation code and JSON pointers describe the submitted shape.
3. If the app shows a linked **Why can I do this?** disclosure, open it and read the recorded act, roles in force, standings, verdict, and deciding grant; the disclosure appears only when the response carries an audit id ([authorization trace](../../apps/react/src/authorization/AuthorizationTrace.tsx)).
4. If no audit id is linked, do not claim that a trace exists; if the trace says it was not recorded, is incomplete or unsupported, or cannot be read with the current permission, preserve that exact limitation.
5. Correct only the problem the refusal establishes, retry through the same supported interface, and keep the original refusal as evidence rather than turning a later success into proof that the original attempt was acceptable.

The app's admin clients render a stable error code plus named detail values when the node supplies them and fall back to the HTTP status text when it does not ([admin error envelope](../../apps/react/src/admin/adminErrorEnvelope.ts)).

## Current surface boundary

The source-reviewed React Forms surface lists definitions and histories and offers **Restore as draft**, while the source-reviewed Views surface is read-only and lists definition detail and history.

Fixture-backed behavior is training or development evidence only; the repository's release banner says there are no supported installs yet, and a successful fixture action does not establish a working connected integration ([release status](../../README.md)).

General definition creation, progressive capture, automatic evidence completion, workflow execution, risk calculations, and scheduled follow-up are outside this lesson unless the selected release's acceptance evidence names them.

## Check your understanding

You are ready for the exercise when you can explain why a form submission can be valid but inconclusive, why a view cannot create evidence that the underlying record lacks, and why permission and validation refusals require different remedies.
