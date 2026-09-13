# Evidence interpretation exercise

Practice explaining what a record supports, what evidence is missing and what to check before changing a refused entry. Use only the synthetic packet below.

> **Paper exercise:** No app, login or writes are needed. This packet does not establish shipped Harborline behavior, offline operation or recovery. It does not ask you to make a real equipment-operating decision.

Read [domain-author foundations](domain-author-foundations.md) first. Answer in short sentences or discuss your answers aloud with someone who records them. You may pause between the three parts. Suggested timing is provisional: about ten minutes per part, followed by discussion.

Keep the [facilitator guide](facilitator-guide.md) closed until you finish. Note unfamiliar terms and points of confusion alongside your answers.

## Packet

### Model

- **Pack:** `facility-safety@2.4.0` groups the following definitions.
- **Record definition:** `equipment-inspection@1.2.0` requires `assetId`, `inspectedAt`, `inspectorId` and `guardStatus`. Guard status may be `present`, `absent` or `unknown`.
- **Form definition:** `daily-inspection@3.0.0` captures those fields and optional `photoReference` and `note`.
- **View definition:** `inspection-review@1.1.0` presents asset, stated time, inspector and guard status, selecting `unknown` and `absent` for an attention list.
- **Rendering library:** hypothetical `inspection-renderer@1.0.0` implements form and view rendering. It is an implementation example, not a shipped Harborline package.

### Records

**Record A** names asset `pump-17`, states `inspectedAt=2026-09-12T14:10:00Z`, names inspector `operator-8`, and sets `guardStatus=unknown`. It has no photo reference and contains `note="access panel was locked"`.

**Record B** names asset `pump-18`, states `inspectedAt=2026-09-12T14:18:00Z`, names inspector `operator-8`, and sets `guardStatus=present`. It contains `photoReference=photo-882` and no note.

Neither record includes an authenticated submitter, submission timestamp, permission or validation receipt, verified image, or operating decision. Treat those as missing evidence.

### Refusals

**Refusal C** reports HTTP 403 and `authorization.permission_required`, naming `records:write`: a permission problem. It supplies no audit identifier. The submitted body, caller and scope are not provided.

**Refusal D** reports HTTP 422 and `entity.validation.body_invalid`, with pointer `/guardStatus`: a submitted-value problem at that field. For this illustrative result, caller authority was established first. The rejected value is not provided.

The codes are illustrative. A reason code identifies a category; the pointer identifies a field. Neither supplies the missing facts.

Source evidence identifies the inspected revision. Release evidence identifies the selected release, its scope and an observed acceptance result.

## Part 1: Identify what you have

**1.** Classify the packet items as pack, record definition, form definition, view definition, implementation library, record instance, authorization refusal or validation refusal. Which items carry reusable domain meaning, and which implement behavior?

Pause here if needed.

## Part 2: Read without adding evidence

**2.** Give three statements about what Record A contains and three things it does not establish. Distinguish the named inspector and stated inspection time from the authenticated submitter and submission time. Identify the definition version you would retain when interpreting it.

**3.** Can this packet establish that `pump-17` is safe to operate? Name missing observations, context and decision authority. Explain why the packet cannot define a sufficient minimum for that decision. Then write a two-sentence handoff separating the recorded uncertainty from any operating decision, without recommending an operating action.

**4.** What does the attention view add? Does Record B’s `present` value and photo reference prove the photo is accessible, depicts the stated asset at the stated time, or establishes operating safety?

Pause here if needed.

## Part 3: Check before changing

**5.** For Refusal C, what would you check before any access change? What can you infer about body validity? Can you claim a recorded trace when no audit identifier is supplied?

**6.** For Refusal D, what would you inspect before changing a value? Can this packet justify a particular replacement? Explain what to do if the truthful answer is `unknown` or the definition cannot represent what you know.

**7.** Someone proposes an automatic shutdown workflow, a calculated risk score and a reinspection schedule, and says the App has Forms history and Views provenance panels. Classify each claim as a conceptual proposal or as behavior whose source/release status is not established by this packet. What evidence would you need before calling any of them release-supported?

Optional practice: rewrite “guard absent because I could not see it” to preserve only what was known.

## After the exercise

Compare your reasoning with the [answer guide](facilitator-guide.md#answer-guide). Record the time spent, unclear terms and any question that seemed impossible or unfair. If a demonstrated App screen differs from the packet, answer from the packet and record the difference; do not improvise writes.
