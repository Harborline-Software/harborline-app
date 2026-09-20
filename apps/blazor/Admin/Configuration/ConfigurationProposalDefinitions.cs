using System.Text.Json;

using Harborline.UIAdapters.Blazor.Components.Forms;

namespace Harborline.App.Blazor.ReferenceHost.Admin.Configuration;

/// <summary>
/// T-461. The platform's RELEASED Proposed change Form and its Proposed change / Saved version /
/// Released package vocabulary, read from the embedded copy of <c>platform-package-ck-7</c> that
/// <c>scripts/build-local-feed.mjs</c> writes out of the pinned platform checkout. The app authors
/// no step vocabulary and no step label of its own.
/// </summary>
public static class ConfigurationProposalDefinitions
{
    private const string ResourceName = "Harborline.App.Blazor.ReferenceHost.configuration-proposal.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonElement Vocabulary = LoadStatuses();

    /// <summary>The released read-only Proposed change Form.</summary>
    public static SchemaFormView Detail { get; } = LoadDetail();

    /// <summary>The released English label for one step code; this lane authors none of its own.</summary>
    public static string Label(string step) =>
        Vocabulary.GetProperty(step).GetProperty("values").GetProperty("en").GetString()!;

    private static JsonDocument Load()
    {
        using var stream = typeof(ConfigurationProposalDefinitions).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException($"{ResourceName} is missing: rebuild the local feed from the pinned platform checkout.");
        return JsonDocument.Parse(stream);
    }

    private static SchemaFormView LoadDetail()
    {
        using var document = Load();
        return document.RootElement.GetProperty("detail").Deserialize<SchemaFormView>(JsonOptions)
            ?? throw new InvalidDataException($"{ResourceName} carries no configurationProposalDetail.");
    }

    private static JsonElement LoadStatuses()
    {
        using var document = Load();
        return document.RootElement.GetProperty("statuses").Clone();
    }
}
