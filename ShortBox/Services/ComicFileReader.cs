namespace ShortBox.Services;

public interface IComicFileReader
{
    Task<Stream?> GetBookPageAsync(string fileName, int pageNumber, CancellationToken cancellationToken);
}

internal sealed class ComicFileReader : IComicFileReader 
{
    public ComicFileReader(IArchiveReaderFactory archiveReaderFactory)
    {
        _archiveReaderFactory = archiveReaderFactory;
    }

    public Task<Stream?> GetBookPageAsync(string fileName, int pageNumber, CancellationToken cancellationToken)
    {
        var reader = _archiveReaderFactory.GetReaderForArchive(fileName);
        return reader.OpenPageAsync(pageNumber);
    }

    private readonly IArchiveReaderFactory _archiveReaderFactory;
}
