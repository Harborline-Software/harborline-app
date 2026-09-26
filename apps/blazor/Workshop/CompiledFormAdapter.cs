using System.Text.Json;
using Harborline.UIAdapters.Blazor.Components.Forms;

namespace Harborline.App.Blazor.ReferenceHost.Workshop;

public static class CompiledFormAdapter
{
    public static SchemaFormView From(WorkshopCatalogueEntry entry)
    {
        if (entry.RenderPlan?.DefinitionKind != "FormDefinition")
            throw new InvalidOperationException("The definition has no compiled form plan.");
        var bindings = entry.CompiledBindings;
        var fields = bindings.GetProperty("fields");
        var overlay = bindings.GetProperty("overlay");
        var fieldOverlay = overlay.GetProperty("fields");
        return new SchemaFormView
        {
            FormId = entry.Id, Version = entry.Version,
            Title = overlay.TryGetProperty("title", out var title) ? Text(title) : null,
            Description = overlay.TryGetProperty("description", out var description) ? Text(description) : null,
            Sections = overlay.GetProperty("sections").EnumerateArray().Select(section => new SchemaFormSection
            {
                Id = section.GetProperty("id").GetString()!,
                Title = Text(section.GetProperty("title")),
                Fields = section.GetProperty("fields").EnumerateArray().Select(name =>
                {
                    var key = name.GetString()!;
                    var metadata = fields.GetProperty(key);
                    var presentation = fieldOverlay.GetProperty(key);
                    return new SchemaFormField
                    {
                        Name = key,
                        Label = Text(presentation.GetProperty("label")),
                        ControlHint = presentation.TryGetProperty("controlHint", out var hint)
                            ? hint.GetString() : metadata.GetProperty("type").GetString(),
                        // T-752: on a value-domain field the plan carries the runtime's editor and its members.
                        PermittedValues = presentation.TryGetProperty("permittedValues", out var permitted)
                            ? permitted.EnumerateArray().Select(value => value.GetString()!).ToArray() : null,
                        Required = metadata.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.True,
                        ReadOnly = presentation.TryGetProperty("readOnly", out var readOnly) && readOnly.ValueKind == JsonValueKind.True,
                        Options = ReadOptions(metadata),
                    };
                }).ToArray(),
            }).ToArray(),
        };
    }

    private static IReadOnlyList<SchemaFormOption> ReadOptions(JsonElement metadata) =>
        metadata.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Array
            ? options.EnumerateArray().Select(option => option.ValueKind == JsonValueKind.String
                ? new SchemaFormOption(option.GetString()!, SchemaFormText.From(option.GetString()!))
                : new SchemaFormOption(option.GetProperty("value").GetString()!, Text(option.GetProperty("label")))).ToArray()
            : [];

    private static SchemaFormText Text(JsonElement text)
    {
        if (text.ValueKind == JsonValueKind.String) return SchemaFormText.From(text.GetString()!);
        if (text.TryGetProperty("kind", out var kind) && kind.GetString() == "Literal")
            return SchemaFormText.From(text.GetProperty("value").GetString()!);
        if (text.TryGetProperty("defaultLocale", out var locale) && text.TryGetProperty("values", out var values))
            return new SchemaFormText(locale.GetString()!, values.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetString()!));
        throw new InvalidOperationException("The compiled form contains an unsupported text binding.");
    }
}
