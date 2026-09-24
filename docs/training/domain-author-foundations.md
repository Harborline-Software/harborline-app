# Domain-author foundations

Learn to tell what a form entry supports, what it leaves unknown, and what to check when an entry is refused. Domain authors use these distinctions to design models that other people can understand; people who enter or review records use them to avoid reading more into a result than it establishes.

> **Learning scope:** The example is synthetic. It does not define equipment-operating policy or claim that its model ships in Harborline. App demonstrations require the evidence listed in [documentation readiness](../release-readiness.md).

This is a paper lesson in interpretation. Creating a working form requires the separate [candidate setup and first-use flow](../getting-started.md).

## Start with shared meaning

A domain author names the information a team needs and defines how to capture and interpret it. For an equipment inspection, that might mean giving “guard present,” “guard absent” and “not observed” distinct meanings so staff do not have to guess what a blank cell means.

| Term | Meaning in this lesson |
|---|---|
| Definition | Reusable rules and meaning, such as the fields of an inspection record. |
| Record | One instance of captured information under a definition. |
| Form | The means of entering information; its definition specifies the capture behavior. |
| View | A presentation of records for a task, such as an attention list. |
| Pack | A versioned package of domain definitions. |
| Library | Reusable implementation that interprets or renders those definitions. |
| Provenance | Information about where something came from, who or what produced it, and which version governed it. |

A form, record and view have different jobs. A view can select records for attention without creating a new observation. A library can render a form without owning the business meaning of its fields. Installing a library does not establish that a domain pack is active.

## Follow one inspection

A synthetic pack, `facility-safety@2.4.0`, groups a record definition, a form definition and a review-view definition. The record definition requires an asset identifier, stated inspection time, inspector identifier and guard status. The form also allows a photo reference and note.

One record names `pump-17` and `operator-8`, states an inspection time, sets guard status to `unknown`, and contains the note “access panel was locked.” No photo reference is supplied.

That record contains an assertion about a locked panel. Its fields alone do not prove who submitted it, when it was entered, whether the note is accurate, or whether the guard was inspected. A stated inspection time and a system-recorded submission time answer different questions. Successful permission and validation checks need their own runtime evidence.

The record does not establish whether the pump is safe to operate. That conclusion needs the organization’s applicable policy, observations and authorized decision maker. This lesson does not ask you to decide what to do with real equipment; follow your workplace procedures in real work.

## Keep responsibilities explicit

The person defining a form, the person entering observations, the reviewer, the business decision maker and the person managing access have different responsibilities. An organization assigns those responsibilities. One person may hold several, but permission to write a record does not by itself authorize the consequential business action described by that record.

A small team may assign all of them to its owner. Ownership alone does not supply runtime permission. That owner still needs to distinguish “I entered this,” “I checked this,” and “I authorized this decision.”

## Preserve meaning and uncertainty

Interpret retained records using the definition that governed their capture. A newer definition may change the meaning of a field; do not assume it applies to older evidence.

Treat `unknown`, `not observed` and `not applicable` as distinct when the definition distinguishes them. An inaccessible guard is not evidence of an absent guard. A photo reference is not proof that the photo is available, depicts the right asset or was taken at the stated time.

If the available form cannot express what you know truthfully, pause the record submission, follow workplace procedures separately and ask the assigned model owner to review the definition. Do not choose a convenient value merely to pass validation. If you discover a mistake after submission, use the environment’s documented correction path and preserve the original evidence; seek help when no such path is available. This lesson does not promise a particular correction feature.

A justified definition change establishes a new meaning and version and requires review of affected uses. Relaxing a check to admit one inconvenient entry does not retroactively validate a refused attempt or supply missing evidence.

## Understand a refusal

**Permission** asks whether the person or service making a request may perform the operation in its scope. **Validation** asks whether the submitted values satisfy the governing checks. A permitted request may contain invalid values; a valid-looking body may come from a caller without permission.

The source-reviewed candidate record-write path checks permission before body validation. An authorization refusal therefore does not establish whether the submitted body would validate. Release support still requires candidate acceptance evidence.

When a refusal appears:

1. Identify whether it concerns permission or submitted values. Keep the supplied reason code.
2. For a permission refusal, confirm the intended operation, caller and scope before requesting any access change. The correct outcome may be to leave access unchanged.
3. For a validation refusal, inspect the named field and the applicable definition. A JSON Pointer such as `/guardStatus` names that field; it does not tell you the truthful replacement value.
4. Open a linked explanation only if the result supplies an audit identifier and your session can read it. Preserve “unavailable” or “incomplete” as the result; do not reconstruct a missing explanation.
5. Correct only an established error using available evidence and the documented interface. A later success does not make the earlier attempt valid.

An audit identifier links to a recorded decision. It is not an approval number for every business conclusion someone might draw from the record.

## Practice

The [paper exercise](evidence-interpretation-exercise.md) asks you to distinguish reusable definitions from entries, separate recorded claims from verified observations, and choose checks before changing data or permissions. It requires no login. The [facilitator guide](facilitator-guide.md) contains answers and demonstration boundaries.
