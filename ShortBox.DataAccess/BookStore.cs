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
    /// <exception cref="KeyNotFoundException">No such book, or no such page in it.</exception>
    /// <exception cref="InvalidDataException">The book's archive has no page images.</exception>
    Task<PageImage> GetBookPageAsync(BookId bookId, int pageNumber, CancellationToken ct);
    /// <summary>Whether the book's pages are extracted and ready to serve. Never extracts.</summary>
    /// <exception cref="KeyNotFoundException">No such book.</exception>
    Task<BookPageStatus> GetPageStatusAsync(BookId bookId, CancellationToken ct);
    /// <summary>
    /// The pages of the book in reading order, with their content types and dimensions. Empty until the book is ready,
    /// and never extracts: call prepare first.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No such book.</exception>
    Task<BookPages> GetPagesAsync(BookId bookId, CancellationToken ct);
    /// <summary>
    /// Reports where preparation stands and, for a book whose last extraction failed, forgets the failure.
    /// A <see cref="PageState.Pending"/> result means the caller should queue the extraction.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No such book.</exception>
    Task<BookPageStatus> RequestPrepareAsync(BookId bookId, CancellationToken ct);
    /// <summary>Extracts the book's pages if they are not ready. This is the slow work behind <see cref="RequestPrepareAsync"/>.</summary>
    /// <exception cref="KeyNotFoundException">No such book.</exception>
    /// <exception cref="InvalidDataException">The book's archive has no page images.</exception>
    Task PrepareBookAsync(BookId bookId, CancellationToken ct);
    Task MarkPageAsync(BookId bookId, int pageNumber, CancellationToken ct);
    /// <exception cref="KeyNotFoundException">No such book.</exception>
    /// <exception cref="InvalidOperationException">Marking read, but the book's page count is unknown.</exception>
    Task MarkReadAsync(BookId bookId, bool read, CancellationToken ct);
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

    public async Task MarkReadAsync(BookId bookId, bool read, CancellationToken ct)
    {
        using var context = await this.GetContextAsync(ct).ConfigureAwait(false);
        var book = await this.GetBookByIdAsync(bookId, context, ct).ConfigureAwait(false);
        book.CurrentPage = read
            ? book.PageCount ?? throw new InvalidOperationException($"Book {bookId} has no page count, so it cannot be marked read.")
            : 0;
        book.Modified = DateTime.Now;
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<PageImage> GetBookPageAsync(BookId bookId, int pageNumber, CancellationToken ct)
    {
        _logger.LogInformation("Fetching book details of book {bookId}", bookId);
        var book = await this.GetBookAsync(bookId, ct).ConfigureAwait(false);
        _logger.LogInformation("Got book detail of book {bookId} ({title} #{number})", bookId, book.Series, book.Number);
        return await _pageCache.GetPageAsync(bookId, book.FileName, pageNumber, ct).ConfigureAwait(false);
    }

    public async Task<BookPageStatus> GetPageStatusAsync(BookId bookId, CancellationToken ct)
    {
        var book = await this.GetBookAsync(bookId, ct).ConfigureAwait(false);
        var status = await _pageCache.GetStatusAsync(bookId, ct).ConfigureAwait(false);
        await this.SyncPageCountAsync(book, status, ct).ConfigureAwait(false);
        return status;
    }

    public async Task<BookPages> GetPagesAsync(BookId bookId, CancellationToken ct)
    {
        var book = await this.GetBookAsync(bookId, ct).ConfigureAwait(false);
        var pages = await _pageCache.GetPagesAsync(bookId, ct).ConfigureAwait(false);
        await this.SyncPageCountAsync(book, pages.Status, ct).ConfigureAwait(false);
        return pages;
    }

    public async Task<BookPageStatus> RequestPrepareAsync(BookId bookId, CancellationToken ct)
    {
        var book = await this.GetBookAsync(bookId, ct).ConfigureAwait(false);
        var status = await _pageCache.GetStatusAsync(bookId, ct).ConfigureAwait(false);
        if (status.State == PageState.Failed)
        {
            await _pageCache.ClearFailureAsync(bookId, ct).ConfigureAwait(false);
            return BookPageStatus.Pending;
        }
        await this.SyncPageCountAsync(book, status, ct).ConfigureAwait(false);
        return status;
    }

    public async Task PrepareBookAsync(BookId bookId, CancellationToken ct)
    {
        var book = await this.GetBookAsync(bookId, ct).ConfigureAwait(false);
        var pageCount = await _pageCache.PrepareAsync(bookId, book.FileName, ct).ConfigureAwait(false);
        await this.SyncPageCountAsync(book, BookPageStatus.Ready(pageCount), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The extracted pages are the truth about how many pages a book has; the stored count came from the archive
    /// listing at ingest and can be off by one (an archive without <c>ComicInfo.xml</c>). Modified is left alone: this is not reading activity.
    /// </summary>
    private async Task SyncPageCountAsync(Book book, BookPageStatus status, CancellationToken ct)
    {
        if (status.PageCount is not { } pageCount || book.PageCount == pageCount)
        {
            return;
        }

        using var context = await this.GetContextAsync(ct).ConfigureAwait(false);
        var tracked = await this.GetBookByIdAsync(book.Id, context, ct).ConfigureAwait(false);
        _logger.LogInformation("Book {bookId} has {actual} pages, not {recorded}. Correcting.", book.Id, pageCount, tracked.PageCount);
        tracked.PageCount = pageCount;
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
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
