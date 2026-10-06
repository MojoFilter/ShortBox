namespace ShortBox.Services;

public interface IPageCache
{
    Task DecacheBooksAsync(IEnumerable<Book> booksToDecache, CancellationToken cancellationToken);
    Task<IEnumerable<BookId>> GetCachedBookIdsAsync(CancellationToken cancellationToken);
    Task<Stream> GetCoverAsync(BookId bookId, string bookFileName, CancellationToken ct);
    /// <exception cref="KeyNotFoundException">The page number is outside the book.</exception>
    /// <exception cref="InvalidDataException">The archive has no page images.</exception>
    Task<PageImage> GetPageAsync(BookId bookId, string bookFileName, int pageNumber, CancellationToken ct);

    /// <summary>Whether the book's pages are ready to serve. Never extracts.</summary>
    Task<BookPageStatus> GetStatusAsync(BookId bookId, CancellationToken ct);

    /// <summary>Forgets a failed extraction so the book reads as <see cref="PageState.Pending"/> until it is tried again.</summary>
    Task ClearFailureAsync(BookId bookId, CancellationToken ct);

    /// <summary>Extracts the book if it is not ready (sharing any extraction already under way) and returns its page count.</summary>
    /// <exception cref="InvalidDataException">The archive has no page images.</exception>
    Task<int> PrepareAsync(BookId bookId, string bookFileName, CancellationToken ct);
}
