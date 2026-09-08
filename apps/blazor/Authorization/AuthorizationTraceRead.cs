namespace Harborline.App.Blazor.ReferenceHost.Authorization;

// Mirrors GET /api/local-node/authorization/traces/{auditId}. The host authorizes the read;
// an audit entry id is never a capability definition id or a fresh decision.
public sealed record AuthorizationTraceStep(int Ordinal, string Stage, IReadOnlyList<string> Facts);
public sealed record AuthorizationTraceRead(int Availability, int? Version, IReadOnlyList<AuthorizationTraceStep> Steps);
public sealed record RecordedDecision(Guid AuditId, Func<Guid, Task<AuthorizationTraceRead>> Read);
