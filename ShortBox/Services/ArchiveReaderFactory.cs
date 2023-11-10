namespace ShortBox.Services;

public interface IArchiveReaderFactory
{
    IKnownFileArchiveReader GetReaderForArchive(string fileName);
}

public interface IKnownFileArchiveReader
{
    Stream? GetInfoStream();
    int GetPageCount();
    Stream? OpenCover();
    Stream? OpenPage(int pageIndex);
}

internal sealed class ArchiveReaderFactory : IArchiveReaderFactory 
{
    public IKnownFileArchiveReader GetReaderForArchive(string fileName) => new KnownFileArchiveReader(fileName,
        Path.GetExtension(fileName) switch
        {
            ".cbz" => new ZipReader(),
            _ => new RarReader()
        });

    private sealed class KnownFileArchiveReader : IKnownFileArchiveReader
    {
        public KnownFileArchiveReader(string fileName, IArchiveReader reader)
        {
            _fileName = fileName;
            _reader = reader;
        }

        public Stream? GetInfoStream() => _reader.GetInfoStream(_fileName);

        public int GetPageCount() => _reader.GetPageCount(_fileName);

        public Stream? OpenCover() => _reader.OpenCover(_fileName);

        public Stream? OpenPage(int pageIndex) => _reader.OpenPage(_fileName, pageIndex);

        private readonly string _fileName;
        private readonly IArchiveReader _reader;
    }
}
