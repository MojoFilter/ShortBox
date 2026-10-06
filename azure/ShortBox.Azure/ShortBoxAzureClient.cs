using ShortBox.Services;

namespace ShortBox.Azure;

internal class ShortBoxAzureClientFactory(IHttpClientFactory clientFactory) : IShortBoxReaderClientFactory
{
    public IShortBoxReaderClient CreateClient() => new ShortBoxAzureClient(_clientFactory);

    private readonly IHttpClientFactory _clientFactory = clientFactory;
}

public class ShortBoxAzureClient(IHttpClientFactory clientFactory) : IShortBoxReaderClient, IShortBoxReleasesClient
{
    public Task<IEnumerable<PullListEntry>> GetReleasesAsync(DateOnly? week = null, bool refresh = false, CancellationToken cancellationToken = default)
    {
        var query = $"?refresh={refresh.ToString().ToLowerInvariant()}"
                  + (week is { } date ? $"&week={date:yyyy-MM-dd}" : string.Empty);
        return WithClient(client => client.GetSomeAsync<PullListEntry>($"api/releases{query}", cancellationToken));
    }

    public Task<IEnumerable<PullListEntry>> GetWantedAsync(CancellationToken cancellationToken = default) =>
        WithClient(client => client.GetSomeAsync<PullListEntry>("api/wanted", cancellationToken));

    public Task SetWantedAsync(int entryId, bool wanted, CancellationToken cancellationToken = default) =>
        WithClient(async client =>
        {
            using var response = await client.PutAsync(
                $"api/releases/{entryId}/wanted/{wanted.ToString().ToLowerInvariant()}", default, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return true;
        });

    public Task<IngestResult> ScanLibraryAsync(CancellationToken cancellationToken = default) =>
        WithClient(async client =>
        {
            using var response = await client.PostAsync("api/library/scan", default, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<IngestResult>(cancellationToken).ConfigureAwait(false)
                ?? new IngestResult([], [], 0);
        });

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
        WithClient(client => client.GetSomeAsync<Book>($"api/series/{Uri.EscapeDataString(seriesName)}", cancellationToken));

    public Task<IEnumerable<Book>> GetSeriesArchiveAsync(string seriesName, CancellationToken cancellationToken = default) =>
        WithClient(client => client.GetSomeAsync<Book>($"api/series/{Uri.EscapeDataString(seriesName)}/archive", cancellationToken));

    public Task<Stream> GetBookCoverAsync(int bookId, int? height, CancellationToken cancellationToken) =>
        this.WithClient(client => client.GetStreamAsync($"api/book/{bookId}/cover", cancellationToken));

    public Task<Stream> GetBookPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken) =>
        this.WithClient(client => client.GetStreamAsync($"api/book/{bookId}/{pageNumber}", cancellationToken));

    public Task MarkPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken) =>
        this.WithClient(async client =>
        {
            using var response = await client.PutAsync($"api/book/{bookId}/mark/{pageNumber}", default, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return true;
        });

    public Task MarkReadAsync(int bookId, bool read, CancellationToken cancellationToken) =>
        this.WithClient(async client =>
        {
            using var response = await client.PutAsync(
                $"api/book/{bookId}/read/{read.ToString().ToLowerInvariant()}", default, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return true;
        });

    private async Task<T> WithClient<T>(Func<HttpClient, Task<T>> query)
    {
        // Deliberately not disposed: HttpClient.Dispose cancels its pending requests, which would abort the body of a
        // returned stream before the caller reads it. Clients from the factory share a pooled handler, so there is
        // nothing to release.
        var client = _clientFactory.CreateClient(nameof(ShortBoxAzureClient));
        client.Timeout = Timeout.InfiniteTimeSpan;
        return await query(client).ConfigureAwait(false);
    }

    private readonly IHttpClientFactory _clientFactory = clientFactory;
}
