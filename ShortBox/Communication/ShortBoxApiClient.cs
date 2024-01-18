namespace ShortBox.Communication;

public interface IShortBoxApiClient
{
    Task CombineSeriesNamesAsync(string[] seriesToCombine, string combinedName, CancellationToken cancellationToken);
    Task<IEnumerable<Book>> GetAllBooksAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken = default);
    Task<Book?> GetBookAsync(int bookId, CancellationToken cancellationToken = default);
    Task<Stream> GetBookCoverAsync(int bookId, int? height, CancellationToken cancellationToken);
    Task<Stream> GetBookPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken);
    Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken cancellationToken = default);
    Task<IEnumerable<Book>> GetSeriesArchiveAsync(string seriesName, CancellationToken cancellationToken = default);
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

    public Task<Book?> GetBookAsync(int bookId, CancellationToken cancellationToken) =>
        _httpClient.GetFromJsonAsync<Book>($"/book/{bookId}", cancellationToken);

    public Task<Stream> GetBookCoverAsync(int bookId, int? height, CancellationToken cancellationToken) =>
        _httpClient.GetStreamAsync($"book/{bookId}/cover?{(height.HasValue ? $"height={height}" : "")}");

    public Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken cancellationToken = default) =>
        GetSomeAsync<Book>($"series/{seriesName}", cancellationToken);

    public Task<IEnumerable<Book>> GetSeriesArchiveAsync(string seriesName, CancellationToken cancellationToken = default) =>
        GetSomeAsync<Book>($"series/{seriesName}/archive", cancellationToken);


    public Task<Stream> GetBookPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken = default) =>
        _httpClient.GetStreamAsync($"book/{bookId}/{pageNumber}", cancellationToken);

    public Task MarkPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken) =>
        _httpClient.PutAsync($"book/{bookId}/mark/{pageNumber}", default, cancellationToken);

    private async Task<IEnumerable<T>> GetSomeAsync<T>(string uri, CancellationToken cancellationToken)
    {
        IEnumerable<T> getDefault() => Enumerable.Empty<T>();
        try
        {
            var result = await _httpClient.GetFromJsonAsync<IEnumerable<T>>(uri, cancellationToken);
            return result ?? getDefault();
        }
        catch (Exception ex) 
        {
            //Debug.WriteLine(ex.Message);
            return getDefault();
        }
    }

    public async Task CombineSeriesNamesAsync(string[] seriesToCombine, string combinedName, CancellationToken cancellationToken)
    {
    }

    private readonly HttpClient _httpClient;
}
