# Evidence interpretation exercise

> **Source-reviewed candidate draft:** use the paper packet now; use an app build only after the release owner identifies the M4 candidate and supplies its acceptance evidence.

This exercise checks whether a learner can separate captured facts from consequential conclusions and can inspect a refusal without inventing missing evidence.

## Setup

Allow 30 minutes plus a 10-minute discussion, give the learner this page without the answer guide at first, and use only synthetic identifiers.

The safest delivery is the paper exercise because it performs no runtime writes and needs no credentials.

If the facilitator also demonstrates the app, use an explicitly configured disposable fixture build and label it **fixture**, or use a connected disposable environment authorized by the release owner and keep every exercise step read-only.

Do not run **Restore as draft** against a connected tenant for this exercise, because it creates a new draft that syncs to peers and does not erase history ([Forms restore behavior](../../apps/react/src/admin/forms/FormsAdminPage.tsx)).

## Packet

All packet content is synthetic and intentionally incomplete, and its example HTTP statuses and reason codes are illustrative rather than a guarantee of the selected release's wire contract.

| Artifact | Training value |
|---|---|
| Pack | `facility-safety@2.4.0` |
| Record definition | `equipment-inspection@1.2.0` requires `assetId`, `inspectedAt`, `inspectorId`, and `guardStatus`; status is `present`, `absent`, or `unknown` |
| Form definition | `daily-inspection@3.0.0` captures the four required fields plus optional `photoReference` and `note` |
| View definition | `inspection-review@1.1.0` lists asset, time, inspector, and guard status; it filters `unknown` and `absent` into an attention list |
| Record A | `pump-17`, `2026-09-12T14:10:00Z`, `operator-8`, `guardStatus=unknown`, no photo, `note="access panel was locked"` |
| Record B | `pump-18`, `2026-09-12T14:18:00Z`, `operator-8`, `guardStatus=present`, `photoReference=photo-882`, no note |
| Refusal C | HTTP 403, `code=authorization.permission_required`, `permission=records:write`; no audit id is present |
| Refusal D | HTTP 422, `code=entity.validation.body_invalid`, pointer `/guardStatus`; caller authority is otherwise established |

The packet says nothing about a workflow, risk formula, automatic follow-up, or inspection schedule, so treat each of those as conceptual.

## Learner tasks

1. Label each packet item as definition, record, pack, view, authorization result, or validation result, and say which items carry domain meaning versus implementation behavior.
2. Write three claims that Record A supports and three conclusions it does not support.
3. Decide whether Record A justifies the statement "pump-17 is safe to operate" and name the smallest additional evidence or decision authority needed before making that statement.
4. Explain what the attention-list view adds and what it cannot add.
5. For Refusal C, state what you would inspect, what you would change first, and what you cannot infer about the submitted body.
6. For Refusal D, state what you would inspect, what you would change first, and why requesting a broader role is not the first remedy.
7. Mark these proposed additions as release-supported or conceptual: an automatic shutdown workflow, a risk score calculated from guard status, a next-day reinspection schedule, the Forms definition/history panel, and the Views definition/provenance panel.

## Expected outcomes

Award one point for each outcome, for ten points total.

- The learner distinguishes reusable versioned definitions from captured record instances.
- The learner separates the declarative pack from the libraries that render general contracts.
- The learner says Record A contains an explicit `unknown` rather than treating it as `absent` or blank.
- The learner refuses to conclude that pump-17 is safe or unsafe from Record A alone.
- The learner says the view selects and presents recorded values but creates no new observation.
- The learner identifies Refusal C as authorization and does not infer body validity.
- The learner identifies Refusal D as validation and follows its pointer before changing authority.
- The learner asks for the governing definition identity and version when interpreting retained evidence.
- The learner marks workflow, calculation, and schedule additions as conceptual.
- The learner preserves unavailable or incomplete trace status instead of reconstructing an explanation.

A score of 8–10 with both refusal outcomes correct shows the learner can proceed with supervised domain authoring; a lower score or either refusal confused with the other calls for a repeat discussion using a new synthetic example.

## Answer guide

### Task 1

The pack groups and transports declarative domain content; the record, form, and view definitions carry versioned domain meaning; Records A and B are captured instances; Refusals C and D are runtime results; rendering and validation libraries are implementation behavior and are not named as packet artifacts.

### Tasks 2 and 3

Record A supports that `operator-8` recorded an inspection for `pump-17` at the stated time, selected the allowed value `unknown`, and recorded that the access panel was locked, subject to retained provenance and ordinary trust in the capture path.

Record A does not support that the guard was absent, that the pump was safe, or that the inspection was complete enough for a safety decision; it also does not prove the note is accurate merely because the shape validated.

The smallest missing basis depends on the organization's policy, but an acceptable answer names a completed observation of the inaccessible guard plus the identified policy or authorized person that turns observations into an operating decision.

### Task 4

The view adds a repeatable selection and presentation of records whose captured status is `unknown` or `absent`; it cannot observe the guard, fill the missing photo, decide safety, or prove that every relevant record is present.

### Tasks 5 and 6

For Refusal C, inspect the operation, scope, required permission, caller, UTC time, and a linked trace only if an audit id exists; review the caller's grant with an administrator, and do not infer whether the submitted body would validate because authorization stopped the attempt first.

For Refusal D, inspect `/guardStatus` against the active record definition and correct the value or omission through the same supported interface; broader authority does not repair a definition-invalid body.

### Task 7

The automatic workflow, calculated risk score, and reinspection schedule are conceptual, while the source-reviewed Forms definition/history and Views definition/provenance panels exist in the current React source; their inclusion in a selected release still needs M4 candidate evidence.

## Facilitator note

Ask "What exactly establishes that?" whenever the learner moves from a displayed value to a conclusion, and ask "Which refusal happened first?" whenever they propose changing both data and permissions.

Do not reward a confident operational recommendation that lacks a named decision authority, even when the recommendation sounds prudent, because the exercise assesses evidential sufficiency rather than safety expertise.

The current Views panel is a useful provenance demonstration because it shows parameters, provenance, and version history without offering a restore action ([Views detail behavior](../../apps/react/src/admin/views/ViewDefinitionDetail.tsx)).

## Safe reset

For the paper exercise, destroy or retain the worksheet according to the training record policy; there is no Harborline state to reset.

For an explicitly configured fixture build, close and reload the whole app to recreate the in-memory fixture clients; confirm the original fixture rows before the next learner, and never describe this as connected persistence or recovery ([fixture client selection](../../apps/react/src/admin/forms/client/index.ts)).

For a connected environment, this exercise authorizes read-only inspection only, so reset means closing panels and ending the session; if a learner creates a draft or record anyway, stop the exercise and route cleanup to the environment owner rather than deleting history or promising reversal.

## Pilot record

**Status: not run.** A reader unfamiliar with the model has not yet completed this material, so the ticket's pilot-and-revise acceptance item remains open.

After a real pilot, record the date, selected release identity, delivery mode, learner role without personal data, completion time, score, misconceptions in the learner's own words, facilitator observations, document changes, and a link to the approved evidence record.

Do not replace the status above with "passed" until the reader actually completes the exercise and the recorded revisions are present in this document.
