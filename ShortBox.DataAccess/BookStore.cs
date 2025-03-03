namespace ShortBox.DataAccess;

public interface IBookStore
{
    IAsyncEnumerable<Book> GetRecentBooksAsync(CancellationToken cancellationToken);
}

internal class BookStore(
    IDbContextFactory<ShortBoxContext> contextFactory) : IBookStore
{
    public async IAsyncEnumerable<Book> GetRecentBooksAsync([EnumeratorCancellation]CancellationToken cancellationToken)
    {
        using var context = await this.GetContextAsync(cancellationToken).ConfigureAwait(false);
        var books = context
                    .Books
                    .WhereUnread()
                    .OrderByDescending(b => b.Modified)
                    .AsAsyncEnumerable();
        await foreach(var book in books)
        {
            yield return book;
        }
    }

    private Task<ShortBoxContext> GetContextAsync(CancellationToken cancellationToken) =>
        _contextFactory.CreateDbContextAsync(cancellationToken);

    private IDbContextFactory<ShortBoxContext> _contextFactory = contextFactory;
}
