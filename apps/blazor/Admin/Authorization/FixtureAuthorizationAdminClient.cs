namespace Harborline.App.Blazor.ReferenceHost.Admin.Authorization;

public sealed class FixtureAuthorizationAdminClient : IAuthorizationAdminClient
{
    public static readonly IReadOnlyList<RoleDefinition> RoleDefinitions =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), new("sys.platform-roles", "administrator"), "Administrator", new("Platform", "harborline-platform"), true),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), new("sys.platform-roles", "auditor"), "Auditor", new("Platform", "harborline-platform"), true),
        new(Guid.Parse("44444444-4444-4444-4444-444444444444"), new("sys.platform-roles", "node-operator"), "Node operator", new("Platform", "harborline-platform"), true),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), new("tax.roles", "author"), "Tax author", new("Package", "harborline.tax"), false),
    ];

    private readonly List<AuthorizationCapabilityDefinition> definitions =
    [
        new(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "harborline.tax", 3,
            new("tax.return.write", "Tenant", "*"),
            [new("sys.platform-roles", "administrator"), new("tax.roles", "author")],
            new(2, [new("sys.platform-roles", "administrator"), new("tax.roles", "author")], null)),
        new(
            Guid.Parse("12121212-1212-1212-1212-121212121212"), "harborline.access-grant", 1,
            new("audit:read", "Tenant", "*"),
            [new("sys.platform-roles", "auditor"), new("sys.platform-roles", "node-operator")],
            new(1, [new("sys.platform-roles", "auditor"), new("sys.platform-roles", "node-operator")], null)),
        new(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "harborline.audit", 1,
            new("audit.export", "Tenant", "*"),
            [new("sys.platform-roles", "auditor")],
            new(4, [], BindingWarningCode.EmptyBinding)),
    ];

    public static readonly IReadOnlyList<StandingDefinition> StandingDefinitions =
    [
        new(
            "tax-return-standing", "1.0.0", "Filed", "tax.return",
            [
                new("returnId", ["tax.return", "tax.return.amendment"]),
                new("legacyReference", []),
            ]),
    ];

    public Task<IReadOnlyList<RoleDefinition>> ListRoleVocabularyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(RoleDefinitions);

    public Task<IReadOnlyList<AuthorizationCapabilityDefinition>> ListCapabilityDefinitionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AuthorizationCapabilityDefinition>>(definitions.ToArray());

    public Task<AuthorizationBinding> GetEffectiveBindingAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        var definition = definitions.SingleOrDefault(row => row.DefinitionId == definitionId)
            ?? throw new AuthorizationAdminException(404, "authorization.definition_not_found");
        return Task.FromResult(definition.Binding);
    }

    public Task<NarrowAuthorizationBindingResult> NarrowCapabilityBindingAsync(
        Guid definitionId,
        IReadOnlyList<RoleReference> selectedRoles,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var index = definitions.FindIndex(row => row.DefinitionId == definitionId);
        if (index < 0) throw new AuthorizationAdminException(404, "authorization.definition_not_found");
        var definition = definitions[index];
        if (selectedRoles.Except(definition.Binding.EffectiveRoles).Any())
            throw new AuthorizationAdminException(409, "authorization.binding_widening_refused");
        var binding = new AuthorizationBinding(
            definition.Binding.Revision + 1,
            selectedRoles.ToArray(),
            selectedRoles.Count == 0 ? BindingWarningCode.EmptyBinding : null);
        definitions[index] = definition with { Binding = binding };
        return Task.FromResult(new NarrowAuthorizationBindingResult(
            definitionId,
            binding.Revision,
            binding.EffectiveRoles,
            binding.Warning,
            "fixture-admin",
            DateTimeOffset.Parse("2026-09-02T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            reason));
    }

    public Task<IReadOnlyList<StandingDefinition>> ListStandingCatalogueAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(StandingDefinitions);
}
