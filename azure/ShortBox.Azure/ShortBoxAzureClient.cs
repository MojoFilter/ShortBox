namespace ShortBox.Azure;

public class ShortBoxAzureClient(IHttpClientFactory clientFactory) : IShortBoxReaderClient
{
    public Task<IEnumerable<Book>> GetAllBooksAsync(CancellationToken cancellationToken = default) =>
        GetClient().GetSomeAsync<Book>("api/Books", cancellationToken);

    public Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken = default) =>
        GetClient().GetSomeAsync<Series>("api/series", cancellationToken);

    public Task<Book?> GetBookAsync(int bookId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Book>> GetSeriesArchiveAsync(string seriesName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetBookCoverAsync(int bookId, int? height, CancellationToken cancellationToken) =>
        this.GetClient().GetStreamAsync($"api/book/{bookId}/cover", cancellationToken);

    public Task<Stream> GetBookPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private HttpClient GetClient() => _clientFactory.CreateClient(nameof(ShortBoxAzureClient));

    private readonly IHttpClientFactory _clientFactory = clientFactory;
}
