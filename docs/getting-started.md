# First use of the M4 Workshop candidate

> **Candidate preview:** This lesson has not completed release validation. Check [documentation readiness](release-readiness.md) before running it; the steps below describe expected behavior.

The candidate exercise is designed to show one small, safe path: establish the first administrator on a new local node, open Workshop, validate and transport a synthetic Notes package, create one Note record, read it back and inspect an authorization or validation explanation.

Use only a disposable node and synthetic values while the candidate is under test. Do not point this guide at a customer environment or a directory containing records you need to keep.

## Before you begin

Have an operator prepare a disposable, connected node and the selected App host using the [development setup](development-setup.md). You need the App address, an established administrator session, the accepted synthetic Notes package and the recorded paths for cleanup.

**Start only when those prerequisites are available.** A fixture session cannot complete this exercise. Missing startup instructions and candidate evidence are tracked in [documentation readiness](release-readiness.md).

## The candidate workflow

The accepted flow is expected to expose these actions from definitions supplied by the active Platform package: validate the package document, export its signed artifact, verify it, install it, activate it, create a record and read the record with its explanation. The action labels and order must come from the accepted candidate UI; this guide does not substitute a second command-line acceptance script.

### 1. Open Workshop

Open the App address for the selected lane and choose **Workshop** in the workspace navigation. The candidate Platform package is expected to supply the workspace and its definition lists, including **Record types** and **Forms**.

Confirm that the list content came from the connected node. A fixture banner, configuration error or list request error means the exercise has not started successfully.

React and Blazor are expected to expose the same workspace, rows and workflow actions through their respective projections. Genuine presentation differences may follow browser and framework conventions, but the accepted action sequence and outcomes must remain equivalent.

### 2. Use the synthetic sample

Use only the Notes sample named by the accepted release notes. The current source fixture is designed around package key `harborline.notes-lite`, record type `notes.entry`, form `notes.capture` and a required text field; those identifiers remain candidate data until the M4 runbook seals the fixture.

Enter an obviously synthetic value such as `M4 onboarding sample — delete me`. Do not include a person's name, email address, customer identifier, credential, access token or real operational observation.

### 3. Validate before export

Choose the candidate's **Validate** action before exporting. A successful result should identify the package document as valid; an invalid package definition should display a stable reason and JSON Pointer locations where the response supplies them.

### 4. Export, verify, install and activate

After validation, follow the declared actions in order: **Export**, **Verify**, **Install** and **Activate**. Keep the exported `.pack` artifact inside the disposable exercise directory and do not substitute an unrelated or customer-authored package.

Verification must report the node's verdict. Installation and activation are distinct operations, and activation is successful only when the candidate reports activation without projection or Platform refusals.

The API's maintained [operator CLI guide](https://github.com/Harborline-Software/harborline-api/blob/main/apps/node-operator-cli/README.md) documents the corresponding headless verbs for operators. Those verbs are useful for diagnosis but do not replace the person-driven App acceptance required for this lesson.

### 5. Create the Note record

After activation, choose the action that creates a record from the active package's bound form. Enter the synthetic sample value and submit it once.

Expected result: the runtime accepts the record and returns an audit identifier for the decision. If it refuses the write, inspect the explanation before continuing.

To find a record-validation explanation, clear the Note form's required text field in the disposable exercise and submit once. Record the refusal reason and JSON Pointer without copying the submitted body, restore the synthetic value, and submit the valid record.

### 6. Read the record and its explanation

Choose the read action for the record you just created. Confirm that the returned record identifier and synthetic value match the write rather than assuming that a success message proves persistence.

Open the authorization explanation addressed by the write's audit identifier. The accepted candidate should show the decision that allowed the write; if the response has no audit identifier or the trace cannot be read in the same session, stop and report the sanitized error through [support](../SUPPORT.md).

An authorization explanation answers why the acting principal could perform the operation. A validation explanation answers why submitted values met or failed the form's checks. Neither explanation proves that the real-world claim in a record is true.

## Clean up the disposable exercise

Stop both development hosts, clear the process environment values that held the username, password hash, session token and node origin, and remove only the disposable data directory and exported sample artifact whose paths you recorded before starting.

Do not delete a default or shared node directory based on a guessed path. The accepted runbook must name its temporary directory and cleanup command so the target can be checked before removal.

The candidate does not yet document an in-product record deletion path for this exercise. Treat creation as persistent within the disposable node and discard the entire verified disposable environment after the run.

## If you cannot complete a step

Stop at the first unexpected result. Note the step, App experience (React or Blazor), candidate revision and sanitized error. Use [support](../SUPPORT.md) for confusing instructions or defects; keep credentials and record bodies out of reports.

For a facilitated documentation walkthrough, use the [observation checklist](release-readiness.md#walkthrough-observations). Continue learning with the [evidence interpretation exercise](training/evidence-interpretation-exercise.md).
