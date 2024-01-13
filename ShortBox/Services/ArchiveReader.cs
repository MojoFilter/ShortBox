using Aspose.Zip;
using Aspose.Zip.Rar;

namespace ShortBox.Services;

public interface IArchiveReader
{
    Task<Stream?> GetInfoStreamAsync(string fileName);
    int GetPageCount(string fileName);
    Task<Stream?> OpenCoverAsync(string fileName);
    Task<Stream?> OpenPageAsync(string fileName, int pageIndex);
}

internal abstract class ArchiveReader : IArchiveReader
{
    public static readonly string ComicInfoFileName = "ComicInfo.xml";

    public async Task<Stream?> GetInfoStreamAsync(string fileName)
    {
        using var archive = this.OpenArchive(fileName);
        var infoEntry = archive.FileEntries.FirstOrDefault(e => string.Equals(e.Name, ArchiveReader.ComicInfoFileName, StringComparison.InvariantCultureIgnoreCase));
        return await this.CopyEntryAsync(infoEntry);
    }

    public int GetPageCount(string fileName)
    {
        using var archive = this.OpenArchive(fileName);
        return archive.FileEntries.Count() - 1;
    }

    public Task<Stream?> OpenCoverAsync(string fileName) => this.OpenPageAsync(fileName, 0);

    public async Task<Stream?> OpenPageAsync(string fileName, int pageNumber)
    {
        using var archive = this.OpenArchive(fileName);
        var pageEntry =
            archive.FileEntries
               .Where(e => PageImageExtensions.Contains(Path.GetExtension(e.Name)))
               .OrderBy(e => e.Name)
               .Skip(pageNumber)
               .FirstOrDefault();
        return await this.CopyEntryAsync(pageEntry);
    }

    private async Task<Stream?> CopyEntryAsync(IArchiveFileEntry? entry)
    {
        if (this.OpenEntry(entry) is Stream archiveStream)
        {
            MemoryStream memoryStream = new();
            await archiveStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
        return default;
    }

    protected abstract IArchive OpenArchive(string fileName);

    protected abstract Stream? OpenEntry(IArchiveFileEntry? entry);

    private static readonly string[] PageImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };

}

public interface IRarReader : IArchiveReader { }

public interface IZipReader : IArchiveReader { }

internal class RarReader : ArchiveReader, IRarReader
{
    protected override IArchive OpenArchive(string fileName) => new RarArchive(fileName);

    protected override Stream? OpenEntry(IArchiveFileEntry? entry) => entry switch
    {
        RarArchiveEntry r => r.Open(),
        null => default,
        _ => throw new InvalidOperationException()
    };
}

internal class ZipReader : ArchiveReader, IZipReader
{
    protected override IArchive OpenArchive(string fileName) => new Archive(fileName);

    protected override Stream? OpenEntry(IArchiveFileEntry? entry) => entry switch
    {
        ArchiveEntry z => z.Open(),
        null => default,
        _ => throw new InvalidOperationException()
    };
}
