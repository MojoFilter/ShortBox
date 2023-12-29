namespace ShortBox.Services;

public interface IFolderBookStore
{
    Task<bool> ExistsAsync(string file, CancellationToken cancellationToken);
    Task<IEnumerable<string>> GetRarFilesAsync(CancellationToken cancellationToken);
    Task<IEnumerable<string>> GetZipFilesAsync(CancellationToken cancellationToken);
    Task CacheCoverAsync(Book book, Func<Task<Stream?>> getCoverStream, CancellationToken ct);
    Task AddBooksAsync(IEnumerable<Book> books, CancellationToken ct);
    Task RepairMissingDatesAsync(CancellationToken ct);
}

public record ScanResult(IEnumerable<string> NewFiles);

public interface IComicFolderScanner
{
    Task<ScanResult> ScanFolderAsync(CancellationToken cancellationToken);
}

internal class ComicFolderScanner : IComicFolderScanner 
{
    public ComicFolderScanner(IFolderBookStore store, IRarReader rarReader, IZipReader zipReader, IBookFactory bookFactory)
    {
        _store = store;
        _rarReader = rarReader;
        _zipReader = zipReader;
        _bookFactory = bookFactory;
    }

    public async Task<ScanResult> ScanFolderAsync(CancellationToken cancellationToken)
    {
        List<Book> newBooks = new List<Book>();
        foreach (var file in await _store.GetRarFilesAsync(cancellationToken))
        {
            if (!(await _store.ExistsAsync(file, cancellationToken)))
            {
                newBooks.Add(await InitBookRarAsync(file, cancellationToken));
            }
        }

        foreach (var file in await _store.GetZipFilesAsync(cancellationToken))
        {
            if (!(await _store.ExistsAsync(file, cancellationToken)))
            {
                newBooks.Add(await InitBookZipAsync(file, cancellationToken));
            }
        }
        await _store.AddBooksAsync(newBooks, cancellationToken);
        await _store.RepairMissingDatesAsync(cancellationToken);
        return new ScanResult(newBooks.Select(b => b.FileName).ToArray());
    }


    private Task<Book> InitBookRarAsync(string file, CancellationToken ct) =>
        InitBookAsync(file, _rarReader, ct);

    private Task<Book> InitBookZipAsync(string file, CancellationToken ct) =>
        InitBookAsync(file, _zipReader, ct);

    private async Task<Book> InitBookAsync(string fileName, IArchiveReader reader, CancellationToken ct)
    {
        var book = await _bookFactory.CreateFromInfoAsync(fileName, reader, ct);
        book.PageCount = book.PageCount ?? reader.GetPageCount(fileName);
        await _store.CacheCoverAsync(book, () => reader.OpenCoverAsync(fileName), ct);
        return book;
    }

    private readonly IFolderBookStore _store;
    private readonly IRarReader _rarReader;
    private readonly IZipReader _zipReader;
    private readonly IBookFactory _bookFactory;
}
