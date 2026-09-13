# Facilitate the evidence interpretation exercise

Give the learner [foundations](domain-author-foundations.md) and the [exercise](evidence-interpretation-exercise.md). Keep this guide separate until discussion.

## Delivery

Use the paper packet first. Allow written or spoken answers, split the exercise across sessions if useful, and record interruptions. The suggested thirty minutes plus discussion is provisional; revise it from observed learner experience.

The lesson assesses how someone reasons about records, definitions and refusals. It does not assess equipment-safety expertise or qualify someone to author a production domain.

## Answer guide

### Task 1: Meaning and implementation

The pack groups domain content. The record, form and view definitions carry reusable domain meaning. The hypothetical rendering library implements behavior. A and B are record instances; C and D are authorization and validation refusals. A view definition is a definition, not an observation.

### Task 2: Claims and provenance

Record A names `pump-17`, assigns guard status `unknown` and contains the locked-panel note. Other accurate descriptions of its supplied fields are acceptable.

It does not establish who submitted the record, the submission time, the accuracy of the note, the physical guard condition or operating safety. `inspectorId` and `inspectedAt` are asserted values, not authenticated authorship or capture-time evidence. Retain `equipment-inspection@1.2.0` when interpreting the record; do not silently apply a newer definition.

### Task 3: Sufficiency and handoff

The packet cannot establish safe operation or define the minimum sufficient evidence. An answer should identify missing observations and relevant context, plus the organization’s policy and assigned decision authority. Do not require a learner to invent a safety policy.

An acceptable handoff is: “The record lists guard status as unknown, includes a note stating the access panel was locked and supplies no photo reference. The packet contains no operating decision; its sufficiency needs review under the organization’s applicable policy by the assigned decision maker.”

In real work, staff follow their workplace procedures. The exercise’s evidential limit is not an instruction to ignore a possible hazard.

### Task 4: Presentation and reassuring fields

The view consistently selects and presents records. It adds no observation and does not establish that every relevant record is present.

Record B reports `present` and references `photo-882`. It does not establish the image’s availability, identity, timing, authenticity or adequacy for an operating decision. A completed field is still a claim.

### Task 5: Permission

Check the intended operation, caller and scope before discussing access with the assigned access owner. The refusal does not show that the caller is entitled to more permission; leaving access unchanged may be correct. The access owner may be the learner, but ownership does not confer permission. If the designated person cannot establish the cause or use a documented remedy, preserve the limitation and use [support](../../SUPPORT.md).

Body validity remains unknown. No supplied audit identifier means no trace can be claimed from this packet. If another environment supplies an incomplete or unreadable explanation, preserve that limitation.

### Task 6: Validation

Inspect the rejected value at `/guardStatus` against the governing definition. The packet omits that value, so it cannot justify a specific replacement.

Correct a transcription or omission only from available evidence. Preserve `unknown` when that is truthful and allowed; if the definition cannot represent known facts, pause for authorized review of the model. Do not invent an observation or request broader access to make validation pass. A model revision needs an explicit meaning, version and review of affected uses; it cannot retroactively validate the refused attempt.

For the optional rewrite, “guard status unknown; I could not observe it” preserves uncertainty. Add a reason only if actually known.

### Task 7: Product claims

The workflow, score and schedule are conceptual proposals. The Forms and Views claims have no source or release evidence in the packet, so their status is not established here.

Source inspection could establish implementation at a named revision. Calling behavior release-supported additionally requires the selected release’s scope and acceptance evidence. Reward “insufficient evidence” rather than guesses based on product vocabulary.

## Review each answer

Use this table for feedback, not a numerical pass threshold. Mark each row **demonstrated**, **needs discussion** or **not observed**, quoting the answer that supports the judgment.

| Task | Observable reasoning |
|---|---|
| 1 | Distinguishes domain definitions, implementation, records and runtime refusals. |
| 2 | Describes supplied values without promoting them into verified authorship or observations; retains definition version. |
| 3 | Identifies missing observations/context and policy/authority without inventing a sufficient minimum; gives a neutral handoff. |
| 4 | Explains that presentation and attachment references add no independent observation. |
| 5 | Checks before access changes; leaves body validity and missing trace unestablished. |
| 6 | Checks the rejected value before correction and preserves truthful uncertainty. |
| 7 | Separates proposals from unsubstantiated product claims and requests release evidence. |

For a partially correct answer, record which part needs discussion rather than averaging the misconception away.

## Respond to misconceptions

| Learner response | Discussion and retry |
|---|---|
| “Unknown means absent.” | Ask what was observed. Retry with an inaccessible area and a truthful uncertainty value. |
| “It passed validation, so it is true.” | Ask what the check actually covers. Retry with a validly formatted but mistaken asset identifier. |
| “It appears in the view, so a decision was made.” | Separate selection from authorization. Ask where the decision and its authority are recorded. |
| “Give me more permission” or “choose a value that passes.” | Ask which fact establishes that change is appropriate. Retry without assuming a change is justified. |
| “The timestamp proves when it happened.” | Separate stated event time, entry time and independently observed time. Ask which the packet provides. |

Optional transfer prompts can connect the distinctions to the learner’s work. Keep these hypothetical; they describe no shipped domain package or regulatory requirement.

- Factory manager: write a handoff that separates an inspection entry from an operating decision.
- Fast-food worker: distinguish a checklist entry from independent confirmation that a task was done.
- Bank teller: distinguish a person named in a transaction record from the authenticated person entering it.
- Field-service technician: distinguish an observation made on site from an entry made later.
- Small-business owner: explain which evidence supports a decision when one person captures, reviews and approves it.

## Optional demonstration and reset

A demonstration is separate from the paper exercise. Use only an identified disposable environment and the selected candidate’s documented instructions. Learners observe; this exercise does not authorize creating drafts, records or changing permissions.

Before a fixture demonstration, record the build and fixture identity and expected starting rows. Reload only according to that fixture’s documented reset behavior and confirm the expected rows before reuse. A fixture cannot prove connected persistence, sync or recovery.

For a connected demonstration, keep it read-only and end the session afterward. If anyone writes state accidentally, stop, record the deviation and ask the environment owner to handle it through a supported procedure. Do not describe that run as an unchanged read-only demonstration.

Paper answers create no Harborline state. Retain or dispose of worksheets according to the training record policy.

## Record the pilot

Use the [training pilot record](../release-readiness.md#training-pilot) to capture baseline familiarity, delivery mode, responses, misconceptions and revisions. AI role reviews and facilitator self-runs are editorial checks, not an unfamiliar-human pilot. An owner’s review is useful, but must be recorded as such if the owner already understands the model.
