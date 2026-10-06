using System.Text.Json;

namespace ShortBox.Services;

/// <summary>A page ready to serve: the image bytes and their real content type.</summary>
public sealed record PageImage(Stream Content, string ContentType);

/// <summary>A page to store, named relative to its book's folder.</summary>
public sealed record PageUpload(string Blob, string ContentType, Stream Content);

/// <param name="Index">Zero-based position in reading order.</param>
/// <param name="Blob">Name of the stored page, relative to the book's folder.</param>
/// <param name="Entry">Full path of the image inside the archive.</param>
/// <param name="Width">Pixel width, or null when the image header could not be read.</param>
public sealed record ManifestPage(int Index, string Blob, string Entry, string ContentType, int? Width, int? Height);

/// <summary>
/// What an extracted book contains. It is stored after every page, so its presence is the "ready" marker:
/// a folder without a manifest is a partial extraction and is never served.
/// </summary>
public sealed record PageManifest(int Version, IReadOnlyList<ManifestPage> Pages)
{
    public const int CurrentVersion = 1;
    public const string BlobName = "manifest.json";

    public int PageCount => Pages.Count;

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Null when the text is not a usable manifest of the current version.</summary>
    public static PageManifest? TryParse(string json)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<PageManifest>(json, JsonOptions);
            return manifest is { Version: CurrentVersion, Pages.Count: > 0 } ? manifest : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public static class PageExtraction
{
    /// <summary>
    /// Stores every page of an archive through <paramref name="upload"/> and returns the manifest describing them.
    /// Nothing is marked ready here: the caller stores the manifest once this returns.
    /// </summary>
    /// <exception cref="InvalidDataException">The archive has no page images.</exception>
    public static async Task<PageManifest> ExtractAsync(
        IAsyncEnumerable<IPageEntry> pages,
        Func<PageUpload, CancellationToken, Task> upload,
        CancellationToken cancellationToken)
    {
        var manifestPages = new List<ManifestPage>();
        await foreach (var page in pages.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            using (var entry = page.Open())
            {
                await entry.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            var info = await TryIdentifyAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;

            var index = manifestPages.Count;
            var blob = $"{index:D4}{Path.GetExtension(page.Name).ToLowerInvariant()}";
            var contentType = PageImageExtensions.GetContentType(page.Name);
            await upload(new PageUpload(blob, contentType, buffer), cancellationToken).ConfigureAwait(false);
            manifestPages.Add(new ManifestPage(index, blob, page.Name, contentType, info?.Width, info?.Height));
        }

        return manifestPages.Count > 0
            ? new PageManifest(PageManifest.CurrentVersion, manifestPages)
            : throw new InvalidDataException("The archive contains no page images.");
    }

    private static async Task<ImageInfo?> TryIdentifyAsync(MemoryStream image, CancellationToken cancellationToken)
    {
        try
        {
            image.Position = 0;
            return await Image.IdentifyAsync(image, cancellationToken).ConfigureAwait(false);
        }
        catch (ImageFormatException)
        {
            return null;
        }
    }
}
