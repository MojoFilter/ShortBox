namespace ShortBox.Communication;

public interface IShortBoxApiClient
{
    Task<IEnumerable<Book>> GetAllBooksAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken = default);
    Task<Stream> GetSeriesCoverAsync(string seriesName, CancellationToken cancellationToken = default);
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

    public Task<Stream> GetSeriesCoverAsync(string seriesName, CancellationToken cancellationToken) =>
        _httpClient.GetStreamAsync($"series/{seriesName}/cover", cancellationToken);

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
