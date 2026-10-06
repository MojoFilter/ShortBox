namespace ShortBox.Services;

/// <summary>What a reader needs to know about a page before it has loaded it.</summary>
/// <param name="Index">Zero-based position in reading order; the number to ask for the page by.</param>
/// <param name="Width">Pixel width, or null when the image header could not be read.</param>
public sealed record PageInfo(int Index, string ContentType, int? Width, int? Height)
{
    /// <summary>Width over height, for laying a page out ahead of its image. Null when either dimension is unknown.</summary>
    public double? AspectRatio => Width is > 0 && Height is > 0 ? (double)Width / Height : null;
}

/// <summary>The pages of a book, in reading order, together with whether they can be served yet.</summary>
/// <param name="Pages">Empty unless <paramref name="Status"/> is <see cref="PageState.Ready"/>: a book's pages are not known until it is extracted.</param>
public sealed record BookPages(BookPageStatus Status, IReadOnlyList<PageInfo> Pages)
{
    public static BookPages NotReady(BookPageStatus status) => new(status, []);

    public static BookPages From(PageManifest manifest) => new(
        BookPageStatus.Ready(manifest.PageCount),
        [.. manifest.Pages.Select(p => new PageInfo(p.Index, p.ContentType, p.Width, p.Height))]);
}
