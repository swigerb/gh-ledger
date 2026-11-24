using System.Globalization;
using CopilotLedger.Cli.Reporting;

namespace CopilotLedger.Cli.Export;

// How the export names repositories, scopes, and counts. Local paths are shown as their folder names unless the export
// includes them.
internal static class ExportRecords
{
    // A shown repository key: a local root (one of roots, or one that looks like a path) becomes its folder name unless
    // includeLocalPaths; a slug or (none) stays as it is.
    public static string Repository(string key, IReadOnlySet<string> roots, bool includeLocalPaths) =>
        !includeLocalPaths && (roots.Contains(key) || LooksLikeLocalPath(key)) ? FolderName(key) : key;

    // The last segment after / or \; the path itself when it has none.
    public static string FolderName(string path)
    {
        int separator = path.LastIndexOfAny(['/', '\\']);
        return separator < 0 || separator == path.Length - 1 ? path : path[(separator + 1)..];
    }

    // Starts with / or \, or with a drive letter and a colon.
    public static bool LooksLikeLocalPath(string key) =>
        key.StartsWith('/') || key.StartsWith('\\') || (key.Length >= 2 && char.IsAsciiLetter(key[0]) && key[1] == ':');

    // "<repository, or all repositories>[, branch <b>][, pull request #<n>][, conversation <id>]". A local root shows as
    // its folder name even when the export includes local paths, so standard output never shows a path.
    public static string Scope(ExportFilter filter) =>
        (filter.Repository is null
            ? "all repositories"
            : LooksLikeLocalPath(filter.Repository) ? FolderName(filter.Repository) : filter.Repository)
        + (filter.Branch is null ? string.Empty : $", branch {filter.Branch}")
        + (filter.PullRequest is int number
            ? ", pull request #" + number.ToString(CultureInfo.InvariantCulture)
            : string.Empty)
        + (filter.Conversation is null ? string.Empty : $", conversation {filter.Conversation}");

    // count with its noun: "1 request", "8 requests".
    public static string Plural(long count, string one, string many) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? one : many)}";
}
