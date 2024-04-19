namespace ShortBox.Services;

public interface IBookFactory
{
    Task<Book> CreateFromInfoAsync(string fileName, IArchiveReader reader, CancellationToken cancellationToken);
}

internal class BookFactory : IBookFactory 
{
    public BookFactory(IComicInfoReader comicInfoReader, IComicFileNameParser parser)
    {
        _comicInfoReader = comicInfoReader;
        _parser = parser;
    }

    public async Task<Book> CreateFromInfoAsync(string fileName, IArchiveReader reader, CancellationToken cancellationToken)
    {
        var book = new Book()
        {
            FileName = Path.GetFileName(fileName),
            Added = DateTime.Now,
            Modified = DateTime.Now
        };
        using var stream = await reader.GetInfoStreamAsync(fileName);
        if (stream is Stream)
        {
            var info = await _comicInfoReader.ReadAsync(stream, cancellationToken);
            (book.Series, book.Number, book.PageCount) =
                (info.Series, info.Number, info.PageCount);
        }
        if (string.IsNullOrWhiteSpace(book.Series)
            && _parser.Parse(book.FileName) is var result)
        {
            book.Series = result.Series;
            book.Number = result.Number;
        }
        return book;
    }

    private readonly IComicInfoReader _comicInfoReader;
    private readonly IComicFileNameParser _parser;
}
