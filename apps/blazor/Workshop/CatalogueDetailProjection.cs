using System.Text.Json;
using System.Text.Json.Nodes;
using Harborline.UIAdapters.Blazor.Components.Forms;

namespace Harborline.App.Blazor.ReferenceHost.Workshop;

public sealed record CatalogueDetailProjection(SchemaFormView Form, IReadOnlyDictionary<string, object?> Values)
{
    public const string Id = "platform.detail.form";
    public const string Version = "1.0.0";
    private static readonly string[] Fields = ["formId", "title", "version", "cascadeLayer"];

    public static object Requests(WorkshopCatalogueEntry definition, WorkshopCatalogueEntry source)
    {
        var mapping = definition.Body.GetProperty("catalogueFieldSource");
        var declared = mapping.GetProperty("fields").EnumerateArray().ToArray();
        Require(definition.Id == Id && definition.Version == Version
            && definition.RenderPlan is { DefinitionKind: "FormDefinition", DefinitionId: Id, DefinitionVersion: Version }
            && mapping.GetProperty("capabilityId").GetString() == "forms.catalogue-field-source"
            && mapping.GetProperty("coordinateSchemaVersion").TryGetInt32(out var coordinateVersion) && coordinateVersion == 1
            && mapping.GetProperty("sourceMappingSchemaVersion").TryGetInt32(out var mappingVersion) && mappingVersion == 1
            && mapping.GetProperty("sourceKind").GetString() == "FormDefinition"
            && declared.Length == Fields.Length
            && declared.Select((field, index) => field.GetProperty("fieldId").GetString() == Fields[index]
                && field.GetProperty("source").GetString() == $"catalogue.entry.{Fields[index]}").All(valid => valid)
            && source.Id is not null && source.CatalogueFieldBinding is not null && CanonicalVersion(source.Version));
        return Fields.Select(field => new
        {
            coordinate = new { schemaVersion = 1, kind = "FormDefinition", id = source.Id, version = source.Version, field },
            sourceBinding = source.CatalogueFieldBinding,
        }).ToArray();
    }

    public static CatalogueDetailProjection From(WorkshopCatalogueEntry definition, JsonElement response)
    {
        var projection = response.GetProperty("projection");
        var binding = projection.GetProperty("detailBinding");
        var provenance = binding.GetProperty("provenance");
        Require(projection.GetProperty("detailId").GetString() == Id && projection.GetProperty("detailVersion").GetString() == Version
            && projection.GetProperty("readOnly").ValueKind == JsonValueKind.True
            && binding.GetProperty("definitionHash").GetString() == "sha256:" + definition.RenderPlan!.DefinitionHash
            && provenance.GetProperty("kind").GetString() == "pack"
            && provenance.GetProperty("packKey").GetString() == definition.RenderPlan.PackKey
            && provenance.GetProperty("packVersion").GetString() == definition.RenderPlan.PackVersion);
        var metadata = projection.GetProperty("fieldsMeta");
        var authored = projection.GetProperty("overlay");
        var presentation = authored.GetProperty("fields");
        var source = projection.GetProperty("values");
        var admitted = metadata.EnumerateObject().Select(field => field.Name).ToArray();
        Require(admitted.All(key => Fields.Contains(key, StringComparer.Ordinal) && presentation.TryGetProperty(key, out _) && source.TryGetProperty(key, out _)));
        var fields = new JsonObject();
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var key in admitted)
        {
            fields[key] = JsonNode.Parse(presentation.GetProperty(key).GetRawText());
            var value = source.GetProperty(key);
            if (value.ValueKind == JsonValueKind.Object)
            {
                var locale = value.GetProperty("defaultLocale").GetString();
                Require(key == "title" && locale is not null);
                values[key] = value.GetProperty("values").GetProperty(locale!).GetString();
                Require(values[key] is string);
            }
            else values[key] = value.ValueKind == JsonValueKind.Null ? null : value.GetString();
        }
        var sections = new JsonArray();
        foreach (var section in authored.GetProperty("sections").EnumerateArray())
            sections.Add(new JsonObject
            {
                ["id"] = section.GetProperty("id").GetString(),
                ["title"] = JsonNode.Parse(section.GetProperty("title").GetRawText()),
                ["fields"] = JsonSerializer.SerializeToNode(section.GetProperty("fields").EnumerateArray().Select(field => field.GetString()!)
                    .Where(key => admitted.Contains(key, StringComparer.Ordinal))),
            });
        var overlay = new JsonObject { ["fields"] = fields, ["sections"] = sections };
        if (authored.TryGetProperty("title", out var title)) overlay["title"] = JsonNode.Parse(title.GetRawText());
        var bindings = JsonSerializer.SerializeToElement(new { fields = metadata, overlay });
        return new(CompiledFormAdapter.From(definition with { CompiledBindings = bindings }), values);
    }

    private static bool CanonicalVersion(string? version) => version is not null && System.Text.RegularExpressions.Regex.IsMatch(version, @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z")
        && version.Split('.').All(part => int.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _));

    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Unsupported catalogue detail."); }
}
