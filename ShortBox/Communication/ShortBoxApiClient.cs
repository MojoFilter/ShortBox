namespace ShortBox.Communication;

public interface IShortBoxApiClient
{
    Task<IEnumerable<Book>> GetAllBooksAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken = default);
    Task<Stream> GetBookCoverAsync(int bookId, int? height, CancellationToken cancellationToken);
    Task<Stream> GetBookPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken);
    Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken cancellationToken = default);
    Task<Stream> GetSeriesCoverAsync(string seriesName, int? height, CancellationToken cancellationToken = default);
    Task MarkPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken);
}

internal sealed class ShortBoxApiClient : IShortBoxApiClient
{

    public ShortBoxApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<IEnumerable<Book>> GetAllBooksAsync(CancellationToken cancellationToken = default) => 
        GetSomeAsync<Book>("books", cancellationToken);


    public Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken) =>
        GetSomeAsync<Series>("series", cancellationToken);

    public Task<Stream> GetSeriesCoverAsync(string seriesName, int? height, CancellationToken cancellationToken) =>
        _httpClient.GetStreamAsync($"series/{seriesName}/cover?{(height.HasValue ? $"height={height}" : "")}", cancellationToken);

    public Task<Stream> GetBookCoverAsync(int bookId, int? height, CancellationToken cancellationToken) =>
        _httpClient.GetStreamAsync($"book/{bookId}/cover?{(height.HasValue ? $"height={height}" : "")}");

    public Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken cancellationToken = default) =>
        GetSomeAsync<Book>($"series/{seriesName}", cancellationToken);

    public Task<Stream> GetBookPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken) =>
        _httpClient.GetStreamAsync($"book/{bookId}/{pageNumber}", cancellationToken);

    public Task MarkPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken) =>
        _httpClient.PutAsync($"book/{bookId}/mark/{pageNumber}", default, cancellationToken);

    private async Task<IEnumerable<T>> GetSomeAsync<T>(string uri, CancellationToken cancellationToken)
    {
        IEnumerable<T> getDefault() => Enumerable.Empty<T>();
        try
        {
            return (await _httpClient.GetFromJsonAsync<IEnumerable<T>>(uri, cancellationToken)) ?? getDefault();
        }
        catch
        {
            return getDefault();
        }
    }

    private readonly HttpClient _httpClient;
}
