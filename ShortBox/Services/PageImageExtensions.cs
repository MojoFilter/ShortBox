namespace ShortBox.Services;

internal static class PageImageExtensions
{
    /// <summary>True for a real page image: a supported extension, outside archiver metadata such as <c>__MACOSX/._01.jpg</c>.</summary>
    public static bool IsPageImage(string? entryPath) =>
        Supported.ContainsKey(Path.GetExtension(entryPath) ?? string.Empty) && !IsArchiverMetadata(entryPath);

    /// <summary>
    /// The page images of an archive in reading order. Sorted by full entry path, ordinally, so pages in
    /// different folders never collide and every reader of the archive agrees on what page 0 is.
    /// </summary>
    public static IEnumerable<T> InReadingOrder<T>(this IEnumerable<T> entries, Func<T, string?> entryPath) =>
        entries
            .Where(e => IsPageImage(entryPath(e)))
            .OrderBy(e => entryPath(e), StringComparer.Ordinal);

    public static string GetContentType(string? entryPath) =>
        Supported.GetValueOrDefault(Path.GetExtension(entryPath) ?? string.Empty, "application/octet-stream");

    private static bool IsArchiverMetadata(string? entryPath)
    {
        if (string.IsNullOrEmpty(entryPath))
        {
            return false;
        }
        var segments = entryPath.Split('/', '\\');
        return segments.Any(s => s.Equals("__MACOSX", StringComparison.OrdinalIgnoreCase))
            || segments[^1].StartsWith("._", StringComparison.Ordinal);
    }

    private static readonly Dictionary<string, string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp"
    };
}
