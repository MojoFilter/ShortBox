namespace ShortBox.Services;

public interface IPageCache
{
    Task<Stream> GetCoverAsync(BookId bookId, string bookFileName, CancellationToken ct);
    Task<Stream> GetPageAsync(BookId bookId, string bookFileName, int pageNumber, CancellationToken ct);
}
