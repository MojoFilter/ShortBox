using ShortBox.Services;

namespace ShortBox.DataAccess;

internal class BookCatalog(IDbContextFactory<ShortBoxContext> contextFactory) : IBookCatalog
{
    public async Task<IReadOnlySet<string>> GetFileNamesAsync(CancellationToken ct)
    {
        using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var names = await context.Books.Select(b => b.FileName).ToListAsync(ct).ConfigureAwait(false);
        return new HashSet<string>(names, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<Book>> AddBooksAsync(IEnumerable<Book> books, CancellationToken ct)
    {
        using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var candidates = books.ToList();
        var names = candidates.Select(b => b.FileName).ToList();
        var existing = await context.Books
            .Where(b => names.Contains(b.FileName))
            .Select(b => b.FileName)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var added = candidates.Where(b => !existing.Contains(b.FileName)).ToList();
        foreach (var book in added)
        {
            // New books all carry BookId.Empty until saved, so they can't be tracked side by side.
            context.Books.Add(book);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            context.Entry(book).State = EntityState.Detached;
        }
        return added;
    }

    private readonly IDbContextFactory<ShortBoxContext> _contextFactory = contextFactory;
}
