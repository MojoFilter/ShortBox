using ShortBox.Services;

namespace ShortBox.DataAccess;

internal class BookRepo (IDbContextFactory<ShortBoxContext> contextFactory) : IBookRepo
{
    public async Task<string> GetFileNameAsync(BookId bookId, CancellationToken ct)
    {
        using var context = await this.GetContextAsync(ct).ConfigureAwait(false);
        var book = await this.GetBookByIdAsync(bookId, context, ct).ConfigureAwait(false);
        return book.FileName;
    }

    private async Task<Book> GetBookByIdAsync(BookId bookId, ShortBoxContext context, CancellationToken ct) =>
        await context.Books.FirstOrDefaultAsync(b => b.Id == bookId, ct)
            ?? throw new KeyNotFoundException($"Book not found with ID {bookId}");

    private Task<ShortBoxContext> GetContextAsync(CancellationToken cancellationToken) =>
        _contextFactory.CreateDbContextAsync(cancellationToken);

    private IDbContextFactory<ShortBoxContext> _contextFactory = contextFactory;
}
