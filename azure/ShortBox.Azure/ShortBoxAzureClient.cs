namespace ShortBox.Azure;

internal class ShortBoxAzureClientFactory(IHttpClientFactory clientFactory) : IShortBoxReaderClientFactory
{
    public IShortBoxReaderClient CreateClient() => new ShortBoxAzureClient(_clientFactory);

    private readonly IHttpClientFactory _clientFactory = clientFactory;
}

public class ShortBoxAzureClient(IHttpClientFactory clientFactory) : IShortBoxReaderClient
{
    public Task<IEnumerable<Book>> GetAllBooksAsync(CancellationToken cancellationToken = default) =>
        WithClient(client => client.GetSomeAsync<Book>("api/Books", cancellationToken));

    public Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken = default) =>
        WithClient(client => client.GetSomeAsync<Series>("api/series", cancellationToken));

    public async Task<Book?> GetBookAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var response = await WithClient(client => client.GetAsync($"api/book/{bookId}", cancellationToken)).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await response.Content.ReadFromJsonAsync<Book>(),
            HttpStatusCode.NotFound => null,
            _ => throw new HttpRequestException($"Failed to get book {bookId} with status {response.StatusCode}")
        };
    }

    public Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken cancellationToken = default) =>
        WithClient(client => client.GetSomeAsync<Book>($"api/series/{seriesName}", cancellationToken));

    public Task<IEnumerable<Book>> GetSeriesArchiveAsync(string seriesName, CancellationToken cancellationToken = default) =>
        WithClient(client => client.GetSomeAsync<Book>($"api/series/{seriesName}/archive", cancellationToken));

    public Task<Stream> GetBookCoverAsync(int bookId, int? height, CancellationToken cancellationToken) =>
        this.WithClient(client => client.GetStreamAsync($"api/book/{bookId}/cover", cancellationToken));

    public Task<Stream> GetBookPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken) =>
        this.WithClient(client => client.GetStreamAsync($"api/book/{bookId}/{pageNumber}", cancellationToken));

    public Task MarkPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken) =>
        this.WithClient(client => client.PutAsync($"api/book/{bookId}/mark/{pageNumber}", default, cancellationToken));

    private async Task<T> WithClient<T>(Func<HttpClient, Task<T>> query)
    {
        using var client = _clientFactory.CreateClient(nameof(ShortBoxAzureClient));
        client.Timeout = Timeout.InfiniteTimeSpan;
        return await query(client).ConfigureAwait(false);
    }

    private readonly IHttpClientFactory _clientFactory = clientFactory;
}
