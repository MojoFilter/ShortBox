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

internal abstract class Extractor
{ 
    protected static readonly HashSet<string> PageImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif"
    };

}

internal class ZipExtractor : Extractor, IArchiveExtractor
{
    public IAsyncEnumerable<IPageEntry> ExtractPagesAsync(Stream archive, CancellationToken cancellationToken)
    {
        var zipArchive = new ZipArchive(archive, ZipArchiveMode.Read);
        return zipArchive.Entries
            .Where(e => PageImageExtensions.Contains(Path.GetExtension(e.Name)))
            .OrderBy(e => e.Name)
            .Select((e, i) => new PageEntry(i, e.Name, e.Open))
            .ToAsyncEnumerable();
    }

}

internal class RarExtractor : Extractor, IArchiveExtractor
{
    public IAsyncEnumerable<IPageEntry> ExtractPagesAsync(Stream archive, CancellationToken cancellationToken)
    {
        var rar = new RarArchive(archive);
        return rar.Entries
            .Where(e => PageImageExtensions.Contains(Path.GetExtension(e.Name)))
            .OrderBy(e => e.Name)
            .Select((e, i) => new PageEntry(i, e.Name, () => e.Open()))
            .ToAsyncEnumerable();
    }
}
