namespace ShortBox.Services;

public interface IBookCatalog
{
    Task<IReadOnlySet<string>> GetFileNamesAsync(CancellationToken ct);

    /// <summary>Adds books whose file name isn't in the catalog yet and returns the ones actually added (with ids).</summary>
    Task<IReadOnlyList<Book>> AddBooksAsync(IEnumerable<Book> books, CancellationToken ct);
}
