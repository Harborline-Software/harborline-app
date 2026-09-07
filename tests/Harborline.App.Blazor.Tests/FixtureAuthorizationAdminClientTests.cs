using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;

namespace Harborline.App.Blazor.Tests;

public sealed class FixtureAuthorizationAdminClientTests
{
    [Fact]
    public async Task Empty_binding_survives_definition_and_binding_reload()
    {
        var client = new FixtureAuthorizationAdminClient();
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        await client.NarrowCapabilityBindingAsync(id, [], "clear");

        var definition = Assert.Single(await client.ListCapabilityDefinitionsAsync(), row => row.DefinitionId == id);
        Assert.Empty(definition.Binding.EffectiveRoles);
        Assert.Equal(BindingWarningCode.EmptyBinding, definition.Binding.Warning);
        var binding = await client.GetEffectiveBindingAsync(id);
        Assert.Empty(binding.EffectiveRoles);
        Assert.Equal(BindingWarningCode.EmptyBinding, binding.Warning);
    }

    [Fact]
    public async Task Strict_subset_preserves_only_selected_qualified_references()
    {
        var client = new FixtureAuthorizationAdminClient();
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        RoleReference[] selected = [new("tax.roles", "author")];

        var result = await client.NarrowCapabilityBindingAsync(id, selected, "least privilege");

        Assert.Equal(selected, result.EffectiveRoles);
        Assert.Equal(selected, (await client.GetEffectiveBindingAsync(id)).EffectiveRoles);
    }

    [Fact]
    public async Task Removed_role_cannot_be_readded()
    {
        var client = new FixtureAuthorizationAdminClient();
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        RoleReference author = new("tax.roles", "author");
        await client.NarrowCapabilityBindingAsync(id, [author], "remove administrator");

        var error = await Assert.ThrowsAsync<AuthorizationAdminException>(() => client.NarrowCapabilityBindingAsync(
            id,
            [author, new("sys.platform-roles", "administrator")],
            "widen"));

        Assert.Equal(409, error.Status);
        Assert.Equal("authorization.binding_widening_refused", error.Code);
    }
}
