# Workshop catalogue view host

T-426 M6 slices 1–2 keep one host in each lane. `SeededListPage` accepts an exact
`viewId` / `ViewId` and fetches that `ViewDefinition` through the ordinary catalogue.
The existing `platform.list.{itemId}` default remains for M4 callers that omit it.

An explicit address such as `?item=forms&surface=health` or
`?item=forms&surface=browse` restores `platform.health.forms` or
`platform.browse.forms` after the active pack declares the Workshop item. The existing
`selected` and `panels` address values retain their meaning. Missing definitions,
HTTP refusals, absent render plans, and unsupported plans never fall back to a list
definition or an administration page. Both lanes pass declared fields to the shared
view runtime in their original order.

The producer does not yet publish a Health/Browse selector binding in
`platform.workshop`. This slice adds no navigation schema or selector declarations.
These explicit addresses exercise the generic host boundary; they do not claim that
the released pack exposes a Health/Browse navigation flow. Producer declaration
wiring remains a separate release step.

The host retains the catalogue list envelope, including `kindsUnavailable`. The
current producer serializes kind enums as numbers; tests also cover named values.
For the host's single-kind request, a nonempty unavailable list renders an explicit
unavailable state with no grid or actions. An available empty list still renders its
declared view and actions. HTTP denial and inert plans expose no actions or restored
selection.

Each row has an immutable `{entry.id}@{entry.version}` selection identity. Its
`catalogue` value retains the typed entry envelope, including kind, provenance,
sealed state, update time, definition hash, and body; `body` remains a separate value.
Authoritative metadata overrides colliding body fields in the display values.

The focused wire fixtures are synthetic contract examples, based on
`apps/local-node-host/Health/Catalogue.cs` and its route tests in API commit
`bd5c4ef6c1254035e13fb2918c10c223b3c41bb3`. They are not exported seed or activation
evidence. Both host suites exercise numeric unavailability, immutable identity and
provenance, ordered fields, exact surface requests, refusal, inert plans, and late
responses. Shell tests restore both surface families through the same URL shape.
