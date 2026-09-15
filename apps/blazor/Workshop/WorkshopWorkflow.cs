using System.Text.Json;
using Harborline.UIAdapters.Blazor.Components.Forms;

namespace Harborline.App.Blazor.ReferenceHost.Workshop;

public sealed record WorkshopAction(string Id, string Label, string Operation, string? InputForm = null, string? Input = null);

public sealed class WorkshopWorkflow(IWorkshopCatalogueClient client)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private IReadOnlyList<WorkshopAction> actions = [];
    private WorkshopAction? formAction;
    private JsonElement? candidate;
    private byte[]? artifact;
    private bool validated, verified, checkedForInstall, installed, active;
    private string? typeId;
    private JsonElement? type;
    private JsonElement? created;
    private IReadOnlyDictionary<string, object?> authorValues = new Dictionary<string, object?>();

    public SchemaFormView? Form { get; private set; }
    public IReadOnlyDictionary<string, object?> Values { get; private set; } = new Dictionary<string, object?>();
    public string? FormLabel => formAction?.Label;
    public string? ResultLabel { get; private set; }
    public string? ResultText { get; private set; }
    public string? Error { get; private set; }
    public bool Busy { get; private set; }
    public string? DownloadUrl => artifact is null ? null : "data:application/octet-stream;base64," + Convert.ToBase64String(artifact);
    public string? DownloadName => candidate is { } pack ? $"{pack.GetProperty("key").GetString()}-{pack.GetProperty("version").GetString()}.pack" : null;
    public string? DownloadLabel => actions.FirstOrDefault(action => action.Operation == "pack.export")?.Label;

    public void Bind(WorkshopCatalogueEntry view)
    {
        actions = []; Form = null; formAction = null; candidate = null;
        Values = authorValues = new Dictionary<string, object?>();
        Error = null; ResultText = null; ResultLabel = null;
        ResetAfterCandidate();
        if (!Supports(view)) return;
        var bindings = view.CompiledBindings;
        actions = bindings.ValueKind == JsonValueKind.Object && bindings.TryGetProperty("parameters", out var parameters)
            && parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("actions", out var declarations)
            && declarations.ValueKind == JsonValueKind.Array
            ? declarations.Deserialize<WorkshopAction[]>(JsonOptions) ?? [] : [];
    }

    public static bool Supports(WorkshopCatalogueEntry view) => view.RenderPlan is
        { DefinitionKind: "ViewDefinition", Bindings: { ViewKind: "views.entity-list/grid", Parameters.Fields: not null } };

    public void ChangeValues(IReadOnlyDictionary<string, object?> values)
    {
        if (Busy) return;
        Values = values;
        Error = null; ResultText = null;
        if (formAction?.Operation != "pack.validate") return;
        authorValues = values;
        candidate = null;
        ResetAfterCandidate();
        Error = null; ResultText = null;
    }

    private void ResetAfterCandidate()
    {
        validated = verified = checkedForInstall = installed = active = false;
        artifact = null; typeId = null; type = null; created = null;
    }

    public async Task<bool> ActivateActionAsync(string actionId, CancellationToken cancellationToken = default)
    {
        if (Busy) return false;
        Busy = true; Error = null;
        try
        {
            var action = actions.SingleOrDefault(item => item.Id == actionId)
                ?? throw new InvalidOperationException("The selected action is not declared by this view.");
            ResultLabel = action.Label;
            switch (action.Operation)
            {
                case "pack.validate":
                    Require(!string.IsNullOrWhiteSpace(action.InputForm), "The action has no declared input form.");
                    Form = CompiledFormAdapter.From(await client.ReadFormAsync(action.InputForm!, cancellationToken: cancellationToken));
                    formAction = action; Values = authorValues;
                    ResultText = null;
                    return false;
                case "pack.export":
                    Require(validated && candidate.HasValue, "Validate the current pack before exporting it.");
                    artifact = await client.ExportAsync(candidate!.Value, cancellationToken);
                    verified = installed = active = false; type = null; created = null;
                    Show(new { bytes = artifact.Length, file = DownloadName });
                    return false;
                case "pack.verify":
                    Require(artifact is not null, "Export the current pack before verifying it.");
                    verified = checkedForInstall = installed = active = false;
                    var verification = await client.PostArtifactAsync("api/local-node/packs/verify", artifact!, cancellationToken);
                    Show(verification);
                    verified = verification.TryGetProperty("verdict", out var verdict) && verdict.GetString() == "Verified";
                    Require(verified, "The node did not verify this pack.");
                    return false;
                case "pack.check":
                    Require(verified && artifact is not null, "Verify the current pack before checking it.");
                    checkedForInstall = installed = active = false;
                    var check = await client.PostArtifactAsync("api/local-node/packs/preview", artifact!, cancellationToken);
                    Show(check);
                    checkedForInstall = CheckPassed(check);
                    Require(checkedForInstall, "Pack check refused this candidate. Review every reported code and pointer before installing it.");
                    return false;
                case "pack.install":
                    Require(artifact is not null && (actions.Any(item => item.Operation == "pack.check") ? checkedForInstall : verified),
                        actions.Any(item => item.Operation == "pack.check")
                            ? "Check the current pack before installing it."
                            : "Verify the current pack before installing it.");
                    installed = active = false;
                    var installation = await client.PostArtifactAsync("api/local-node/packs/install", artifact!, cancellationToken);
                    Show(installation);
                    installed = IsTrue(installation, "installed");
                    Require(installed, "The node did not install this pack.");
                    return false;
                case "pack.activate":
                    Require(installed && candidate.HasValue, "Install the current pack before activating it.");
                    active = false;
                    var pack = candidate!.Value;
                    var activation = await client.PostJsonAsync("api/local-node/packs/activate", new
                    {
                        packKey = pack.GetProperty("key").GetString(), version = pack.GetProperty("version").GetString(),
                    }, cancellationToken);
                    Show(activation);
                    Require(IsTrue(activation, "activated") && Empty(activation, "projectionRefusals") && Empty(activation, "platformRefusals"),
                        "The node did not activate and project this pack without refusals.");
                    var types = pack.GetProperty("contents").EnumerateArray()
                        .Where(item => item.GetProperty("kind").GetString() == "AssetTypeDefinition").ToArray();
                    Require(types.Length == 1, "Select a pack containing exactly one bound record type for this workflow.");
                    typeId = types[0].GetProperty("key").GetString()!;
                    type = await client.ReadJsonAsync($"api/local-node/asset-registry/types/{Uri.EscapeDataString(typeId)}", cancellationToken);
                    Require(type.Value.TryGetProperty("propertyForm", out var binding) && binding.ValueKind == JsonValueKind.Object,
                        "The active record type has no resolved property form.");
                    active = true;
                    Show(new { activation, binding = type });
                    return true;
                case "record.create":
                    Require(active && type.HasValue && action.Input == "active-pack.property-form", "Activate a pack with a bound property form before creating a record.");
                    var propertyForm = type!.Value.GetProperty("propertyForm");
                    Form = CompiledFormAdapter.From(await client.ReadFormAsync(propertyForm.GetProperty("definition").GetString()!,
                        propertyForm.GetProperty("version").GetString(), cancellationToken));
                    formAction = action; Values = new Dictionary<string, object?>();
                    ResultText = null;
                    return false;
                case "record.read":
                    Require(created.HasValue, "Create a record before reading it.");
                    var record = await client.ReadJsonAsync($"api/local-node/asset-registry/entities/{Uri.EscapeDataString(created!.Value.GetProperty("id").GetString()!)}", cancellationToken);
                    Show(record);
                    Require(created.Value.TryGetProperty("auditId", out var auditId) && auditId.ValueKind == JsonValueKind.String,
                        "The accepted record write returned no audit id.");
                    var trace = await client.ReadJsonAsync($"api/local-node/authorization/traces/{Uri.EscapeDataString(auditId.GetString()!)}", cancellationToken);
                    Show(new { record, trace });
                    return false;
                default:
                    throw new InvalidOperationException("The declared action operation is not supported.");
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or JsonException or KeyNotFoundException)
        {
            Refused(exception); return false;
        }
        finally { Busy = false; }
    }

    public async ValueTask<SchemaFormValidationResult?> SubmitAsync(IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        if (Busy) return new(false, [new("", "An operation is already in progress.")]);
        Busy = true; Error = null; ResultLabel = formAction?.Label;
        try
        {
            Values = values;
            switch (formAction?.Operation)
            {
                case "pack.validate":
                    authorValues = values;
                    ResetAfterCandidate();
                    Require(values.TryGetValue("packJson", out var source) && source is string, "The pack document must be JSON text.");
                    candidate = JsonElement.Parse((string)source!);
                    Require(candidate.Value.ValueKind == JsonValueKind.Object, "The pack document must be a JSON object.");
                    var validation = await client.PostJsonAsync("api/local-node/packs/export?validateOnly=true", candidate.Value, cancellationToken);
                    Show(validation);
                    validated = IsTrue(validation, "valid");
                    return validated ? SchemaFormValidationResult.Valid : Validation(validation, "/packJson");
                case "record.create":
                    Require(active && typeId is not null && type.HasValue, "Activate a bound record type before submitting a record.");
                    created = null;
                    var displayName = values.Values.OfType<string>().FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                        ?? type!.Value.GetProperty("displayName").GetString();
                    var receipt = await client.PostJsonAsync("api/local-node/asset-registry/entities", new { type = typeId, displayName, values }, cancellationToken);
                    Show(receipt);
                    Require(HasText(receipt, "id") && HasText(receipt, "auditId"),
                        "The node accepted the request but returned no complete record and audit receipt. The record may exist; do not submit again without checking.");
                    created = receipt;
                    return SchemaFormValidationResult.Valid;
                default:
                    throw new InvalidOperationException("No declared submission is selected.");
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or JsonException or KeyNotFoundException)
        {
            Refused(exception);
            if (exception is WorkshopRequestException request)
            {
                try { return Validation(JsonElement.Parse(request.ResponseBody), formAction?.Operation == "pack.validate" ? "/packJson" : ""); }
                catch (JsonException) { }
            }
            return new(false, [new(formAction?.Operation == "pack.validate" ? "/packJson" : "", exception.Message)]);
        }
        finally { Busy = false; }
    }

    private static SchemaFormValidationResult Validation(JsonElement body, string fallback)
    {
        var message = body.TryGetProperty("code", out var code) ? code.GetString()! : body.GetRawText();
        var errors = body.TryGetProperty("pointers", out var pointers) && pointers.ValueKind == JsonValueKind.Array
            ? pointers.EnumerateArray().Select(pointer => new SchemaFormValidationError(
                pointer.GetString()!.StartsWith("/values/", StringComparison.Ordinal) ? pointer.GetString()![7..] : pointer.GetString()!, message)).ToArray()
            : [new SchemaFormValidationError(fallback, message)];
        return new(false, errors.Length > 0 ? errors : [new(fallback, message)]);
    }

    private void Refused(Exception exception)
    {
        Error = exception.Message;
        if (exception is WorkshopRequestException request) ResultText = request.ResponseBody;
    }
    private void Show(object value) => ResultText = JsonSerializer.Serialize(value, JsonOptions);
    private static bool IsTrue(JsonElement value, string key) => value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.True;
    private static bool HasText(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.GetString());
    private static bool Empty(JsonElement value, string key) => !value.TryGetProperty(key, out var property)
        || property.ValueKind == JsonValueKind.Array && property.GetArrayLength() == 0;
    private static bool CheckPassed(JsonElement value)
    {
        var verdict = value.TryGetProperty("verdict", out var property) ? property.GetString() : null;
        return verdict is "WouldInstall" or "WouldUpgrade"
            && new[] { "conflicts", "watermarkHits", "admissionRefusals", "refusalCodes", "refusals",
                "crossPackCollisions", "unmetContentReferences", "unmetDependencies" }
                .All(key => value.TryGetProperty(key, out var items)
                    && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() == 0);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
