using Bunit;
using Harborline.App.Blazor.ReferenceHost.Admin.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Harborline.UIAdapters.Blazor;

namespace Harborline.App.Blazor.Tests;

public sealed class AuthorizationAdminPageTests : BunitContext
{
    [Fact]
    public async Task Every_seeded_operation_appears_once_with_an_open_editor_and_ordered_binding_facts()
    {
        var client = new FixtureAuthorizationAdminClient();
        var expected = await client.ListCapabilityDefinitionsAsync();
        var cut = RenderPage(client);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(expected.Count, cut.FindAll("article[aria-labelledby^='capability-']").Count);
            foreach (var definition in expected)
            {
                var title = $"{definition.Atom.Operation} · {definition.Atom.ScopeType}:{definition.Atom.ScopeValue}";
                var heading = Assert.Single(cut.FindAll("h3"), candidate => candidate.TextContent.Trim() == title);
                Assert.NotNull(heading.Closest("article")!.QuerySelector("button"));
            }

            Assert.Equal(
                ["Operation", "Scoped atom", "Publisher-offered roles", "Effective roles", "Source"],
                FindEditor(cut).QuerySelectorAll("dt").Select(term => term.TextContent.Trim()));
        });
    }

    [Fact]
    public void Clear_all_requires_confirmation_posts_empty_and_renders_warning_alert()
    {
        var client = new RecordingFixtureClient();
        var cut = RenderPage(client);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#capability-bindings-heading")));

        var editor = FindEditor(cut);
        FindButton(editor, "Clear all roles…").Click();

        var dialog = cut.Find("[role='dialog']");
        Assert.Contains(
            "Removed roles, including the last role, cannot be restored through this definition revision.",
            dialog.TextContent,
            StringComparison.Ordinal);
        Assert.Null(client.LastSelectedRoles);
        FindButton(dialog, "Clear all roles").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(client.LastSelectedRoles!);
            Assert.Contains("Binding saved with warning", FindEditor(cut).QuerySelector("[role='alert']")!.TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Removed_role_is_disabled_history_and_standings_are_not_role_options()
    {
        var client = new FixtureAuthorizationAdminClient();
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await client.NarrowCapabilityBindingAsync(id, [new("tax.roles", "author")], "remove administrator");
        var cut = RenderPage(client);

        cut.WaitForAssertion(() =>
        {
            var editor = FindEditor(cut);
            var administrator = Assert.Single(editor.QuerySelectorAll("input[type='checkbox']"), input => input.ParentElement!.TextContent.Contains("Administrator", StringComparison.Ordinal));
            Assert.True(administrator.HasAttribute("disabled"));
            Assert.Contains("Removed; history only", editor.TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain(editor.QuerySelectorAll("input[type='checkbox']"), input => input.ParentElement!.TextContent.Contains("Filed", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Remove_one_role_renders_the_effective_result()
    {
        var client = new RecordingFixtureClient();
        var cut = RenderPage(client);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#capability-bindings-heading")));
        var editor = FindEditor(cut);
        var administrator = Assert.Single(editor.QuerySelectorAll("input[type='checkbox']"), input => input.ParentElement!.TextContent.Contains("Administrator", StringComparison.Ordinal));

        administrator.Change(false);
        FindButton(editor, "Save narrower binding…").Click();
        FindButton(cut.Find("[role='dialog']"), "Save narrower binding").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal([new RoleReference("tax.roles", "author")], client.LastSelectedRoles);
            Assert.True(Assert.Single(FindEditor(cut).QuerySelectorAll("input[type='checkbox']"), input => input.ParentElement!.TextContent.Contains("Administrator", StringComparison.Ordinal)).HasAttribute("disabled"));
            var effective = DefinitionValue(FindEditor(cut), "Effective roles");
            Assert.Contains("Tax author", effective, StringComparison.Ordinal);
            Assert.DoesNotContain("Administrator", effective, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Widening_refusal_shows_lane_owned_copy()
    {
        var cut = RenderPage(new RefusingClient());
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#capability-bindings-heading")));
        var editor = FindEditor(cut);
        Assert.Single(editor.QuerySelectorAll("input[type='checkbox']"), input => input.ParentElement!.TextContent.Contains("Administrator", StringComparison.Ordinal)).Change(false);
        FindButton(editor, "Save narrower binding…").Click();
        FindButton(cut.Find("[role='dialog']"), "Save narrower binding").Click();

        cut.WaitForAssertion(() => Assert.Contains(
            "A removed role cannot be restored through this definition revision.",
            FindEditor(cut).QuerySelector("[role='alert']")!.TextContent,
            StringComparison.Ordinal));
    }

    [Fact]
    public void Standing_fields_list_every_record_type_and_warn_when_none_are_declared()
    {
        var cut = RenderPage(new FixtureAuthorizationAdminClient());

        cut.WaitForAssertion(() =>
        {
            var carried = cut.Find("section[aria-label='Standing field returnId']");
            Assert.Contains("tax.return", carried.TextContent, StringComparison.Ordinal);
            Assert.Contains("tax.return.amendment", carried.TextContent, StringComparison.Ordinal);
            var empty = cut.Find("section[aria-label='Standing field legacyReference']");
            Assert.Equal("alert", empty.QuerySelector("p")!.GetAttribute("role"));
            Assert.Contains("No carrying record types", empty.TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Loading_requests_are_cancelled_when_the_page_is_disposed()
    {
        var client = new PendingClient();
        var cut = RenderPage(client);

        Assert.Contains("Loading authorization settings", cut.Markup, StringComparison.Ordinal);
        cut.Instance.Dispose();

        Assert.Equal(3, client.Tokens.Count);
        Assert.All(client.Tokens, token => Assert.True(token.IsCancellationRequested));
    }

    [Fact]
    public void Load_error_offers_retry_then_renders_empty_catalogue_in_width_safe_layout()
    {
        var client = new RetryClient();
        var cut = RenderPage(client);
        cut.WaitForAssertion(() => Assert.Contains("could not complete", cut.Find("[role='alert']").TextContent, StringComparison.Ordinal));

        FindButton(cut, "Retry").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No authorization capability definitions are available.", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("min-width:0", cut.Find("div[style]").GetAttribute("style"), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, client.RoleAttempts);
        });
    }

    [Fact]
    public void Definition_installed_between_catalogue_loads_appears_without_a_code_change()
    {
        var client = new GrowingCatalogueClient();
        var first = RenderPage(client);
        first.WaitForAssertion(() => Assert.Single(first.FindAll("article[aria-labelledby^='capability-']")));
        Assert.DoesNotContain("installed.package.read", first.Markup, StringComparison.Ordinal);
        first.Dispose();

        var second = Render<AuthorizationAdminPage>();

        second.WaitForAssertion(() =>
        {
            Assert.Equal(2, second.FindAll("article[aria-labelledby^='capability-']").Count);
            Assert.Contains("installed.package.read", second.Markup, StringComparison.Ordinal);
            Assert.Equal(2, client.ListCalls);
        });
    }

    private IRenderedComponent<AuthorizationAdminPage> RenderPage(IAuthorizationAdminClient client)
    {
        Services.AddSingleton(client);
        Services.AddHarborlineUiAdapters();
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<AuthorizationAdminPage>();
    }

    private static AngleSharp.Dom.IElement FindEditor(IRenderedComponent<AuthorizationAdminPage> cut) =>
        cut.Find("article[aria-labelledby='capability-aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa']");

    private static AngleSharp.Dom.IElement FindButton(AngleSharp.Dom.IParentNode root, string text) =>
        Assert.Single(root.QuerySelectorAll("button"), button => button.TextContent.Trim() == text);

    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<AuthorizationAdminPage> root, string text) =>
        Assert.Single(root.FindAll("button"), button => button.TextContent.Trim() == text);

    private static string DefinitionValue(AngleSharp.Dom.IElement editor, string term) =>
        Assert.Single(editor.QuerySelectorAll("dt"), candidate => candidate.TextContent.Trim() == term).NextElementSibling!.TextContent.Trim();

    private sealed class RecordingFixtureClient : IAuthorizationAdminClient
    {
        public Task<Harborline.App.Blazor.ReferenceHost.Authorization.AuthorizationTraceRead> ReadTraceAsync(Guid auditId) => throw new NotSupportedException();
        public Task<AccessHoldersResponse> ListHoldersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        private readonly FixtureAuthorizationAdminClient inner = new();
        public IReadOnlyList<RoleReference>? LastSelectedRoles { get; private set; }
        public Task<IReadOnlyList<RoleDefinition>> ListRoleVocabularyAsync(CancellationToken cancellationToken = default) => inner.ListRoleVocabularyAsync(cancellationToken);
        public Task<IReadOnlyList<AuthorizationCapabilityDefinition>> ListCapabilityDefinitionsAsync(CancellationToken cancellationToken = default) => inner.ListCapabilityDefinitionsAsync(cancellationToken);
        public Task<AuthorizationBinding> GetEffectiveBindingAsync(Guid definitionId, CancellationToken cancellationToken = default) => inner.GetEffectiveBindingAsync(definitionId, cancellationToken);
        public Task<IReadOnlyList<StandingDefinition>> ListStandingCatalogueAsync(CancellationToken cancellationToken = default) => inner.ListStandingCatalogueAsync(cancellationToken);
        public Task<NarrowAuthorizationBindingResult> NarrowCapabilityBindingAsync(Guid definitionId, IReadOnlyList<RoleReference> selectedRoles, string reason, CancellationToken cancellationToken = default)
        {
            LastSelectedRoles = selectedRoles;
            return inner.NarrowCapabilityBindingAsync(definitionId, selectedRoles, reason, cancellationToken);
        }
    }

    private sealed class PendingClient : IAuthorizationAdminClient
    {
        public Task<Harborline.App.Blazor.ReferenceHost.Authorization.AuthorizationTraceRead> ReadTraceAsync(Guid auditId) => throw new NotSupportedException();
        public Task<AccessHoldersResponse> ListHoldersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public List<CancellationToken> Tokens { get; } = [];
        private Task<T> Pending<T>(CancellationToken token) { Tokens.Add(token); return new TaskCompletionSource<T>().Task; }
        public Task<IReadOnlyList<RoleDefinition>> ListRoleVocabularyAsync(CancellationToken cancellationToken = default) => Pending<IReadOnlyList<RoleDefinition>>(cancellationToken);
        public Task<IReadOnlyList<AuthorizationCapabilityDefinition>> ListCapabilityDefinitionsAsync(CancellationToken cancellationToken = default) => Pending<IReadOnlyList<AuthorizationCapabilityDefinition>>(cancellationToken);
        public Task<AuthorizationBinding> GetEffectiveBindingAsync(Guid definitionId, CancellationToken cancellationToken = default) => Pending<AuthorizationBinding>(cancellationToken);
        public Task<NarrowAuthorizationBindingResult> NarrowCapabilityBindingAsync(Guid definitionId, IReadOnlyList<RoleReference> selectedRoles, string reason, CancellationToken cancellationToken = default) => Pending<NarrowAuthorizationBindingResult>(cancellationToken);
        public Task<IReadOnlyList<StandingDefinition>> ListStandingCatalogueAsync(CancellationToken cancellationToken = default) => Pending<IReadOnlyList<StandingDefinition>>(cancellationToken);
    }

    private sealed class RefusingClient : IAuthorizationAdminClient
    {
        public Task<Harborline.App.Blazor.ReferenceHost.Authorization.AuthorizationTraceRead> ReadTraceAsync(Guid auditId) => throw new NotSupportedException();
        public Task<AccessHoldersResponse> ListHoldersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        private readonly FixtureAuthorizationAdminClient inner = new();
        public Task<IReadOnlyList<RoleDefinition>> ListRoleVocabularyAsync(CancellationToken cancellationToken = default) => inner.ListRoleVocabularyAsync(cancellationToken);
        public Task<IReadOnlyList<AuthorizationCapabilityDefinition>> ListCapabilityDefinitionsAsync(CancellationToken cancellationToken = default) => inner.ListCapabilityDefinitionsAsync(cancellationToken);
        public Task<AuthorizationBinding> GetEffectiveBindingAsync(Guid definitionId, CancellationToken cancellationToken = default) => inner.GetEffectiveBindingAsync(definitionId, cancellationToken);
        public Task<IReadOnlyList<StandingDefinition>> ListStandingCatalogueAsync(CancellationToken cancellationToken = default) => inner.ListStandingCatalogueAsync(cancellationToken);
        public Task<NarrowAuthorizationBindingResult> NarrowCapabilityBindingAsync(Guid definitionId, IReadOnlyList<RoleReference> selectedRoles, string reason, CancellationToken cancellationToken = default) =>
            Task.FromException<NarrowAuthorizationBindingResult>(new AuthorizationAdminException(409, "authorization.binding_widening_refused"));
    }

    private sealed class GrowingCatalogueClient : IAuthorizationAdminClient
    {
        public Task<Harborline.App.Blazor.ReferenceHost.Authorization.AuthorizationTraceRead> ReadTraceAsync(Guid auditId) => throw new NotSupportedException();
        public Task<AccessHoldersResponse> ListHoldersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        private readonly FixtureAuthorizationAdminClient inner = new();
        private AuthorizationCapabilityDefinition? installed;
        public int ListCalls { get; private set; }
        public Task<IReadOnlyList<RoleDefinition>> ListRoleVocabularyAsync(CancellationToken cancellationToken = default) => inner.ListRoleVocabularyAsync(cancellationToken);
        public async Task<IReadOnlyList<AuthorizationCapabilityDefinition>> ListCapabilityDefinitionsAsync(CancellationToken cancellationToken = default)
        {
            var definitions = await inner.ListCapabilityDefinitionsAsync(cancellationToken);
            ListCalls++;
            if (ListCalls == 1) return [definitions[0]];
            installed = definitions[0] with
            {
                DefinitionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Atom = definitions[0].Atom with { Operation = "installed.package.read" },
            };
            return [definitions[0], installed];
        }
        public Task<AuthorizationBinding> GetEffectiveBindingAsync(Guid definitionId, CancellationToken cancellationToken = default) =>
            installed?.DefinitionId == definitionId ? Task.FromResult(installed.Binding) : inner.GetEffectiveBindingAsync(definitionId, cancellationToken);
        public Task<IReadOnlyList<StandingDefinition>> ListStandingCatalogueAsync(CancellationToken cancellationToken = default) => inner.ListStandingCatalogueAsync(cancellationToken);
        public Task<NarrowAuthorizationBindingResult> NarrowCapabilityBindingAsync(Guid definitionId, IReadOnlyList<RoleReference> selectedRoles, string reason, CancellationToken cancellationToken = default) => inner.NarrowCapabilityBindingAsync(definitionId, selectedRoles, reason, cancellationToken);
    }

    private sealed class RetryClient : IAuthorizationAdminClient
    {
        public Task<Harborline.App.Blazor.ReferenceHost.Authorization.AuthorizationTraceRead> ReadTraceAsync(Guid auditId) => throw new NotSupportedException();
        public Task<AccessHoldersResponse> ListHoldersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public int RoleAttempts { get; private set; }
        public Task<IReadOnlyList<RoleDefinition>> ListRoleVocabularyAsync(CancellationToken cancellationToken = default)
        {
            RoleAttempts++;
            return RoleAttempts == 1
                ? Task.FromException<IReadOnlyList<RoleDefinition>>(new AuthorizationAdminException(503, "http.503"))
                : Task.FromResult<IReadOnlyList<RoleDefinition>>([]);
        }
        public Task<IReadOnlyList<AuthorizationCapabilityDefinition>> ListCapabilityDefinitionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AuthorizationCapabilityDefinition>>([]);
        public Task<IReadOnlyList<StandingDefinition>> ListStandingCatalogueAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StandingDefinition>>([]);
        public Task<AuthorizationBinding> GetEffectiveBindingAsync(Guid definitionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<NarrowAuthorizationBindingResult> NarrowCapabilityBindingAsync(Guid definitionId, IReadOnlyList<RoleReference> selectedRoles, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
