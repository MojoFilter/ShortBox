using ShortBox.Api.Data;
using ShortBox.Communication;

namespace ShortBox.Web.Server;

public class TestClient : IShortBoxApiClient
{
    public Task<IEnumerable<Book>> GetAllBooksAsync(CancellationToken cancellationToken = default) => Task.FromResult(
        Enumerable.Range(0, 100).Select(i => new Book()
        {
            Id = i,
            Number = i.ToString(),
            Series = "Testing Adventures"
        }));

    public Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken) => Task.FromResult(
        Enumerable.Range(1, 100).Select(i => new Series($"Number {i} Adventures")));

    public Task<Stream> GetSeriesCoverAsync(string seriesName, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(File.OpenRead(@"D:\Comics\covers\Deadpool 010 (2023) (Digital) (Li'l-Empire) (HD-Upscaled).cbz.jpg"));
}
