namespace ShortBox.Services;

internal static class PageImageExtensions
{
    public static bool IsPageImage(string? fileName) => Supported.Contains(Path.GetExtension(fileName));

    private static readonly HashSet<string?> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp"
    };
}
