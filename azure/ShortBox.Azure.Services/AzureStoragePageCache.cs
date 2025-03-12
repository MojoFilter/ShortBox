using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using ShortBox.Api.Data;
using ShortBox.Services;

namespace ShortBox.Azure.Services;

internal class AzureStoragePageCache(
    BlobServiceClient blobServiceClient,
    IArchiveStore archiveStore,
    IArchiveBusiness archiveBusiness,
    ICoverStore coverStore,
    ICoverBusiness coverBusiness,
    ILogger<AzureStoragePageCache> log)
    : IPageCache
{
    public async Task<Stream> GetPageAsync(BookId bookId, string bookFileName, int pageNumber, CancellationToken ct)
    {
        var folder = $"{bookId.Value}/";        
        var containerClient = _serviceClient.GetBlobContainerClient(PagesContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct).ConfigureAwait(false);
        _log.LogInformation("Checking files in {folder}", folder);

        var folderBlobs = await this.GetBlobsInVirtualFolderAsync(containerClient, folder, ct).ConfigureAwait(false);
        
        if (folderBlobs.Count == 0)
        {
            _log.LogInformation("Archive {bookId} not found. Attempting to extract.", bookId);
            await this.ExtractArchiveToBlobStorageAsync(containerClient, bookId, bookFileName, ct);
            folderBlobs = await this.GetBlobsInVirtualFolderAsync(containerClient, folder, ct).ConfigureAwait(false);
            if (folderBlobs.Count == 0)
            {
                _log.LogWarning("Archive {bookId} not found after extraction.", bookId);
                throw new KeyNotFoundException($"Archive {bookId} not found.");
            }
        }

        if (pageNumber < 0 || pageNumber >= folderBlobs.Count)
        {
            _log.LogWarning("Archive {bookId} not found after extraction.", bookId);
            throw new KeyNotFoundException($"Archive {bookId} not found.");
        }

        var blob = folderBlobs[pageNumber];
        _log.LogInformation("Downloading page {pageNumber} of {bookId} from blob {blobName}", pageNumber, bookId, blob.Name);
        var blobClient = containerClient.GetBlobClient(blob.Name);
        var downloadResponse = await blobClient.DownloadAsync(ct).ConfigureAwait(false);
        _log.LogInformation("Page {pageNumber} of {bookId} downloaded from blob {blobName}: {contentLength} bytes", pageNumber, bookId, blob.Name, downloadResponse.Value.Details.ContentLength);
        return downloadResponse.Value.Content;
    }

    public async Task<Stream> GetCoverAsync(BookId bookId, string bookFileName, CancellationToken ct)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(CoversContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct).ConfigureAwait(false);
        var blobClient = containerClient.GetBlobClient(bookId.ToString());
        var existsResponse = await blobClient.ExistsAsync(ct).ConfigureAwait(false);
        if (existsResponse.Value)
        {
            return await blobClient.OpenReadAsync(new(true), ct).ConfigureAwait(false);
        }
        return await this.CacheCoverAsync(bookId, bookFileName, ct).ConfigureAwait(false);
    }

    private ValueTask<List<BlobItem>> GetBlobsInVirtualFolderAsync(
        BlobContainerClient client,
        string folder,
        CancellationToken cancellationToken) =>
        client.GetBlobsAsync(prefix: folder, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

    private async Task ExtractArchiveToBlobStorageAsync(BlobContainerClient containerClient, BookId bookId, string bookFileName, CancellationToken cancellationToken)
    {
        var archive = await _archiveStore.GetArchiveAsync(bookFileName, cancellationToken).ConfigureAwait(false);
        var pages = _archiveBusiness.ExtractArchivePagesAsync(bookFileName, archive, cancellationToken);
        await foreach (var page in pages)
        {
            var blobClient = containerClient.GetBlobClient($"{bookId.Value}/{page.Name}");
            using var stream = page.Open();
            try
            {
                await blobClient.UploadAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                _log.LogWarning("Blob {blobName} already exists. Skipping upload.", blobClient.Name);
            }
        }
    }

    private async Task<Stream> CacheCoverAsync(BookId bookId, string bookFileName, CancellationToken ct)
    {
        using var fullCover = await _coverStore.GetCoverAsync(bookFileName, ct).ConfigureAwait(false);
        using var thumbnailStream = await _coverBusiness.CreateThumbnailAsync(fullCover, ct).ConfigureAwait(false);
        var stream = new MemoryStream();
        await thumbnailStream.CopyToAsync(stream, ct).ConfigureAwait(false);
        stream.Position = 0;
        var containerClient = _serviceClient.GetBlobContainerClient(CoversContainerName);
        var blobClient = containerClient.GetBlobClient(bookId.ToString());
        await blobClient.UploadAsync(stream, true, ct).ConfigureAwait(false);
        stream.Position = 0;
        return stream;
    }

    private readonly IArchiveBusiness _archiveBusiness = archiveBusiness;
    private readonly IArchiveStore _archiveStore = archiveStore;
    private readonly ICoverBusiness _coverBusiness = coverBusiness;
    private readonly ICoverStore _coverStore = coverStore;
    private readonly BlobServiceClient _serviceClient = blobServiceClient;
    private readonly ILogger<AzureStoragePageCache> _log = log;

    private const string PagesContainerName = "pages";
    private const string CoversContainerName = "covers";
}
