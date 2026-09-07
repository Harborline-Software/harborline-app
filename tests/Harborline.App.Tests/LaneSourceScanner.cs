using System.Text.RegularExpressions;

namespace Harborline.App.Tests;

/// <summary>
/// Provides the source-text primitives the lane-parity fences share: locating the repository root
/// and carving balanced, quote-aware regions out of React and Blazor sources without compiling or
/// running either lane.
/// </summary>
internal static class LaneSourceScanner
{
    /// <summary>
    /// Locates the repository root by walking upward from the test assembly output directory.
    /// </summary>
    /// <returns>The directory containing the repository solution marker.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when no ancestor contains the marker.</exception>
    internal static string LocateHostSourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Harborline.App.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the Harborline App repository root from '{AppContext.BaseDirectory}'; no ancestor contains Harborline.App.slnx.");
    }

    /// <summary>
    /// Isolates an assigned collection so unrelated source constructs cannot be parsed as fixture data.
    /// </summary>
    /// <param name="source">The complete source text.</param>
    /// <param name="assignmentPattern">A regular expression ending at the collection's opening delimiter.</param>
    /// <param name="opening">The opening delimiter.</param>
    /// <param name="closing">The closing delimiter.</param>
    /// <returns>The balanced collection text, or an empty string when the assignment is absent.</returns>
    internal static string ExtractAssignedCollection(string source, string assignmentPattern, char opening, char closing)
    {
        var match = Regex.Match(source, assignmentPattern, RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return match.Success ? ExtractBalanced(source, match.Index + match.Length - 1, opening, closing) : string.Empty;
    }

    /// <summary>
    /// Returns a balanced delimiter region while ignoring delimiters inside quoted strings.
    /// </summary>
    /// <param name="source">The source containing the region.</param>
    /// <param name="openingIndex">The index of the opening delimiter.</param>
    /// <param name="opening">The opening delimiter.</param>
    /// <param name="closing">The closing delimiter.</param>
    /// <returns>The region including its delimiters, or an empty string when it is unbalanced.</returns>
    internal static string ExtractBalanced(string source, int openingIndex, char opening, char closing)
    {
        var depth = 0;
        var quote = '\0';
        var escaped = false;
        for (var index = openingIndex; index < source.Length; index++)
        {
            var character = source[index];
            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '\"')
            {
                quote = character;
            }
            else if (character == opening)
            {
                depth++;
            }
            else if (character == closing && --depth == 0)
            {
                return source[openingIndex..(index + 1)];
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Finds the first occurrence of a character that is not inside a quoted string.
    /// </summary>
    /// <param name="source">The source to search.</param>
    /// <param name="target">The character to find.</param>
    /// <param name="startIndex">The index to start searching from.</param>
    /// <returns>The zero-based index, or -1 when the character does not occur outside quotes.</returns>
    internal static int IndexOfUnquoted(string source, char target, int startIndex)
    {
        var quote = '\0';
        var escaped = false;
        for (var index = startIndex; index < source.Length; index++)
        {
            var character = source[index];
            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character == target)
            {
                return index;
            }

            if (character is '\'' or '\"')
            {
                quote = character;
            }
        }

        return -1;
    }

    /// <summary>
    /// Splits a delimited list into its top-level segments, ignoring separators that sit inside
    /// quoted strings, brackets, or a generic argument list. A '&lt;' opens a nesting level only when
    /// it directly follows an identifier character, which distinguishes <c>Dictionary&lt;string, string&gt;</c>
    /// from a comparison or a lambda arrow.
    /// </summary>
    /// <param name="text">The list body, without its enclosing delimiters.</param>
    /// <param name="separator">The separator character.</param>
    /// <returns>The trimmed top-level segments in source order.</returns>
    internal static List<string> SplitTopLevel(string text, char separator)
    {
        var segments = new List<string>();
        var depth = 0;
        var quote = '\0';
        var escaped = false;
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            switch (character)
            {
                case '\'':
                case '\"':
                    quote = character;
                    break;
                case '(':
                case '[':
                case '{':
                    depth++;
                    break;
                case ')':
                case ']':
                case '}':
                    depth--;
                    break;
                case '<' when index > 0 && (char.IsLetterOrDigit(text[index - 1]) || text[index - 1] == '_'):
                    depth++;
                    break;
                case '>' when depth > 0 && index > 0 && text[index - 1] != '=':
                    depth--;
                    break;
                default:
                    if (character == separator && depth == 0)
                    {
                        segments.Add(text[start..index].Trim());
                        start = index + 1;
                    }

                    break;
            }
        }

        segments.Add(text[start..].Trim());
        return segments;
    }
}
