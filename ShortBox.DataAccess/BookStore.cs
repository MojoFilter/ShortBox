using Microsoft.Extensions.Logging;
using ShortBox.Services;
using System.Threading.Tasks;

namespace ShortBox.DataAccess;

public interface IBookStore
{
    Task<byte[]> GetBookCoverAsync(BookId bookId, int? height, CancellationToken ct);
    IAsyncEnumerable<Book> GetRecentBooksAsync(CancellationToken cancellationToken);
    Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken);
    Task<Book> GetBookAsync(BookId bookId, CancellationToken ct);
    Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken ct);
    Task<IEnumerable<Book>> GetSeriesArchiveAsync(string seriesName, CancellationToken ct);
    Task<Stream> GetBookPageAsync(BookId bookId, int pageNumber, CancellationToken ct);
    Task MarkPageAsync(BookId bookId, int pageNumber, CancellationToken ct);
    Task CleanUpReadBooksAsync(CancellationToken cancellationToken);
}

internal class BookStore(
    IDbContextFactory<ShortBoxContext> contextFactory,
    IPageCache pageCache,
    ILogger<BookStore> logger) 
    : IBookStore
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

    public async Task<byte[]> GetBookCoverAsync(BookId bookId, int? height, CancellationToken ct)
    {
        using var context = await this.GetContextAsync(ct).ConfigureAwait(false);
        var book = await this.GetBookByIdAsync(bookId, context, ct).ConfigureAwait(false);
        using var fileStream = await _pageCache.GetCoverAsync(book.Id, book.FileName, ct).ConfigureAwait(false);
        using var localStream = new MemoryStream();
        await fileStream.CopyToAsync(localStream, ct).ConfigureAwait(false);
        return localStream.ToArray();
    }

    public async Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken)
    {
        using var context = await this.GetContextAsync(cancellationToken).ConfigureAwait(false);
        return await context
            .Books
            .WhereUnread()
            .GroupBy(b => b.Series)
            .OrderByDescending(g => g.Select(b => b.Modified).Max())
            .Select(group => new Series(group.Key ?? string.Empty))
            .ToListAsync(cancellationToken);
    }

    public async Task<Book> GetBookAsync(BookId bookId, CancellationToken ct)
    {
        using var context = await this.GetContextAsync(ct).ConfigureAwait(false);
        return (await context.Books.FindAsync(new BookId(bookId.Value)).ConfigureAwait(false))
            ?? throw new KeyNotFoundException($"Book not found with ID {bookId}");
    }

    public Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken ct) => this.GetIssuesAsync(seriesName, true, ct);


    public Task<IEnumerable<Book>> GetSeriesArchiveAsync(string seriesName, CancellationToken ct) => this.GetIssuesAsync(seriesName, false, ct);

    public async Task MarkPageAsync(BookId bookId, int pageNumber, CancellationToken ct)
    {
        using var context = await this.GetContextAsync(ct).ConfigureAwait(false);
        await context.Books
                     .Where(b => b.Id == bookId)
                     .ExecuteUpdateAsync(s =>
                        s.SetProperty(b => b.CurrentPage, pageNumber)
                         .SetProperty(b => b.Modified, DateTime.Now));
    }

    public async Task<Stream> GetBookPageAsync(BookId bookId, int pageNumber, CancellationToken ct)
    {
        _logger.LogInformation("Fetching book details of book {bookId}", bookId);
        var book = await this.GetBookAsync(bookId, ct).ConfigureAwait(false);
        _logger.LogInformation("Got book detail of book {bookId} ({title} #{number})", bookId, book.Series, book.Number);
        return await _pageCache.GetPageAsync(bookId, book.FileName, pageNumber, ct).ConfigureAwait(false);
    }

    private async Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, bool unread, CancellationToken ct)
    { 
        using var context = await this.GetContextAsync(ct).ConfigureAwait(false);
        var books = await context.Books
            .Where(b => string.Equals(b.Series, seriesName))
            .WhereUnread(unread)
            .OrderBy(b => b.Number)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return books.AsEnumerable();
    }


    private Task<ShortBoxContext> GetContextAsync(CancellationToken cancellationToken) =>
        _contextFactory.CreateDbContextAsync(cancellationToken);

    private async Task<Book> GetBookByIdAsync(BookId bookId, ShortBoxContext context, CancellationToken ct) =>
        await context.Books.FirstOrDefaultAsync(b => b.Id == bookId, ct)
            ?? throw new KeyNotFoundException($"Book not found with ID {bookId}");

    public async Task CleanUpReadBooksAsync(CancellationToken cancellationToken)
    {
        var cachedBookIds = await _pageCache.GetCachedBookIdsAsync(cancellationToken).ConfigureAwait(false);
        using var context = await this.GetContextAsync(cancellationToken).ConfigureAwait(false);
        var booksToDecache = await this.GetBooksToDecacheAsync(cachedBookIds, context, cancellationToken).ConfigureAwait(false);
        await _pageCache.DecacheBooksAsync(booksToDecache, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IEnumerable<Book>> GetBooksToDecacheAsync(
        IEnumerable<BookId> cachedBookIds, 
        ShortBoxContext context,
        CancellationToken cancellationToken) => await
        context.Books
            .Where(b => cachedBookIds.Contains(b.Id))
            .Where(b => b.Modified < DateTime.Now.AddDays(-1))
            .WhereRead()
            .ToListAsync(cancellationToken);    

    private IDbContextFactory<ShortBoxContext> _contextFactory = contextFactory;
    private IPageCache _pageCache = pageCache;
    private ILogger<BookStore> _logger = logger;
}
