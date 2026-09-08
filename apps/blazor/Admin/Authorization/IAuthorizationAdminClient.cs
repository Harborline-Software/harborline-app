using Harborline.App.Blazor.ReferenceHost.Authorization;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Authorization;

public interface IAuthorizationAdminClient
{
    Task<AuthorizationTraceRead> ReadTraceAsync(Guid auditId);
    Task<AccessHoldersResponse> ListHoldersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleDefinition>> ListRoleVocabularyAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthorizationCapabilityDefinition>> ListCapabilityDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<AuthorizationBinding> GetEffectiveBindingAsync(Guid definitionId, CancellationToken cancellationToken = default);
    Task<NarrowAuthorizationBindingResult> NarrowCapabilityBindingAsync(
        Guid definitionId,
        IReadOnlyList<RoleReference> selectedRoles,
        string reason,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StandingDefinition>> ListStandingCatalogueAsync(CancellationToken cancellationToken = default);
}

public sealed record AccessHoldersResponse(IReadOnlyList<AccessHolder> Holders);
public sealed record AccessHolder(string PartyId, string Source, string GrantId, RoleReference? Role,
    string Granter, string Scope, string EffectiveFrom, string? EffectiveTo, string? AttributionFailure);
