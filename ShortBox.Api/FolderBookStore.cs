﻿using MarvelApiClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using ShortBox.Api.Data;
using ShortBox.Services;

namespace ShortBox.Api;


public interface IBookStore {
    Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken);
    Task<Book> GetBookAsync(BookId bookId, CancellationToken ct);
    Task<byte[]> GetBookCoverAsync(BookId bookId, int? height, CancellationToken ct);
    Task<byte[]> GetBookPageAsync(BookId bookId, int pageNumber, CancellationToken ct);
    Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken ct);
    Task<List<Book>> GetRecentBooksAsync(CancellationToken ct);
    Task<IEnumerable<Book>> GetSeriesArchiveAsync(string seriesName, CancellationToken ct);
    Task<byte[]> GetSeriesCoverAsync(string seriesName, int? height, CancellationToken cancellationToken);
    Task MarkPageAsync(BookId bookId, int pageNumber, CancellationToken ct);
    Task<IEnumerable<PullListEntry>> UpdatePullListAsync(IEnumerable<Comic> recentComics, CancellationToken ct);
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
                .WhereUnread()
                .GroupBy(b => b.Series)
                .OrderByDescending(g => g.Select(b=>b.Modified).Max())
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
            
    public async Task CacheCoverAsync(Book book, Func<Task<Stream?>> getCoverStream, CancellationToken ct) {
        using var coverStream = await getCoverStream();
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

    public Task<Book> GetBookAsync(BookId bookId, CancellationToken ct) => this.GetBookByIdAsync(bookId, ct);

    public async Task<byte[]> GetBookCoverAsync(BookId bookId, int? height, CancellationToken ct)
    {
        var book = await this.GetBookByIdAsync(bookId, ct);
        return await this.GetCoverFileAsync(book, height, ct);
    }

    public Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken ct) => this.GetIssuesAsync(seriesName, true, ct);


    public Task<IEnumerable<Book>> GetSeriesArchiveAsync(string seriesName, CancellationToken ct) => this.GetIssuesAsync(seriesName, false, ct);

    private async Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, bool unread, CancellationToken ct) =>
        (await _context.Books
            .Where(b => string.Equals(b.Series, seriesName))
            .WhereUnread(unread)
            .OrderBy(b=>b.Number)
            .ToListAsync(ct)).AsEnumerable();

    private async Task<Book> GetBookByIdAsync(BookId bookId, CancellationToken ct) =>
        await _context.Books.FirstOrDefaultAsync(b => b.Id == bookId, ct) 
            ?? throw new KeyNotFoundException($"Book not found with ID {bookId}");

    private async Task<byte[]> GetCoverFileAsync(Book book, int? height, CancellationToken ct)
    {
        using var inputStream = File.OpenRead(this.CoverFilePath(book));
        using var image = await _imageBusiness.LoadImageAsync(inputStream, height, ct);
        using var reader = new BinaryReader(image);
        return reader.ReadBytes((int)image.Length);
    }

    public async Task<byte[]> GetBookPageAsync(BookId bookId, int pageNumber, CancellationToken ct) =>
        await _context.Books.FindAsync(bookId) switch
        {
            Book book => await _reader.GetBookPageAsync(ArchivePath(book), pageNumber, ct).ToByteArrayAsync(ct),
            _ => throw new KeyNotFoundException("Book ID not found")
        };

    public Task<List<Book>> GetRecentBooksAsync(CancellationToken ct) =>
        _context.Books.WhereUnread()
                      .OrderByDescending(b => b.Modified).ToListAsync(ct);

    public async Task<IEnumerable<PullListEntry>> UpdatePullListAsync(IEnumerable<Comic> recentComics, CancellationToken ct)
    {
        foreach (var comic in recentComics)
        {
            var keyValues = new object?[] { new PullListEntryId(comic.Id) };
            if (await _context.PullList.FindAsync(keyValues, ct).ConfigureAwait(false) is null)
            {
                await _context.PullList.AddAsync(new()
                {
                    Id = new(comic.Id),
                    Description = comic.Description,
                    IssueNumber = comic.IssueNumber,
                    Title = comic.Title,
                    ThumbnailUri = comic.ThumbnailUri
                }).ConfigureAwait(false);
            }
        }

        var pendingInserts = _context.ChangeTracker
                                     .Entries()
                                     .Where(e => e.State == EntityState.Added)
                                     .Select(e => e.Entity)
                                     .OfType<PullListEntry>()
                                     .ToList();

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return pendingInserts;
    }
        
    private static readonly DateTime tooOld = new(2000, 1, 1);

    public async Task RepairMissingDatesAsync(CancellationToken ct)
    {
        var missing = await _context.Books.Where(b => b.Added < tooOld).ToListAsync(ct);
        foreach (var book in missing)
        {
            book.Added = File.GetLastWriteTime(this.ArchivePath(book));
            _context.Update(book);
        }
        await _context.SaveChangesAsync(ct);
    }

    private string ArchivePath(Book book) => Path.Combine(_bookFolder, book.FileName);

    private string CoverFilePath(Book book) => Path.Combine(_bookFolder, "covers", $"{book.FileName}.jpg");

    public Task MarkPageAsync(BookId bookId, int pageNumber, CancellationToken ct) =>
        _context.Books
                .Where(b => b.Id == bookId)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(b => b.CurrentPage, pageNumber)
                     .SetProperty(b => b.Modified, DateTime.Now));


    private readonly ShortBoxContext _context;
    private readonly string _bookFolder;
    private readonly IComicFileReader _reader;
    private readonly IImageBusiness _imageBusiness;
}

internal static class EfExtensions
{
    private const double ReadThreshold = 0.91; // 91%

    public static IQueryable<Book> WhereUnread(this IQueryable<Book> books, bool unread = true) =>
        books.Where(b => (b.PageCount == null || (b.CurrentPage / (double)b.PageCount) < ReadThreshold) == unread);


    //EF doesn't like the extension method
    //private static bool BookIsUnread(Book b) => b.PageCount == null || (b.CurrentPage / (double)b.PageCount) < ReadThreshold;
}