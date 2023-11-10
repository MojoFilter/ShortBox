using ShortBox.Api.Data;
using System.Net.Http.Json;

namespace ShortBox.Web.Shared;

public interface IShortBoxWebClient
{
    Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Book>> GetIssuesAsync(string seriesName);
    Task MarkPageAsync(int bookId, int pageNumber, CancellationToken cancellation = default);
}

public class ShortBoxWebClient : IShortBoxWebClient
{
    public ShortBoxWebClient(HttpClient client)
    {
        _client = client;
    }

    public Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken = default) => GetSetAsync<Series>("Library");

    public Task<IEnumerable<Book>> GetIssuesAsync(string seriesName) => GetSetAsync<Book>($"Library/{seriesName}");

    public async Task MarkPageAsync(int bookId, int pageNumber, CancellationToken cancellation = default) => 
        await _client.PutAsync($"Library/book/{bookId}/mark/{pageNumber}", default);    

    private async Task<IEnumerable<T>> GetSetAsync<T>(string uri) =>
        (await _client.GetFromJsonAsync<IEnumerable<T>>(uri)) ?? Enumerable.Empty<T>();

    private readonly HttpClient _client;

}
