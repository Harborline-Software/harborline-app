using System.Text.Json;

namespace Harborline.App.Blazor.ReferenceHost.Admin;

/// <summary>
/// Ticket 094: every local-node admin api answers <c>{ code, detail? }</c> on error. This is the one
/// parse the Forms, Reports, Views and DataExchange clients share: the code is carried through for
/// localization, the message renders code + detail where the retired <c>error</c> prose was shown,
/// and a body without a code degrades to the HTTP reason phrase.
/// </summary>
/// <param name="Message">The message to show, already rendered from the code and its detail.</param>
/// <param name="Code">The machine-readable code, or <see langword="null"/> when the body had none.</param>
/// <param name="Detail">The named detail fields the code carries, when any.</param>
internal readonly record struct AdminError(
    string Message,
    string? Code,
    IReadOnlyDictionary<string, string>? Detail)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Reads the error envelope off a failed response.</summary>
    public static async Task<AdminError> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        string? code = null;
        IReadOnlyDictionary<string, string>? detail = null;
        try
        {
            var wire = JsonSerializer.Deserialize<ErrorWire>(body, JsonOptions);
            code = string.IsNullOrEmpty(wire?.Code) ? null : wire!.Code;
            detail = code is null ? null : ReadDetail(wire!.Detail);
        }
        catch (JsonException)
        {
            // A malformed error body falls back to the HTTP reason phrase.
        }

        var fallback = response.ReasonPhrase ?? "Request failed.";
        var parameters = detail is null ? string.Empty : string.Join(", ", detail.Select(pair => $"{pair.Key}={pair.Value}"));
        var message = code is null
            ? fallback
            : parameters.Length > 0 ? $"{code} ({parameters})" : code;

        return new AdminError(message, code, detail);
    }

    /// <summary>Named detail fields, in wire order; a non-object detail is treated as absent.</summary>
    private static IReadOnlyDictionary<string, string>? ReadDetail(JsonElement? detail)
    {
        if (detail is not { ValueKind: JsonValueKind.Object } element)
        {
            return null;
        }

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            fields[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()!
                : property.Value.GetRawText();
        }

        return fields;
    }

    private sealed record ErrorWire(string? Code, JsonElement? Detail);
}
