namespace ShortBox.Services;

public interface IPageCache
{
    Task DecacheBooksAsync(IEnumerable<Book> booksToDecache, CancellationToken cancellationToken);
    Task<IEnumerable<BookId>> GetCachedBookIdsAsync(CancellationToken cancellationToken);
    Task<Stream> GetCoverAsync(BookId bookId, string bookFileName, CancellationToken ct);
    Task<Stream> GetPageAsync(BookId bookId, string bookFileName, int pageNumber, CancellationToken ct);
}
