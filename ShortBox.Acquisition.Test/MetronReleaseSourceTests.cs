using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using ShortBox.Metron;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class MetronReleaseSourceTests
{
    [TestMethod]
    public async Task ReadsReleasesAcrossPagesAndFiltersToTheRange()
    {
        var handler = new StubHandler(
            Json("""
                {"next":"https://metron.cloud/api/issue/?page=2","results":[
                  {"id":10,"series":{"name":"Darkhawk"},"number":"4","issue":"Darkhawk #4","store_date":"2026-10-07","image":"https://static.metron.cloud/a.jpg","desc":"Hi"},
                  {"id":11,"series":{"name":"Too Early"},"number":"1","store_date":"2026-10-04"}]}
                """),
            Json("""
                {"next":null,"results":[
                  {"id":12,"series":{"name":"Daredevil"},"number":"9","store_date":"2026-10-07"},
                  {"id":13,"series":{"name":"No Date"},"number":"1","store_date":null}]}
                """));

        var releases = await Source(handler).GetReleasesAsync(DateOnly.Parse("2026-10-05"), DateOnly.Parse("2026-10-11"), default);

        CollectionAssert.AreEqual(
            new[] { MetronReleaseSource.IdOffset + 10, MetronReleaseSource.IdOffset + 12 },
            releases.Select(r => r.ExternalId).ToArray());
        Assert.AreEqual("Darkhawk #4", releases[0].Title);
        Assert.AreEqual("Daredevil #9", releases[1].Title, "title falls back to series and number");
        Assert.AreEqual(new Uri("https://static.metron.cloud/a.jpg"), releases[0].CoverUri);
        StringAssert.Contains(handler.Requests[0], "publisher_name=marvel");
        StringAssert.Contains(handler.Requests[0], "store_date_range_after=2026-10-04");
        StringAssert.Contains(handler.Requests[0], "store_date_range_before=2026-10-12");
        Assert.AreEqual("https://metron.cloud/api/issue/?page=2", handler.Requests[1]);
    }

    [TestMethod]
    public async Task RetriesAfterRateLimiting()
    {
        var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter = new(TimeSpan.Zero);
        var handler = new StubHandler(limited, Json("""{"next":null,"results":[]}"""));

        var releases = await Source(handler).GetReleasesAsync(DateOnly.Parse("2026-10-05"), DateOnly.Parse("2026-10-11"), default);

        Assert.AreEqual(0, releases.Count);
        Assert.AreEqual(2, handler.Requests.Count);
    }

    [TestMethod]
    public async Task ThrowsOnAuthFailure()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.ThrowsExceptionAsync<HttpRequestException>(() =>
            Source(handler).GetReleasesAsync(DateOnly.Parse("2026-10-05"), DateOnly.Parse("2026-10-11"), default));
    }

    private static MetronReleaseSource Source(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new("https://metron.cloud/api/") },
            Options.Create(new MetronOptions()));

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private class StubHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
