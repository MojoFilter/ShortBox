namespace ShortBox.Services;

public interface IArchiveReaderFactory
{
    IKnownFileArchiveReader GetReaderForArchive(string fileName);
}

public interface IKnownFileArchiveReader
{
    Task<Stream?> GetInfoStreamAsync();
    int GetPageCount();
    Task<Stream?> OpenCoverAsync();
    Task<Stream?> OpenPageAsync(int pageIndex);
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

        public Task<Stream?> GetInfoStreamAsync() => _reader.GetInfoStreamAsync(_fileName);

        public int GetPageCount() => _reader.GetPageCount(_fileName);

        public Task<Stream?> OpenCoverAsync() => _reader.OpenCoverAsync(_fileName);

        public Task<Stream?> OpenPageAsync(int pageIndex) => _reader.OpenPageAsync(_fileName, pageIndex);

        private readonly string _fileName;
        private readonly IArchiveReader _reader;
    }
}
