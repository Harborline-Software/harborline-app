using System.Text.Json.Serialization;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Authorization;

public sealed record RoleReference(string Vocabulary, string Name);
public sealed record RoleOwner(string Kind, string OwnerId);
public sealed record RoleDefinition(Guid RoleDefinitionId, RoleReference Role, string DisplayName, RoleOwner Owner, bool IsSealed);
public sealed record PermissionAtom(string Operation, string ScopeType, string ScopeValue);

[JsonConverter(typeof(JsonStringEnumConverter<BindingWarningCode>))]
public enum BindingWarningCode { EmptyBinding }

public sealed record AuthorizationBinding(long Revision, IReadOnlyList<RoleReference> EffectiveRoles, BindingWarningCode? Warning);
public sealed record AuthorizationCapabilityDefinition(
    Guid DefinitionId,
    string PublisherPackageId,
    long DefinitionRevision,
    PermissionAtom Atom,
    IReadOnlyList<RoleReference> OfferedRoles,
    AuthorizationBinding Binding);
public sealed record NarrowAuthorizationBindingRequest(IReadOnlyList<RoleReference> SelectedRoles, string Reason);
public sealed record NarrowAuthorizationBindingResult(
    Guid DefinitionId,
    long Revision,
    IReadOnlyList<RoleReference> EffectiveRoles,
    BindingWarningCode? Warning,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    string Reason);
public sealed record StandingFieldRecordTypes(string Field, IReadOnlyList<string> CarryingRecordTypes);
public sealed record StandingDefinition(
    string RuleId,
    string RuleVersion,
    string Standing,
    string DeclaredRecordType,
    IReadOnlyList<StandingFieldRecordTypes> Fields);

// The API includes auditId only when a refusal row was actually recorded.
public sealed class AuthorizationAdminException(int status, string code, Guid? auditId = null) : Exception(MessageFor(code))
{
    public Guid? AuditId { get; } = auditId;
    public int Status { get; } = status;
    public string Code { get; } = code;

    private static string MessageFor(string code) => code switch
    {
        "authorization.binding_invalid" => "The selected role binding is invalid. Review the selection and try again.",
        "authorization.binding_stale" => "This binding changed while you were editing it. Reload and try again.",
        "authorization.binding_widening_refused" => "A removed role cannot be restored through this definition revision.",
        "authorization.definition_not_found" => "This authorization definition is no longer available. Reload the catalogue.",
        "authorization.idempotency_key_required" => "The binding request could not be safely identified. Try again.",
        "authorization.idempotency_key_reused" => "This binding request conflicts with an earlier request. Try again.",
        "authorization.permission_required" => "You do not have permission to administer authorization settings.",
        _ => "The authorization service could not complete the request. Try again.",
    };
}
