namespace ShortBox.Services;

public interface IPageCache
{
    Task DecacheBooksAsync(IEnumerable<Book> booksToDecache, CancellationToken cancellationToken);
    Task<IEnumerable<BookId>> GetCachedBookIdsAsync(CancellationToken cancellationToken);
    Task<Stream> GetCoverAsync(BookId bookId, string bookFileName, CancellationToken ct);
    /// <exception cref="KeyNotFoundException">The page number is outside the book.</exception>
    /// <exception cref="InvalidDataException">The archive has no page images.</exception>
    Task<PageImage> GetPageAsync(BookId bookId, string bookFileName, int pageNumber, CancellationToken ct);
}
