﻿using ShortBox.Api.Data;
using ShortBox.Communication;

namespace ShortBox.Web.Server;

public class TestClient : IShortBoxApiClient
{
    public Task<IEnumerable<Book>> GetAllBooksAsync(CancellationToken cancellationToken = default) => Task.FromResult(
        Enumerable.Range(0, 100).Select(i => new Book()
        {
            Id = i,
            FileName = string.Empty,
            Number = i.ToString(),
            Series = "Testing Adventures"
        }));

    public Task<IEnumerable<Series>> GetAllSeriesAsync(CancellationToken cancellationToken) => Task.FromResult(
        Enumerable.Range(1, 100).Select(i => new Series($"Number {i} Adventures")));

    public Task<Stream> GetBookCoverAsync(int bookId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetBookPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Book>> GetIssuesAsync(string seriesName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetSeriesCoverAsync(string seriesName, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(File.OpenRead(@"D:\Comics\covers\Deadpool 010 (2023) (Digital) (Li'l-Empire) (HD-Upscaled).cbz.jpg"));

    public Task MarkPageAsync(int bookId, int pageNumber, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
