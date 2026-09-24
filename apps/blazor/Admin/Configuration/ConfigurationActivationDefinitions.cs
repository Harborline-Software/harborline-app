using System.Text.Json;

using Harborline.UIAdapters.Blazor.Components.Forms;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Configuration;

/// <summary>
/// T-460. The platform's RELEASED configuration activation Form, read from the embedded copy of
/// <c>platform-package-ck-7</c> that <c>scripts/build-local-feed.mjs</c> writes out of the pinned
/// platform checkout. The app authors no status vocabulary and no status label of its own.
/// </summary>
public static class ConfigurationActivationDefinitions
{
    private const string ResourceName = "Harborline.App.Blazor.ReferenceHost.configuration-activation.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The released read-only activation Form.</summary>
    public static SchemaFormView Detail { get; } = Load();

    private static SchemaFormView Load()
    {
        using var stream = typeof(ConfigurationActivationDefinitions).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException($"{ResourceName} is missing: rebuild the local feed from the pinned platform checkout.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("detail").Deserialize<SchemaFormView>(JsonOptions)
            ?? throw new InvalidDataException($"{ResourceName} carries no configurationActivationDetail.");
    }
}
