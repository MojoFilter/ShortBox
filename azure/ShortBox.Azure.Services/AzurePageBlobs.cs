using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using ShortBox.Api.Data;
using ShortBox.Services;

namespace ShortBox.Azure.Services;

internal class AzurePageBlobs(BlobServiceClient blobServiceClient) : IPageBlobs
{
    public async Task<PageManifest?> ReadManifestAsync(BookId bookId, CancellationToken ct)
    {
        var container = await this.GetContainerAsync(ct).ConfigureAwait(false);
        try
        {
            var response = await container.GetBlobClient(BlobName(bookId, PageManifest.BlobName)).DownloadContentAsync(ct).ConfigureAwait(false);
            return PageManifest.TryParse(response.Value.Content.ToString());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task WritePageAsync(BookId bookId, PageUpload page, CancellationToken ct)
    {
        var container = await this.GetContainerAsync(ct).ConfigureAwait(false);
        var options = new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = page.ContentType } };
        await container.GetBlobClient(BlobName(bookId, page.Blob)).UploadAsync(page.Content, options, ct).ConfigureAwait(false);
    }

    public async Task WriteManifestAsync(BookId bookId, PageManifest manifest, CancellationToken ct)
    {
        var container = await this.GetContainerAsync(ct).ConfigureAwait(false);
        var options = new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } };
        await container.GetBlobClient(BlobName(bookId, PageManifest.BlobName))
                       .UploadAsync(BinaryData.FromString(manifest.ToJson()), options, ct)
                       .ConfigureAwait(false);
    }

    public async Task DeleteUnlistedAsync(BookId bookId, PageManifest manifest, CancellationToken ct)
    {
        var container = await this.GetContainerAsync(ct).ConfigureAwait(false);
        var keep = manifest.Pages.Select(p => BlobName(bookId, p.Blob)).Append(BlobName(bookId, PageManifest.BlobName)).ToHashSet();
        await foreach (var blob in container.GetBlobsAsync(prefix: Folder(bookId), cancellationToken: ct).ConfigureAwait(false))
        {
            if (!keep.Contains(blob.Name))
            {
                await container.DeleteBlobIfExistsAsync(blob.Name, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct).ConfigureAwait(false);
            }
        }
    }

    public async Task<PageImage> OpenPageAsync(BookId bookId, ManifestPage page, CancellationToken ct)
    {
        var container = await this.GetContainerAsync(ct).ConfigureAwait(false);
        var download = await container.GetBlobClient(BlobName(bookId, page.Blob)).DownloadAsync(ct).ConfigureAwait(false);
        return new PageImage(download.Value.Content, page.ContentType);
    }

    public async Task DeleteBookAsync(BookId bookId, CancellationToken ct)
    {
        var container = await this.GetContainerAsync(ct).ConfigureAwait(false);
        await container.DeleteBlobIfExistsAsync(BlobName(bookId, PageManifest.BlobName), DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct).ConfigureAwait(false);
        await foreach (var blob in container.GetBlobsAsync(prefix: Folder(bookId), cancellationToken: ct).ConfigureAwait(false))
        {
            await container.DeleteBlobIfExistsAsync(blob.Name, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct).ConfigureAwait(false);
        }
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken ct)
    {
        var container = _serviceClient.GetBlobContainerClient(ContainerName);
        if (!_containerExists)
        {
            await container.CreateIfNotExistsAsync(cancellationToken: ct).ConfigureAwait(false);
            _containerExists = true;
        }
        return container;
    }

    private static string Folder(BookId bookId) => $"{bookId.Value}/";

    private static string BlobName(BookId bookId, string blob) => $"{Folder(bookId)}{blob}";

    private readonly BlobServiceClient _serviceClient = blobServiceClient;

    private static volatile bool _containerExists;

    internal const string ContainerName = "pages";
}
