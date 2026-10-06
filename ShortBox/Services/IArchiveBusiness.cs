namespace ShortBox.Services;

public interface IPageEntry
{
    int Index { get; }
    string Name { get; }
    Stream Open();
}

public interface IArchiveBusiness
{
    IAsyncEnumerable<IPageEntry> ExtractArchivePagesAsync(
        string archiveFilename,
        Stream archive,
        CancellationToken cancellationToken);
}

interface IArchiveExtractor
{
    IAsyncEnumerable<IPageEntry> ExtractPagesAsync(
        Stream archive,
        CancellationToken cancellationToken);
}

internal class PageEntry(int index, string name, Func<Stream> open) : IPageEntry
{
    public int Index => index;

    public string Name => name;

    public Stream Open() => open();
}

internal static class ServiceKeys
{
    public const string ZipExtractor = "ZipExtractor";
    public const string RarExtractor = "RarExtractor";
}

internal class ArchiveBusiness(
    [FromKeyedServices(ServiceKeys.ZipExtractor)]
    IArchiveExtractor zipExtractor,
    [FromKeyedServices(ServiceKeys.RarExtractor)]
    IArchiveExtractor rarExtractor) 
    : IArchiveBusiness
{

    public IAsyncEnumerable<IPageEntry> ExtractArchivePagesAsync(string archiveFilename, Stream archive, CancellationToken cancellationToken)
    {
        var extractor = this.IsZipArchive(archiveFilename) ? _zipExtractor : _rarExtractor;
        return extractor.ExtractPagesAsync(archive, cancellationToken);
    }

    private bool IsZipArchive(string archiveFilename) => archiveFilename.EndsWith(".cbz", StringComparison.OrdinalIgnoreCase);

    private readonly IArchiveExtractor _zipExtractor = zipExtractor;
    private readonly IArchiveExtractor _rarExtractor = rarExtractor;
}

// Both extractors hand out entries that are only readable until the enumeration ends: the underlying archive
// is disposed then. The caller still owns the stream it passed in.
internal class ZipExtractor : IArchiveExtractor
{
    public IAsyncEnumerable<IPageEntry> ExtractPagesAsync(Stream archive, CancellationToken cancellationToken) =>
        Enumerate(archive, cancellationToken).ToAsyncEnumerable();

    private static IEnumerable<IPageEntry> Enumerate(Stream archive, CancellationToken cancellationToken)
    {
        using var zipArchive = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
        var index = 0;
        foreach (var entry in zipArchive.Entries.InReadingOrder(e => e.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new PageEntry(index++, entry.FullName, entry.Open);
        }
    }
}

internal class RarExtractor : IArchiveExtractor
{
    public IAsyncEnumerable<IPageEntry> ExtractPagesAsync(Stream archive, CancellationToken cancellationToken) =>
        Enumerate(archive, cancellationToken).ToAsyncEnumerable();

    private static IEnumerable<IPageEntry> Enumerate(Stream archive, CancellationToken cancellationToken)
    {
        using var rar = new RarArchive(archive);
        var index = 0;
        foreach (var entry in rar.Entries.InReadingOrder(e => e.Name))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new PageEntry(index++, entry.Name, () => entry.Open());
        }
    }
}
