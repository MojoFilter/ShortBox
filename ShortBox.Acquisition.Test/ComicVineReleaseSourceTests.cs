using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using ShortBox.ComicVine;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class ComicVineReleaseSourceTests
{
    private static readonly DateOnly From = DateOnly.Parse("2026-10-05");
    private static readonly DateOnly To = DateOnly.Parse("2026-10-11");

    [TestMethod]
    public async Task KeepsOnlyTheConfiguredPublishersIssues()
    {
        var handler = new QueueHandler(
            Json("""
                {"status_code":1,"number_of_total_results":3,"results":[
                  {"id":1001,"name":"Hawk Down","issue_number":"4","store_date":"2026-10-07",
                   "image":{"medium_url":"https://img.example/a.jpg"},"volume":{"id":7,"name":"Darkhawk"},"deck":"Brief"},
                  {"id":1002,"name":null,"issue_number":"9 ","store_date":"2026-10-07","image":null,"volume":{"id":8,"name":"Daredevil"}},
                  {"id":1003,"name":null,"issue_number":"1","store_date":"2026-10-08","volume":{"id":9,"name":"Other Publisher Book"}}]}
                """),
            Json("""
                {"status_code":1,"number_of_total_results":3,"results":[
                  {"id":7,"publisher":{"name":"Marvel"}},
                  {"id":8,"publisher":{"name":"marvel"}},
                  {"id":9,"publisher":{"name":"Image"}}]}
                """));

        var releases = await Source(handler).GetReleasesAsync(From, To, default);

        // Ordered by store date, then series name.
        CollectionAssert.AreEqual(new[] { 1002, 1001 }, releases.Select(r => r.ExternalId).ToArray());
        Assert.AreEqual("Darkhawk #4: Hawk Down", releases[1].Title);
        Assert.AreEqual("Daredevil #9", releases[0].Title);
        Assert.AreEqual("9", releases[0].Number, "Comic Vine pads some issue numbers with whitespace");
        Assert.AreEqual("Darkhawk", releases[1].Series);
        Assert.AreEqual(new Uri("https://img.example/a.jpg"), releases[1].CoverUri);
        Assert.IsNull(releases[0].CoverUri);
        Assert.AreEqual(DateOnly.Parse("2026-10-07"), releases[1].StoreDate);
        StringAssert.Contains(Uri.UnescapeDataString(handler.Requests[0]), "filter=store_date:2026-10-05|2026-10-11");
        StringAssert.Contains(Uri.UnescapeDataString(handler.Requests[1]), "volumes/");
        StringAssert.Contains(Uri.UnescapeDataString(handler.Requests[1]), "filter=id:7|8|9");
    }

    [TestMethod]
    public async Task PagesThroughAllIssues()
    {
        var firstPage = Enumerable.Range(1, 100)
            .Select(i => "{\"id\":" + i + ",\"issue_number\":\"1\",\"store_date\":\"2026-10-07\",\"volume\":{\"id\":7,\"name\":\"V\"}}");
        var handler = new QueueHandler(
            Json("{\"status_code\":1,\"number_of_total_results\":101,\"results\":[" + string.Join(',', firstPage) + "]}"),
            Json("""{"status_code":1,"number_of_total_results":101,"results":[{"id":101,"issue_number":"1","store_date":"2026-10-07","volume":{"id":7,"name":"V"}}]}"""),
            Json("""{"status_code":1,"number_of_total_results":1,"results":[{"id":7,"publisher":{"name":"Marvel"}}]}"""));

        var releases = await Source(handler).GetReleasesAsync(From, To, default);

        Assert.AreEqual(101, releases.Count);
        StringAssert.Contains(Uri.UnescapeDataString(handler.Requests[1]), "offset=100");
        StringAssert.Contains(Uri.UnescapeDataString(handler.Requests[2]), "filter=id:7");
    }

    [TestMethod]
    public async Task PagesInAStableOrderAndDropsRepeatedIssues()
    {
        // Comic Vine can repeat an item across pages when the sort key has ties, so sort by id and de-duplicate.
        var handler = new QueueHandler(
            Json("""
                {"status_code":1,"number_of_total_results":2,"results":[
                  {"id":5,"issue_number":"1","store_date":"2026-10-07","volume":{"id":7,"name":"V"}},
                  {"id":5,"issue_number":"1","store_date":"2026-10-07","volume":{"id":7,"name":"V"}}]}
                """),
            Json("""{"status_code":1,"number_of_total_results":1,"results":[{"id":7,"publisher":{"name":"Marvel"}}]}"""));

        var releases = await Source(handler).GetReleasesAsync(From, To, default);

        Assert.AreEqual(1, releases.Count);
        StringAssert.Contains(handler.Requests[0], "sort=id:asc");
    }

    [TestMethod]
    public async Task ReportsApiErrorsClearly()
    {
        var handler = new QueueHandler(Json("""{"error":"Invalid API Key","status_code":100,"number_of_total_results":0,"results":[]}"""));

        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            Source(handler).GetReleasesAsync(From, To, default));

        StringAssert.Contains(ex.Message, "Invalid API Key");
    }

    [TestMethod]
    public async Task AWeekWithoutIssuesNeedsNoVolumeLookup()
    {
        var handler = new QueueHandler(Json("""{"status_code":1,"number_of_total_results":0,"results":[]}"""));

        var releases = await Source(handler).GetReleasesAsync(From, To, default);

        Assert.AreEqual(0, releases.Count);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task SendsTheApiKeyEscaped()
    {
        var handler = new QueueHandler(Json("""{"status_code":1,"number_of_total_results":0,"results":[]}"""));

        await Source(handler, apiKey: "a b&c").GetReleasesAsync(From, To, default);

        StringAssert.Contains(handler.Requests[0], "api_key=a%20b%26c");
    }

    private static ComicVineReleaseSource Source(QueueHandler handler, string apiKey = "key") =>
        new(new HttpClient(handler) { BaseAddress = new("https://comicvine.gamespot.com/api/") },
            Options.Create(new ComicVineOptions { ApiKey = apiKey, RequestDelay = TimeSpan.Zero }));

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
