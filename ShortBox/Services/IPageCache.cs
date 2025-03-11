namespace ShortBox.Services;

public interface IPageCache
{
    Task<Stream> GetPageAsync(BookId bookId, string bookFileName, int pageNumber, CancellationToken ct);
}
