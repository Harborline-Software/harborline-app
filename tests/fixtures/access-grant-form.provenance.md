# Access granting fixture

Source: harborline-api `_shared/packs/access-administration/access-administration-pack.export.json`, package 1.1.0, form `access.grant-a-role` 1.0.0.
API source HEAD observed: `6ee7c98a43bad8b40623325543e8b1b10721f2ce`.
Source SHA256: `7647ffc5f45b534c53e8661340d4dc723bc41f4d0f0156062ddc7422947ab8f4`.
Fixture LF SHA256: `07d35b33423e29455a1b3f67a69b419bbd97f11ecfb1418ca8da809729d04ca9`.

This is a constructed FormView fixture, not a live capture. It transcribes the pack section's field order and labels into the DTO declared in `apps/local-node-host/Health/FormsRoutes.cs:397`. All fields are readable in this replay; values are initially null. The pack's section order places residency before effective dates, despite the different order of keys in overlay.fields. The host GET returned 404 during the live proof; no successful live grant is represented by this fixture.

The current rendered-form DTO omits fieldsMeta options and required flags. The app does not duplicate the pack's reason/residency options; this slice uses text inputs until the ordinary runtime contract exposes the metadata. No existing form-fill renderer was present in either app lane at the base commit.

`access-grant-body.json` is the exact 154-byte candidate used by both component tests, without a trailing newline. The parity runner captures the actual HTTP submission body from each lane and compares UTF-8 bytes. The replay holders response is test transport behavior, not evidence that the API issues an AccessGrant from this form. Live tests separately require the real form read, submission, and a holders row for the submitted party.
