using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Workshop;
using Harborline.UIAdapters.Blazor.Components.DataDisplay;
using Harborline.UIAdapters.Blazor.Components.Forms;

namespace Harborline.App.Blazor.ReferenceHost.Runtime;

public sealed record PackActionDeclaration(string Id, string Label, PackFileInput? FileInput);
public sealed record PackFileInput(string Accept);
public sealed record PackActionReceipt(int Status, string? Code, JsonElement Body, string Text, string? AuditId, string? CorrelationId);
public sealed record PackActionRow(string Id, IReadOnlyDictionary<string, JsonElement> Values);

public sealed record PackActionSnapshot(ViewRenderPlan? Plan, IReadOnlyList<PackActionRow> Rows,
    string? SelectedId, PackActionDeclaration? ActiveAction, JsonElement InputPlan,
    PackActionReceipt? Receipt, string? Error, bool Busy)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<ViewRuntimeRow> RuntimeRows => Rows.Select(row => new ViewRuntimeRow(row.Id,
        row.Values.ToDictionary(pair => pair.Key, pair => Value(pair.Value)))).ToArray();

    public SchemaFormView? Form => InputPlan.ValueKind != JsonValueKind.Object ? null : CompiledFormAdapter.From(
        new WorkshopCatalogueEntry(InputPlan.GetProperty("definitionId").GetString()!,
            InputPlan.GetProperty("definitionVersion").GetString()!, "Active", null, default,
            InputPlan.Deserialize<ViewRenderPlan>(JsonOptions)) { CompiledBindings = InputPlan.GetProperty("bindings") });

    private static object? Value(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(), JsonValueKind.Number => value.GetDecimal(),
        JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null,
        _ => value.Clone(),
    };
}
