using ShortBox.Api.Data;
using ShortBox.Services;

namespace ShortBox.Azure.Services;

/// <summary>Storage of extracted pages, one folder per book. Exists so the extraction rules can be tested without Azure.</summary>
internal interface IPageBlobs
{
    /// <summary>The book's manifest, or null when it is missing, unreadable or from another version (i.e. not ready).</summary>
    Task<PageManifest?> ReadManifestAsync(BookId bookId, CancellationToken ct);

    /// <summary>Stores a page, replacing any earlier copy.</summary>
    Task WritePageAsync(BookId bookId, PageUpload page, CancellationToken ct);

    /// <summary>Stores the manifest. This is what marks the book ready, so it must follow every page.</summary>
    Task WriteManifestAsync(BookId bookId, PageManifest manifest, CancellationToken ct);

    /// <summary>Removes anything in the book's folder that the manifest does not list: leftovers of failed or older extractions.</summary>
    Task DeleteUnlistedAsync(BookId bookId, PageManifest manifest, CancellationToken ct);

    Task<PageImage> OpenPageAsync(BookId bookId, ManifestPage page, CancellationToken ct);

    /// <summary>Removes the book's folder, manifest first so a reader never sees a ready book with missing pages.</summary>
    Task DeleteBookAsync(BookId bookId, CancellationToken ct);
}
