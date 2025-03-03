using Microsoft.EntityFrameworkCore;
using ShortBox.Services;

namespace ShortBox.DataAccess;

public interface IBookStore
{
    Task<byte[]> GetBookCoverAsync(BookId bookId, int? height, CancellationToken ct);
    IAsyncEnumerable<Book> GetRecentBooksAsync(CancellationToken cancellationToken);
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
