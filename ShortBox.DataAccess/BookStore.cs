using ShortBox.Services;

namespace ShortBox.DataAccess;

public interface IBookStore
{
    Task<byte[]> GetBookCoverAsync(BookId bookId, int? height, CancellationToken ct);
    IAsyncEnumerable<Book> GetRecentBooksAsync(CancellationToken cancellationToken);
    Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken);
    Task<Book> GetBookAsync(BookId bookId, CancellationToken ct);
    Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken ct);
    Task<IEnumerable<Book>> GetSeriesArchiveAsync(string seriesName, CancellationToken ct);
}

internal class BookStore(
    IDbContextFactory<ShortBoxContext> contextFactory,
    IBookCoverFileBusiness coverBusiness,
    IImageBusiness imageBusiness) : IBookStore
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
        var book = await this.GetBookByIdAsync(bookId, context, ct).ConfigureAwait(false); ;
        return await this.GetCoverFileAsync(book, height, ct);
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

    private async Task<byte[]> GetCoverFileAsync(Book book, int? height, CancellationToken ct)
    {
        using var inputStream = await _coverBusiness.GetCoverStreamAsync(book.Id, ct).ConfigureAwait(false);
        using var localStream = new MemoryStream();
        await inputStream.CopyToAsync(localStream, ct).ConfigureAwait(false);
        localStream.Position = 0;
        using var image = await _imageBusiness.LoadImageAsync(localStream, height, ct);
        using var reader = new BinaryReader(image);
        return reader.ReadBytes((int)image.Length);
    }

    private IDbContextFactory<ShortBoxContext> _contextFactory = contextFactory;
    private IBookCoverFileBusiness _coverBusiness = coverBusiness;
    private IImageBusiness _imageBusiness = imageBusiness;
}
