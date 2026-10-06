using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using ShortBox.Api.Data;
using ShortBox.Services;
using System.Collections.Concurrent;

namespace ShortBox.Azure.Services;

internal class AzureStoragePageCache(
    BlobServiceClient blobServiceClient,
    IPageBlobs pageBlobs,
    IArchiveStore archiveStore,
    IArchiveBusiness archiveBusiness,
    ICoverStore coverStore,
    ICoverBusiness coverBusiness,
    ILogger<AzureStoragePageCache> log)
    : IPageCache
{
    public async Task<PageImage> GetPageAsync(BookId bookId, string bookFileName, int pageNumber, CancellationToken ct)
    {
        var manifest = await this.GetManifestAsync(bookId, bookFileName, ct).ConfigureAwait(false);
        if (pageNumber < 0 || pageNumber >= manifest.PageCount)
        {
            _log.LogWarning("Page {pageNumber} requested of book {bookId}, which has {pageCount} pages", pageNumber, bookId.Value, manifest.PageCount);
            throw new KeyNotFoundException($"Book {bookId.Value} has no page {pageNumber} (it has {manifest.PageCount}).");
        }

        var page = manifest.Pages[pageNumber];
        _log.LogInformation("Downloading page {pageNumber} of {bookId} from blob {blobName}", pageNumber, bookId.Value, page.Blob);
        return await _pageBlobs.OpenPageAsync(bookId, page, ct).ConfigureAwait(false);
    }

    /// <summary>The manifest of a fully extracted book, extracting it first if it is not ready.</summary>
    private async Task<PageManifest> GetManifestAsync(BookId bookId, string bookFileName, CancellationToken ct)
    {
        var manifest = await _pageBlobs.ReadManifestAsync(bookId, ct).ConfigureAwait(false);
        if (manifest is not null)
        {
            return manifest;
        }

        _log.LogInformation("Book {bookId} is not extracted. Extracting.", bookId.Value);
        // Requests for the same book share one extraction, which belongs to none of them: a caller that
        // gives up (cancels) stops waiting but must not abort the work the others are waiting on.
        var extraction = InFlight.GetOrAdd(bookId.Value, _ => new(() => this.ExtractAsync(bookId, bookFileName)));
        return await extraction.Value.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task<PageManifest> ExtractAsync(BookId bookId, string bookFileName)
    {
        try
        {
            using var timeout = new CancellationTokenSource(ExtractionTimeout);
            var ct = timeout.Token;

            // Another request may have finished this book between our manifest check and registering here.
            if (await _pageBlobs.ReadManifestAsync(bookId, ct).ConfigureAwait(false) is { } ready)
            {
                return ready;
            }

            await using var archive = await _archiveStore.GetArchiveAsync(bookFileName, ct).ConfigureAwait(false);
            var pages = _archiveBusiness.ExtractArchivePagesAsync(bookFileName, archive, ct);
            var manifest = await PageExtraction.ExtractAsync(pages, (page, token) => _pageBlobs.WritePageAsync(bookId, page, token), ct).ConfigureAwait(false);

            // The manifest goes last: it is what marks the book ready.
            await _pageBlobs.WriteManifestAsync(bookId, manifest, ct).ConfigureAwait(false);
            await _pageBlobs.DeleteUnlistedAsync(bookId, manifest, ct).ConfigureAwait(false);
            _log.LogInformation("Extracted {pageCount} pages of book {bookId}", manifest.PageCount, bookId.Value);
            return manifest;
        }
        finally
        {
            InFlight.TryRemove(bookId.Value, out _);
        }
    }

    public async Task<Stream> GetCoverAsync(BookId bookId, string bookFileName, CancellationToken ct)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(CoversContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct).ConfigureAwait(false);
        var blobClient = containerClient.GetBlobClient(bookId.Value.ToString());
        var existsResponse = await blobClient.ExistsAsync(ct).ConfigureAwait(false);
        if (existsResponse.Value)
        {
            _log.LogInformation("Cover for {bookId} found. Downloading cover.", bookId.Value);
            return await blobClient.OpenReadAsync(new(true), ct).ConfigureAwait(false);
        }
        _log.LogInformation("Cover for {bookId} not found. Caching cover.", bookId.Value);
        return await this.CacheCoverAsync(bookId, bookFileName, ct).ConfigureAwait(false);
    }

    private async Task<Stream> CacheCoverAsync(BookId bookId, string bookFileName, CancellationToken ct)
    {
        using var fullCover = await _coverStore.GetCoverAsync(bookFileName, ct).ConfigureAwait(false);
        using var thumbnailStream = await _coverBusiness.CreateThumbnailAsync(fullCover, ct).ConfigureAwait(false);
        var stream = new MemoryStream();
        await thumbnailStream.CopyToAsync(stream, ct).ConfigureAwait(false);
        stream.Position = 0;
        var containerClient = _serviceClient.GetBlobContainerClient(CoversContainerName);
        var blobClient = containerClient.GetBlobClient(bookId.Value.ToString());
        await blobClient.UploadAsync(stream, true, ct).ConfigureAwait(false);
        stream.Position = 0;
        return stream;
    }

    public async Task DecacheBooksAsync(IEnumerable<Book> booksToDecache, CancellationToken cancellationToken)
    {
        _log.LogInformation("Decaching books {books}", booksToDecache.Select(b => $"{b.Series} #{b.Number}"));
        var ids = booksToDecache.Select(b => b.Id);
        await this.DeletePagesAsync(ids, cancellationToken).ConfigureAwait(false);
        await this.DeleteCoversAsync(booksToDecache, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<BookId>> GetCachedBookIdsAsync(CancellationToken cancellationToken)
    {
        var folders = await this.ListPageFoldersAsync(cancellationToken).ConfigureAwait(false);
        return from folder in folders
               let trimmed = folder.TrimEnd('/')
               let parsed = int.TryParse(trimmed, out var id) ? (int?)id : null
               where parsed is not null
               select new BookId(parsed.Value);
    }

    private async Task<IEnumerable<string>> ListPageFoldersAsync(CancellationToken cancellationToken)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(PagesContainerName);
        var folders = new List<string>();
        var blobs = containerClient.GetBlobsByHierarchyAsync(delimiter: "/", cancellationToken: cancellationToken);
        await foreach (var blob in blobs)
        {
            if (blob.Prefix is not null)
            {
                folders.Add(blob.Prefix);
            }
        }
        return folders;
    }

    private async Task DeleteCoversAsync(IEnumerable<Book> booksToDecache, CancellationToken cancellationToken)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(CoversContainerName);
        var blobNames = booksToDecache.Select(b => $"{b.FileName}.jpg");
        foreach (var blobName in blobNames)
        {
            await containerClient.DeleteBlobIfExistsAsync(blobName, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DeletePagesAsync(IEnumerable<BookId> bookIds, CancellationToken cancellationToken)
    {
        foreach (var bookId in bookIds)
        {
            await _pageBlobs.DeleteBookAsync(bookId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Extractions under way in this host, by book id.</summary>
    private static readonly ConcurrentDictionary<int, Lazy<Task<PageManifest>>> InFlight = new();
    private static readonly TimeSpan ExtractionTimeout = TimeSpan.FromMinutes(10);

    private readonly IPageBlobs _pageBlobs = pageBlobs;
    private readonly IArchiveBusiness _archiveBusiness = archiveBusiness;
    private readonly IArchiveStore _archiveStore = archiveStore;
    private readonly ICoverBusiness _coverBusiness = coverBusiness;
    private readonly ICoverStore _coverStore = coverStore;
    private readonly BlobServiceClient _serviceClient = blobServiceClient;
    private readonly ILogger<AzureStoragePageCache> _log = log;

    private const string PagesContainerName = AzurePageBlobs.ContainerName;
    private const string CoversContainerName = "covers";
}
