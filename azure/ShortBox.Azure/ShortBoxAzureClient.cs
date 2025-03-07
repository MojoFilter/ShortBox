namespace ShortBox.Azure;

public class ShortBoxAzureClient(IHttpClientFactory clientFactory) : IShortBoxReaderClient
{
    public Task<IEnumerable<Book>> GetAllBooksAsync(CancellationToken cancellationToken = default) =>
        GetClient().GetSomeAsync<Book>("api/Books", cancellationToken);

    public Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken = default) =>
        GetClient().GetSomeAsync<Series>("api/series", cancellationToken);

    public async Task<Book?> GetBookAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var response = await GetClient().GetAsync($"api/book/{bookId}", cancellationToken).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await response.Content.ReadFromJsonAsync<Book>(),
            HttpStatusCode.NotFound => null,
            _ => throw new HttpRequestException($"Failed to get book {bookId} with status {response.StatusCode}")
        };
    }

    public Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken cancellationToken = default) =>
        GetClient().GetSomeAsync<Book>($"api/series/{seriesName}", cancellationToken);

    public Task<IEnumerable<Book>> GetSeriesArchiveAsync(string seriesName, CancellationToken cancellationToken = default) =>
        GetClient().GetSomeAsync<Book>($"api/series/{seriesName}/archive", cancellationToken);

    public Task<Stream> GetBookCoverAsync(int bookId, int? height, CancellationToken cancellationToken) =>
        this.GetClient().GetStreamAsync($"api/book/{bookId}/cover", cancellationToken);

    public Task<Stream> GetBookPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private HttpClient GetClient() => _clientFactory.CreateClient(nameof(ShortBoxAzureClient));

    private readonly IHttpClientFactory _clientFactory = clientFactory;
}
