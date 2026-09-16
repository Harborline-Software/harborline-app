using System.Text.Json;
using Harborline.App.Blazor.ReferenceHost.Runtime;

namespace Harborline.App.Blazor.Tests;

public sealed class PackActionSnapshotTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Shared_snapshot_preserves_rows_denial_and_native_receipts()
    {
        var snapshot = JsonSerializer.Deserialize<PackActionSnapshot>("""
            {"plan":null,"rows":[{"id":"row-42","values":{"identity":"row-42","title":"Example","active":true}}],
             "selectedId":"row-42","activeAction":null,"inputPlan":null,"busy":false,"error":null,
             "receipt":{"status":403,"code":"authorization.permission_required","body":{"code":"authorization.permission_required"},
             "text":"native refusal","auditId":"native-audit","correlationId":"native-correlation"}}
            """, JsonOptions)!;
        Assert.Equal("row-42", Assert.Single(snapshot.RuntimeRows).Id);
        Assert.Equal(403, snapshot.Receipt!.Status);
        Assert.Equal("native-audit", snapshot.Receipt.AuditId);
        Assert.Equal("native-correlation", snapshot.Receipt.CorrelationId);
        Assert.Equal("native refusal", snapshot.Receipt.Text);
        Assert.Null(snapshot.Form);
    }

    [Fact]
    public void Shared_normalized_inline_plan_reuses_the_compiled_form_adapter()
    {
        var snapshot = JsonSerializer.Deserialize<PackActionSnapshot>("""
            {"plan":null,"rows":[],"selectedId":null,"activeAction":{"id":"opaque","label":"Apply"},
             "inputPlan":{"definitionId":"opaque","definitionVersion":"1","definitionKind":"FormDefinition",
             "bindings":{"fields":{"scope":{"type":"text","required":true}},
             "overlay":{"fields":{"scope":{"label":"Scope"}},"sections":[{"id":"details","title":"Details","fields":["scope"]}]}}},
             "receipt":null,"error":null,"busy":false}
            """, JsonOptions)!;
        var field = Assert.Single(Assert.Single(snapshot.Form!.Sections).Fields);
        Assert.Equal("scope", field.Name);
        Assert.True(field.Required);
    }
}
