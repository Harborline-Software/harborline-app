# T-433 producer/consumer contract gap

Inspected 2026-09-15 against API `1703dc3c` and App `68306d28`.
This is a proposed extension and an implementation handoff, not released behavior
or acceptance evidence.

## Released behavior

The five-definition Access pack and deterministic signed replacement exist. Their
action declarations do not yet constitute an executable shared-runtime contract.
`RenderPlanCompiler.ViewBindings` retains `parameters.actions` and emits only
`id`/`label` in `bindings.actions`. It validates operation names against a compiled
allowlist. App's existing Workshop workflows dispatch other operation names in
lane-specific switches. Extending those switches with Access names would violate
T-433's compiled-action fence.

| Declared operation | Additional released metadata | Missing execution evidence |
| --- | --- | --- |
| `access.grant.submit` | `inputForm=access.grant-a-role` | Submission binding, version pin, receipt/audit association |
| `access.grant.review` | `workflow=access.privileged-grant-review` | Subject/instance identity, workflow version, trigger binding, review route |
| `access.holder.read` | none | Ordinary record target and read binding; admin holder listing is a different act |
| `access.grant.narrow` | none | Grant identity, scope input, atomic scope-narrowing route |
| `access.grant.revoke` | none | Grant identity, reason/input binding, ordinary grant revoke route |
| `access.pack.replace` | none | Signed artifact input and admitted replacement transport/result semantics |

`AccessHoldersRead` emits separate `holders` and `rows` arrays. Only `holders`
contains `grantId`; `rows` contains principalId/role/scope/status. Joining arrays by
index or synthesizing row identity in App would create an undocumented contract.
Rows need an explicit immutable identity and the selected payload needed by
declared actions.

The frozen journey's holder has `records:read` at a record scope. Both the view's
`authorizationCapability` and the admin holders route demand `members:manage`.
The holder-read action must execute an ordinary authorized record read; an admin
holders reload cannot prove the requested allow/deny transitions.

The session `members/narrow` route narrows an admission permission bundle, not a
role grant's scope. Its `{grantId,narrowedPermissions}` payload cannot represent
the frozen `/records/m6-t433` to `/records/m6-t433/allowed` change. Reusing it just
because the operation says "narrow" would test a different journey.

Form submission creates the typed workflow at a hard-coded definition/version and
immediately dispatches approval. It returns only the form instance id (plus an
optional projection status/skips). This is not evidence of the subsequent
pack-defined review action. The manifest grants first and reviews second; it does
not require two distinct approvers. Existing `IGrantStore.RecordReviewAsync` can
record the later review but needs an authorized, workflow-bound transport.

## Smallest proposed extension

Extend the existing `ViewDefinition.Parameters.actions` and render-plan bindings.
Do not add another top-level definition kind or a client workflow interpreter.
Each action retains its stable `id`, `label`, and opaque `operation` identity, and
adds a versioned `dispatch` binding. The App branches only on generic `kind`.

```json
{
  "id": "a-pack-owned-action-id",
  "label": "A pack-owned label",
  "operation": "an-opaque-pack-owned-identity",
  "dispatch": {
    "schemaVersion": 1,
    "kind": "request",
    "descriptorId": "a-host-registered-request-descriptor",
    "bindings": {
      "id": { "source": "selection", "pointer": "/id" },
      "values": { "source": "input", "pointer": "" }
    }
  },
  "inputForm": { "formId": "a-pack-owned-form", "version": "1.0.0" },
  "authorizationCapability": "a:registered-operation",
  "result": { "refresh": ["rows"], "auditPointer": "/auditId" }
}
```

Binding values are either JSON literals or `{source,pointer}` references to
`selection` or `input`. Pointers use JSON Pointer, with no expression language,
property evaluation, ambient identity, or arbitrary URL expansion. The closed host
descriptor owns the route template, HTTP method, payload schema, content type,
session/antiforgery requirements, and actual AuthorizationGate operation. Only
the descriptor ID is pack-authored; the compiler emits the resolved transport.
Path substitutions are encoded segments. Pack visibility metadata cannot choose
or weaken enforcement. A signed pack cannot invent a credential-bearing
destination. Unknown
schema versions, kinds, methods, templates, missing inputs, or duplicate action ids
remain inert/refused in both lanes.

Reuse `FormDefinitionRef` for input references and the existing form
`fieldsMeta`/`overlay` compilation for small inline inputs. Inline inputs are
needed for scope/reason/record-target inputs if the exact five-definition set is
retained; they are not extra catalogue definitions. The compiler must admit and
emit their normalized form binding; neither lane invents their labels or fields.
A binary input binding uses the generic file control and an octet-stream request.

Reuse `WorkflowDefinition`, `subjectFormRef`, `WorkflowTriggerBindingDef`, and
`IWorkflowTriggerDispatcher` for workflow execution. The dispatch binding may
declare an exact workflow reference, a declared trigger id, and a subject source;
admission resolves all three. Server execution resolves the admitted workflow and
the subject in the request tenant and derives the actor from the authenticated
request. The UI does not reconstruct a workflow instance id or approve by name.
The existing `DeclarativeConfirmRoutes` only serves interpreter-parked CP effects;
typed Access instances cannot be sent there without a supported producer bridge.

The authorization capability describes/reaches the existing server gate; App does
not evaluate it. Responses preserve the route's status, stable code, auditId and
result verbatim. The renderer can display/link existing authorization trace
evidence. It must clear obsolete selection/results after replacement, and clear
actionable data on authorization refusal. Refresh names are a bounded enum:
`rows`, `view`, `navigation`; they never name executable code.

Row transport also belongs to the view: add an admitted `dataSource` binding with
a closed descriptor ID, row-array JSON Pointer, and row-id JSON Pointer. This reuses
the same transport/binding representation and removes App's `entityType` switch.
No row index is an identity. Do not silently join legacy holder arrays.

Replacement is one server-side command over the existing installer, or an
existing server workflow whose stages use the installer. Do not add a generic
multi-step transaction language in the browser. Existing install and activate
routes are separately callable and do not provide one request's atomic replacement
receipt. A replacement command must prove old-active/new-absent on failure and
return the exact five-definition result. It must use the existing trust,
authorization, admission, and projection pipeline.

## Mapping to the released six actions

| Action | Generic input/binding | Ordinary server operation required |
| --- | --- | --- |
| grant | Exact input FormDefinitionRef; JSON body from input | Existing forms submit, plus response audit/workflow/grant associations |
| review | Selected grant/subject id; exact workflow ref and declared trigger | Authorized workflow-bound review, using existing workflow/store primitives |
| holder read | Pack-declared record target input | Existing authorized record-read route, with actor derived from holder session |
| narrow | Selected grant id; inline scope/reason input | Atomic role-grant revoke/reissue with strict-subscope check and existing authority gate |
| revoke | Selected grant id; inline reason input | Existing authorized grant revocation primitive exposed with stable receipt |
| replace | Signed artifact file | Existing installer pipeline exposed as bounded replacement command |

Do not confuse selected grant ids, record ids, workflow instance ids, or principals.
Transport descriptors must declare which identifier each binding accepts.

## Producer files and tests

Existing API files to extend:

- `_shared/packs/access-administration/access-administration-pack.export.json`:
  data source and six admitted dispatch declarations/inputs.
- `apps/local-node-host/Health/RenderPlanCatalogue.cs`: compile/validate dispatch
  and data-source bindings instead of granting executability through operation names.
- `apps/local-node-host/Data/PackProjection/HostViewKindDescriptorRegistry.cs`:
  resolve row descriptors and action transport/input/workflow references at admission.
- `packages/foundation-view-definitions/ViewDefinition.cs`: keep the extension
  inside the existing parameters contract; share binding types if required.
- `apps/local-node-host/Health/AccessHoldersRead.cs`: immutable row identity and
  selected grant payload; preserve authoritative row scope/status.
- `apps/local-node-host/Data/Workflow/AccessGrantFormSubmissionProjection.cs` and
  `NodeWorkflowInstantiationService.cs`: eliminate hard-coded workflow version
  selection; expose actual submission/workflow/grant associations.
- `apps/local-node-host/Health/FormsRoutes.cs`: surface authoritative receipt
  correlations without changing the meaning of projection pending/skipped.
- `packages/blocks-access-grant/IGrantStore.cs`, `GrantIssuanceHandler.cs`,
  `GrantRevocationHandler.cs`, and existing authorized store paths: reuse authority,
  review, revocation, and atomic write semantics.
- `apps/local-node-host/Health/PackInstallRoutes.cs`: ordinary bounded replacement
  transport, using the existing installer and rollback behavior.
- `tooling/conformance/access-replacement/AccessReplacementFixture.cs`: regenerate
  signed bytes and source/artifact pins after declarations change.

Required API tests extend `T433AccessGrantViewDescriptorTests`,
`T433AccessReplacementProducerTests`, `AccessGrantFormLiveTests`,
`AccessHoldersReadCompositionTests`, and `AccessAdministrationPreloadTests`.
Add composed transport tests for each real allow/deny transition, incorrect actor,
unknown action kind, missing binding, invalid route/template, stale selection,
wrong workflow version, out-of-scope read, and refusal atomicity. A synthetic
dispatcher unit test cannot substitute for those tests.

## Consumer implementation and acceptance

App changes remain in separate generic host/dispatcher files beside Workshop.
Reuse `ViewRuntime`, `SchemaForm`, and existing authorization trace components.
The Blazor `IWorkshopCatalogueClient` can carry typed request descriptors without
learning any Access names. Shell wiring should resolve a navigation item's exact
view reference instead of adding a new Access branch. `SeededListPage` and shared
detail remain T-427-owned. Preserve all T-428 absence fences.

Both lanes must consume the same admitted producer fixture, including in existing
`packNavigation.test.tsx` and `PackNavigationTests.cs`. Tests must prove ordered
actions/rows, encoded identity binding, input binding, full refusal body/audit
association, no request for unknown kinds, cancellation on view replacement, and
equivalent state reset after mutations or authority loss.

Current signed artifact SHA256:
`a5a1a6b1d8e75bc30ba902c0fb2a795e82931631a0ad644bb7018038a3bce122`.
Current initial source SHA256:
`2b40586778534a172ac5cba4cb47590f3380fb793599c7e8fe9ec4a1155745dd`.
These pins will change when the missing contract is authored. Do not claim that
the current fixture already supplies it.

Exact replacement sets from the released API manifest:

- Removed: `WorkflowDefinition/access.privileged-grant-review@1.0.1`,
  `ViewDefinition/access.holders@1.0.0`.
- Added: `WorkflowDefinition/access.scoped-grant-review@1.0.0`,
  `ViewDefinition/access.holders@1.0.1`.
- Retained: `RoleDefinition/access.form-submitter@1.0.0`,
  `FormDefinition/access.grant-a-role@1.0.1`,
  `NavWorkspaceConfig/access.navigation@1.1.0`.

The control manifest inspected during this audit still omits the view replacement
from its removed/added sets. Reconcile that record before the first acceptance
run; it cannot weaken the exact-five assertion.

## Transport audience audit

The existing local M4 shell path intentionally reaches desktop administration:
React `vite.config.ts` attaches the server-only `LOCAL_NODE_SESSION_TOKEN` to
proxied `/api/local-node` requests; Blazor `Program.cs` attaches
`LocalNode:SessionToken` in `ConfigureNodeClient`. API
`SharedHostedWebApp` Accept 1 recognizes that bearer and publishes
`BootstrapBearerRequestPrincipal` and `DesktopPlaneRequestFeature`. The holder
route's `MapDesktopPlaneOnlyGroup` therefore does not by itself prove that the
released local App cannot reach it. Do not broaden that group.

This does reveal a separate two-person acceptance constraint. Bootstrap bearer
admission precedes selected-cookie admission. A proxy that always injects the
bootstrap bearer prevents the second person's selected session from becoming the
request's authority. `NodeGatePrincipal.Resolve` uses the selected-session
principal only when that feature exists; otherwise the desktop operator supplies
the authority. Tests/acceptance must distinguish those modes explicitly.

The generic selected-session transport must send the person's session cookie and
the ordinary rotating antiforgery token, and must not inject the bootstrap bearer
on those requests. React currently proxies only `/api/local-node`, not
`/api/session`; Blazor's configured shared HttpClient is not a per-person cookie
transport. A server-wide cookie jar would also merge users and is not acceptable.
Preserve the listener's current precedence and implement a per-person transport
boundary in the consumers. Prove principal identity and response-to-audit linkage
using the actual selected-session path, including denied actions.

## Implementation checkpoint

API worktree `harborline-api/.claude/worktrees/t433-action-contract` starts from
`1703dc3c`. Local commit `5584e3a8` adds `grantId` to canonical holder rows and
their descriptor. Its focused composition test proves stable identity across
restarts and multiple grants for one principal.

The draft request admission layer uses a host-owned descriptor ID, exact input
names, typed selection/input pointers, and a bounded schema version/kind. The
first focused run passed 29 tests, including the new binding tests and existing
holder admission/read/authorization fences. This is not a full gate or a release.
Only already implemented read routes are being registered during compiler
integration; missing mutations remain unsupported until their real transports
and authority tests exist. No App route or Access-specific action switch has been
introduced.

The API compiler/admission checkpoint is now local commits `1a0d5120` and
`536d7ff6`; the focused suite passes 87 tests. Only `records.read.v1` and
`authorization.holders.read.v1` are registered, both owned by their actual routes.
Result/refresh/workflow mutation metadata remains proposed, not implemented.

The App now has a separate `/api/selected-node/...` proxy namespace for both
lanes. It forwards only `__Host-hl-selected`, never bootstrap/legacy/installation
credentials, and requires same-origin requests plus antiforgery for mutations.
Both proxy implementations reject path escapes and redirects and retain the
server's response status/body/audit headers. Blazor uses a cookie-disabled sender;
the browser, not the Blazor circuit or a shared server jar, owns the cookie.
The shared browser module serializes one-time antiforgery issuance and mutations;
denials never trigger a write retry or fallback to bootstrap authority.

Transport boundary tests pass for concurrent independent Alice/Bob requests,
bootstrap-shadowing negatives, origin/antiforgery refusal, stable denial evidence,
and invalid paths. These are not yet live-node selected-principal or clean-node
acceptance tests. Existing desktop clients and listener precedence remain intact.
The proxy is selected-session-only: login/selection must already have happened
through the node's existing session flow on the same browser host. Secure cookie
requirements are retained; deployments must use a compatible HTTPS origin.

## Scope ruling for ordinary review and revocation

The implementation will use the existing governed admin grant revocation path,
not claim that `GrantRevocationHandler` is composed in production. The frozen M6
exit requires removal of observed authority, no mutation on denial, and stable
server audit correlation. It does not require forward-key rotation or a typed
revocation workflow. The same distinction applies to a governed review stamp: it
is not a workflow transition unless that transition is actually executed.

The production typed revocation context, key-rotation coordinator, and the
documented `Foundation.SickBay.IKeyRotationScheduler` do not exist in the inspected
API tree. Workflow and grants also use different databases; copying issuance's
`CommitsIndependently` effect cannot establish atomic revocation. Do not invent a
scheduler or claim cross-database atomicity in this change. Preserve these as a
follow-up alongside existing deferred rotation work (ADR 0009 / T-018); ordinary
grant revocation must remain explicitly labelled as such. Scope narrowing still
requires a real atomic revoke/reissue and the exact frozen scope transition.

## Pre-execution scope fixture correction

The original candidate used slash-bearing record identities. The production
authorization gate intentionally rejects those identities; this change does not
extend that gate or add an alternative query-bound record-read route. Before
acceptance execution, the harness owner corrected the candidate to ordinary IDs
`m6-t433-allowed-record-1` and `m6-t433-outside-record-1`, with initial grant scope
`/records` narrowed to `/records/m6-t433-allowed-record-1`. The existing entity-read
route can address both IDs without encoded path separators.

The corrected contract must prove both records readable before narrowing, only
the allowed record readable afterward, and neither readable after ordinary grant
revocation. The store tests assert containment against targets produced by the
unchanged canonical authorization request builder. This records a pre-run fixture
correction, not an acceptance result. The exact-five replacement definition set
is unchanged.
