using Aspose.Zip;
using Aspose.Zip.Rar;

namespace ShortBox.Services;

public interface IArchiveReader
{
    Stream? GetInfoStream(string fileName);
    int GetPageCount(string fileName);
    Stream? OpenCover(string fileName);
}

internal abstract class ArchiveReader : IArchiveReader
{
    public static readonly string ComicInfoFileName = "ComicInfo.xml";

    public Stream? GetInfoStream(string fileName)
    {
        using var archive = this.OpenArchive(fileName);
        var infoEntry = archive.FileEntries.FirstOrDefault(e => string.Equals(e.Name, ArchiveReader.ComicInfoFileName, StringComparison.InvariantCultureIgnoreCase));
        return this.CopyEntry(infoEntry);
    }

    public int GetPageCount(string fileName)
    {
        using var archive = this.OpenArchive(fileName);
        return archive.FileEntries.Count() - 1;
    }

    public Stream? OpenCover(string fileName) => this.OpenPage(fileName, 0);

    public Stream? OpenPage(string fileName, int pageNumber)
    {
        using var archive = this.OpenArchive(fileName);
        var pageEntry =
            archive.FileEntries
               .Where(e => Path.GetExtension(e.Name) is ".jpg")
               .OrderBy(e => e.Name)
               .Skip(pageNumber)
               .FirstOrDefault();
        return this.CopyEntry(pageEntry);
    }

    private Stream? CopyEntry(IArchiveFileEntry? entry)
    {
        if (this.OpenEntry(entry) is Stream archiveStream)
        {
            MemoryStream memoryStream = new();
            archiveStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
        return default;
    }

    protected abstract IArchive OpenArchive(string fileName);

    protected abstract Stream? OpenEntry(IArchiveFileEntry? entry);

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
