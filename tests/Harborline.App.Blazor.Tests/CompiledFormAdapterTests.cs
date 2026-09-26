using System.Text.Json;
using Bunit;
using Harborline.App.Blazor.ReferenceHost.Workshop;
using Harborline.UIAdapters.Blazor;
using Harborline.UIAdapters.Blazor.Components.Forms;

namespace Harborline.App.Blazor.Tests;

/// <summary>
/// T-752: the shape harborline-api's render plan now emits for the released access-administration grant form.
/// The signed overlay said controlHint "select" on `residency`; the plan carries the runtime's editor and
/// permitted values instead. `person` has no value domain and keeps its authored hint.
/// </summary>
public sealed class CompiledFormAdapterTests : BunitContext
{
    private const string Entry = """
        {"id":"access.grant-a-role","version":"1.0.0","status":"Published","body":{},
         "renderPlan":{"definitionHash":"h","definitionId":"access.grant-a-role","definitionVersion":"1.0.0",
          "packKey":"harborline.access-administration","packVersion":"1.1.4","definitionKind":"FormDefinition",
          "bindings":{
           "fields":{"person":{"type":"text","required":true,"options":null},
                     "residency":{"type":"select","required":true,"options":["cache","online-only"]}},
           "overlay":{"title":{"kind":"Literal","value":"Grant a role"},
            "fields":{"person":{"label":{"kind":"Literal","value":"Person"},"controlHint":"textarea"},
                      "residency":{"label":{"kind":"Literal","value":"Residency"},"controlHint":"RadioGroup","permittedValues":["cache","online-only"]}},
            "sections":[{"id":"grant","title":{"kind":"Literal","value":"Grant a role"},"fields":["person","residency"]}]}}}}
        """;

    private static SchemaFormView GrantForm()
    {
        var json = JsonDocument.Parse(Entry).RootElement;
        var entry = json.Deserialize<WorkshopCatalogueEntry>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!
            with { CompiledBindings = json.GetProperty("renderPlan").GetProperty("bindings").Clone() };
        return CompiledFormAdapter.From(entry);
    }

    [Fact(DisplayName = "T-752: a value-domain field renders the runtime's editor with its permitted values")]
    public void Value_domain_field_renders_the_runtime_editor_with_its_permitted_values()
    {
        var view = GrantForm();
        var residency = Assert.Single(view.Sections.SelectMany(section => section.Fields), field => field.Name == "residency");
        Assert.Equal("RadioGroup", residency.ControlHint);
        Assert.Equal(["cache", "online-only"], residency.PermittedValues);

        Services.AddHarborlineUiAdapters();
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<HarborlineSchemaForm>(parameters => parameters
            .Add(form => form.View, view)
            .Add(form => form.Values, new Dictionary<string, object?>())
            .Add(form => form.OnSubmit, _ => ValueTask.FromResult<SchemaFormValidationResult?>(null)));
        var group = cut.Find("[role='radiogroup']");
        Assert.Equal(["cache", "online-only"],
            group.QuerySelectorAll("input[type='radio']").Select(radio => radio.GetAttribute("value")));
    }

    [Fact(DisplayName = "T-752: a field with no value domain keeps its authored hint")]
    public void Field_without_a_value_domain_keeps_its_authored_hint()
    {
        var person = Assert.Single(GrantForm().Sections.SelectMany(section => section.Fields), field => field.Name == "person");
        Assert.Equal("textarea", person.ControlHint);
        Assert.Null(person.PermittedValues);
    }
}
