using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShortBox.Api.Data;
using ShortBox.Services;

namespace ShortBox.Api;


public interface IBookStore {
    Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken);
    Task<byte[]> GetBookCoverAsync(int bookId, int? height, CancellationToken ct);
    Task<byte[]> GetBookPageAsync(int bookId, int pageNumber, CancellationToken ct);
    Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken ct);
    Task<byte[]> GetSeriesCoverAsync(string seriesName, int? height, CancellationToken cancellationToken);
    Task MarkPageAsync(int bookId, int pageNumber, CancellationToken ct);
}

internal class FolderBookStoreOptions { 
    public string Path { get; set; } = string.Empty;
}

internal class FolderBookStore : IFolderBookStore, IBookStore {

    public FolderBookStore(
        IOptions<FolderBookStoreOptions> options,
        ShortBoxContext context,
        IComicFileReader reader,
        IImageBusiness imageBusiness) {
        _bookFolder = options.Value.Path;
        _context = context;
        _reader = reader;
        _imageBusiness = imageBusiness;
    }

    public async Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken) =>
        (await _context.Books
                .GroupBy(b => b.Series)
                .Select(group => new Series(group.Key ?? string.Empty))
                .ToListAsync()).AsEnumerable();

    public async Task<byte[]> GetSeriesCoverAsync(string seriesName, int? height, CancellationToken cancellationToken)
    {
        var firstBook = await _context
            .Books
            .Where(b => string.Equals(b.Series, seriesName))
            .FirstAsync();
        return await this.GetCoverFileAsync(firstBook, height, cancellationToken);
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


    public async Task<byte[]> GetBookCoverAsync(int bookId, int? height, CancellationToken ct)
    {
        var book = await this.GetBookByIdAsync(bookId, ct);
        return await this.GetCoverFileAsync(book, height, ct);
    }

    public async Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken ct) =>
        (await _context.Books.Where(b => string.Equals(b.Series, seriesName)).ToListAsync()).AsEnumerable();

    private async Task<Book> GetBookByIdAsync(int bookId, CancellationToken ct) =>
        await _context.Books.FirstOrDefaultAsync(b => b.Id == bookId, ct) 
            ?? throw new KeyNotFoundException($"Book not found with ID {bookId}");

    private async Task<byte[]> GetCoverFileAsync(Book book, int? height, CancellationToken ct)
    {
        using var inputStream = File.OpenRead(this.CoverFilePath(book));
        using var image = await _imageBusiness.LoadImageAsync(inputStream, height, ct);
        using var reader = new BinaryReader(image);
        return reader.ReadBytes((int)image.Length);
    }

    public async Task<byte[]> GetBookPageAsync(int bookId, int pageNumber, CancellationToken ct) =>
        await _context.Books.FindAsync(bookId) switch
        {
            Book book => await _reader.GetBookPageAsync(ArchivePath(book), pageNumber, ct).ToByteArrayAsync(ct),
            _ => throw new KeyNotFoundException("Book ID not found")
        };

    private string ArchivePath(Book book) => Path.Combine(_bookFolder, book.FileName);

    private string CoverFilePath(Book book) => Path.Combine(_bookFolder, "covers", $"{book.FileName}.jpg");

    public Task MarkPageAsync(int bookId, int pageNumber, CancellationToken ct) =>
        _context.Books
                .Where(b => b.Id == bookId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.CurrentPage, pageNumber));
    

    private string _bookFolder;
    private ShortBoxContext _context;
    private readonly IComicFileReader _reader;
    private readonly IImageBusiness _imageBusiness;
    //private IComicFolderScanner _scanner;
}