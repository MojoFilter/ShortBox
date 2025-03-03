namespace ShortBox.Services;

public interface IBookCoverFileBusiness
{
    Task<Stream> GetCoverStreamAsync(BookId bookId, CancellationToken ct);
}
