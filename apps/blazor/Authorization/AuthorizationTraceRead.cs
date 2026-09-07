namespace Harborline.App.Blazor.ReferenceHost.Authorization;

// Mirrors the routeless kernel-audit reader. The host owns the authorized read;
// an audit entry id is never a capability definition id or a fresh decision.
public sealed record AuthorizationTraceStep(int Ordinal, string Stage, IReadOnlyList<string> Facts);
public sealed record AuthorizationTraceRead(int Availability, int? Version, IReadOnlyList<AuthorizationTraceStep> Steps);
public sealed record RecordedDecision(Guid AuditId, Func<Guid, Task<AuthorizationTraceRead>> Read);
