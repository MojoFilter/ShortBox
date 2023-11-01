using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShortBox.Api.Data;
using ShortBox.Services;

namespace ShortBox.Api;


public interface IBookStore {
    Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken);
    Task<byte[]> GetSeriesCoverAsync(string seriesName, CancellationToken cancellationToken);
}

internal class FolderBookStoreOptions { 
    public string Path { get; set; } = string.Empty;
}

internal class FolderBookStore : IFolderBookStore, IBookStore {

    public FolderBookStore(
        IOptions<FolderBookStoreOptions> options,
        ShortBoxContext context) {
        _bookFolder = options.Value.Path;
        _context = context;
    }

    public async Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken) =>
        (await _context.Books
                .GroupBy(b => b.Series)
                .Select(group => new Series(group.Key ?? string.Empty))
                .ToListAsync()).AsEnumerable();

    public async Task<byte[]> GetSeriesCoverAsync(string seriesName, CancellationToken cancellationToken)
    {
        var firstBook = await _context
            .Books
            .Where(b => string.Equals(b.Series, seriesName))
            .FirstAsync();
        return await File.ReadAllBytesAsync(this.CoverFilePath(firstBook));
    }
            
    public async Task CacheCoverAsync(Book book, Func<Stream?> getCoverStream, CancellationToken ct) {
        using var coverStream = getCoverStream();
        if (coverStream is not null) {
            var fileName = this.CoverFilePath(book);
            using var file = File.Create(fileName);
            await coverStream.CopyToAsync(file, ct);
        }
    }

    public Task<bool> ExistsAsync(string file, CancellationToken cancellationToken) =>
        _context.Books.AnyAsync(b => b.FileName == Path.GetFileName(file), cancellationToken);

    public Task<IEnumerable<string>> GetRarFilesAsync(CancellationToken ct) => this.GetFilesAsync("*.cbr", ct);

    public Task<IEnumerable<string>> GetZipFilesAsync(CancellationToken ct) => this.GetFilesAsync("*.cbz", ct);

    private Task<IEnumerable<string>> GetFilesAsync(string extension, CancellationToken cancellationToken) =>
        Task.Run(() => Directory.GetFiles(_bookFolder, extension).AsEnumerable(), cancellationToken);

    public async Task AddBooksAsync(IEnumerable<Book> books, CancellationToken ct)
    {
        await _context.Books.AddRangeAsync(books);
        await _context.SaveChangesAsync();
    }

    private string CoverFilePath(Book book) => Path.Combine(_bookFolder, "covers", $"{book.FileName}.jpg");
   

    private string _bookFolder;
    private ShortBoxContext _context;
    //private IComicFolderScanner _scanner;
}