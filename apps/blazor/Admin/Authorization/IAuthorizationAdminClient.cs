namespace Harborline.App.Blazor.ReferenceHost.Admin.Authorization;

public interface IAuthorizationAdminClient
{
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
