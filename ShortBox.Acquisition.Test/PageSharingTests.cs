using ShortBox.Azure;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ShortBox.Acquisition.Test;

/// <summary>Page downloads shared between callers, abandoned by the last one to leave, and prefetches that yield to visible pages.</summary>
[TestClass]
public class PageSharingTests
{
    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Ready() => Json(HttpStatusCode.OK, """{"status":"ready","pageCount":27,"error":null}""");

    private static HttpResponseMessage Image(params byte[] bytes)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return response;
    }

    /// <summary>A page request that waits until <paramref name="gate"/> opens (or the request is cancelled), then serves an image.</summary>
    private static Func<CancellationToken, Task<HttpResponseMessage>> Gated(TaskCompletionSource gate) => async token =>
    {
        await gate.Task.WaitAsync(token);
        return Image(1, 2, 3);
    };

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.IsTrue(DateTime.UtcNow < deadline, "Timed out waiting for the condition.");
            await Task.Delay(10);
        }
    }

    /// <summary>For asserting that something does not happen: long enough for it to, were it going to.</summary>
    private static Task Settle() => Task.Delay(150);

    [TestInitialize]
    public void CreateCacheFolder() => _cacheDir = Path.Combine(Path.GetTempPath(), $"pages-{Guid.NewGuid():N}");

    [TestCleanup]
    public void DeleteCacheFolder()
    {
        if (Directory.Exists(_cacheDir))
        {
            Directory.Delete(_cacheDir, true);
        }
    }

    private PageProvider Provider(ScriptedHandler http, Action<PageProviderOptions>? tweak = null)
    {
        var options = new PageProviderOptions { CacheDirectory = _cacheDir, Delay = (_, token) => Task.Delay(1, token) };
        tweak?.Invoke(options);
        return new PageProvider(http, options);
    }

    private static ScriptedHandler Book() => new ScriptedHandler().On("POST", "/api/book/7/prepare", Ready());

    [TestMethod]
    public async Task CallersAskingForOnePageShareOneDownload()
    {
        var gate = new TaskCompletionSource();
        var http = Book().On("GET", "/api/book/7/3", Gated(gate));
        using var provider = this.Provider(http);

        var first = provider.GetPageFileAsync(7, 3);
        var second = provider.GetPageFileAsync(7, 3);
        gate.SetResult();

        Assert.AreEqual(await first, await second);
        Assert.AreEqual(1, http.CallsTo("GET", "/api/book/7/3"));
    }

    [TestMethod]
    public async Task APrefetchAndTheVisiblePageShareOneDownload()
    {
        var gate = new TaskCompletionSource();
        var http = Book().On("GET", "/api/book/7/3", Gated(gate));
        using var provider = this.Provider(http);

        var prefetch = provider.PrefetchPageAsync(7, 3);
        var visible = provider.GetPageFileAsync(7, 3);
        gate.SetResult();

        Assert.AreEqual(await prefetch, await visible);
        Assert.AreEqual(1, http.CallsTo("GET", "/api/book/7/3"));
    }

    [TestMethod]
    public async Task APrefetchedPageIsServedFromDiskWithoutAnotherRequest()
    {
        var http = Book().On("GET", "/api/book/7/3", Image(9));
        using var provider = this.Provider(http);

        await provider.PrefetchPageAsync(7, 3);
        var calls = http.Calls.Count;
        await provider.GetPageFileAsync(7, 3);

        Assert.AreEqual(calls, http.Calls.Count);
    }

    [TestMethod]
    public async Task OneWaiterCancellingDoesNotStopTheDownloadForTheOthers()
    {
        var gate = new TaskCompletionSource();
        var abandoned = false;
        var http = Book().On("GET", "/api/book/7/3", async token =>
        {
            try
            {
                await gate.Task.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                abandoned = true;
                throw;
            }

            return Image(1);
        });
        using var provider = this.Provider(http);
        using var cancel = new CancellationTokenSource();

        var leaving = provider.PrefetchPageAsync(7, 3, cancel.Token);
        var staying = provider.GetPageFileAsync(7, 3);
        cancel.Cancel();
        await Expect.ThrowsAsync<OperationCanceledException>(() => leaving);
        gate.SetResult();

        Assert.IsTrue(File.Exists(await staying));
        Assert.IsFalse(abandoned);
        Assert.AreEqual(1, http.CallsTo("GET", "/api/book/7/3"));
    }

    [TestMethod]
    public async Task TheLastWaiterLeavingAbandonsTheDownloadAndALaterCallStartsAgain()
    {
        var started = new TaskCompletionSource();
        var abandoned = new TaskCompletionSource();
        var http = Book().On("GET", "/api/book/7/3",
            async token =>
            {
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException)
                {
                    abandoned.TrySetResult();
                    throw;
                }

                return Image(1);
            },
            _ => Task.FromResult(Image(2)));
        using var provider = this.Provider(http);
        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();

        var a = provider.GetPageFileAsync(7, 3, first.Token);
        var b = provider.PrefetchPageAsync(7, 3, second.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        first.Cancel();
        second.Cancel();
        await Expect.ThrowsAsync<OperationCanceledException>(() => a);
        await Expect.ThrowsAsync<OperationCanceledException>(() => b);
        await abandoned.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var path = await provider.GetPageFileAsync(7, 3);

        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual(2, http.CallsTo("GET", "/api/book/7/3"));
    }

    [TestMethod]
    public async Task APrefetchWaitsWhileAVisiblePageIsDownloading()
    {
        var visibleGate = new TaskCompletionSource();
        var http = Book()
            .On("GET", "/api/book/7/1", Gated(visibleGate))
            .On("GET", "/api/book/7/2", Image(2));
        using var provider = this.Provider(http);

        var visible = provider.GetPageFileAsync(7, 1);
        var prefetch = provider.PrefetchPageAsync(7, 2);
        await Eventually(() => http.CallsTo("GET", "/api/book/7/1") == 1);
        await Settle();

        Assert.AreEqual(0, http.CallsTo("GET", "/api/book/7/2"), "A prefetch must not compete with the visible page.");

        visibleGate.SetResult();
        await visible;
        await prefetch;
        Assert.AreEqual(1, http.CallsTo("GET", "/api/book/7/2"));
    }

    [TestMethod]
    public async Task AVisibleRequestPromotesAPrefetchThatWasStillWaiting()
    {
        var visibleGate = new TaskCompletionSource();
        var promotedGate = new TaskCompletionSource();
        var http = Book()
            .On("GET", "/api/book/7/1", Gated(visibleGate))
            .On("GET", "/api/book/7/2", Gated(promotedGate));
        using var provider = this.Provider(http);

        var one = provider.GetPageFileAsync(7, 1);
        var prefetch = provider.PrefetchPageAsync(7, 2);
        await Eventually(() => http.CallsTo("GET", "/api/book/7/1") == 1);
        await Settle();
        Assert.AreEqual(0, http.CallsTo("GET", "/api/book/7/2"));

        var promoted = provider.GetPageFileAsync(7, 2);
        await Eventually(() => http.CallsTo("GET", "/api/book/7/2") == 1);

        visibleGate.SetResult();
        promotedGate.SetResult();
        Assert.AreEqual(await prefetch, await promoted);
        await one;
        Assert.AreEqual(1, http.CallsTo("GET", "/api/book/7/2"));
    }

    [TestMethod]
    public async Task OnlyTheConfiguredNumberOfPrefetchesDownloadAtOnce()
    {
        var gates = new[] { new TaskCompletionSource(), new TaskCompletionSource(), new TaskCompletionSource() };
        var http = Book()
            .On("GET", "/api/book/7/1", Gated(gates[0]))
            .On("GET", "/api/book/7/2", Gated(gates[1]))
            .On("GET", "/api/book/7/3", Gated(gates[2]));
        using var provider = this.Provider(http, o => o.MaxPrefetchDownloads = 2);

        var downloads = new[] { provider.PrefetchPageAsync(7, 1), provider.PrefetchPageAsync(7, 2), provider.PrefetchPageAsync(7, 3) };
        await Eventually(() => http.CallsTo("GET", "/api/book/7/2") == 1);
        await Settle();

        Assert.AreEqual(0, http.CallsTo("GET", "/api/book/7/3"));

        gates[0].SetResult();
        await Eventually(() => http.CallsTo("GET", "/api/book/7/3") == 1);
        gates[1].SetResult();
        gates[2].SetResult();
        await Task.WhenAll(downloads);
    }

    [TestMethod]
    public async Task APrefetchCancelledWhileWaitingNeverReachesTheServerAndFreesItsPlace()
    {
        var visibleGate = new TaskCompletionSource();
        var http = Book()
            .On("GET", "/api/book/7/1", Gated(visibleGate))
            .On("GET", "/api/book/7/2", Image(2))
            .On("GET", "/api/book/7/3", Image(3));
        using var provider = this.Provider(http, o => o.MaxPrefetchDownloads = 1);
        using var cancel = new CancellationTokenSource();

        var visible = provider.GetPageFileAsync(7, 1);
        var cancelled = provider.PrefetchPageAsync(7, 2, cancel.Token);
        var next = provider.PrefetchPageAsync(7, 3);
        await Eventually(() => http.CallsTo("GET", "/api/book/7/1") == 1);
        cancel.Cancel();
        await Expect.ThrowsAsync<OperationCanceledException>(() => cancelled);
        visibleGate.SetResult();

        await visible;
        await next;
        Assert.AreEqual(0, http.CallsTo("GET", "/api/book/7/2"));
        Assert.AreEqual(1, http.CallsTo("GET", "/api/book/7/3"));
    }

    [TestMethod]
    public async Task APrefetchDoesNotMakeTheServerExtractABookItDropped()
    {
        var http = Book().On("GET", "/api/book/7/3", new HttpResponseMessage(HttpStatusCode.Accepted));
        using var provider = this.Provider(http);

        var ex = await Expect.ThrowsAsync<PageLoadException>(() => provider.PrefetchPageAsync(7, 3));

        Assert.AreEqual(PageLoadFailure.NotReady, ex.Kind);
        Assert.AreEqual(1, http.CallsTo("POST", "/api/book/7/prepare"));
    }

    [TestMethod]
    public async Task AFailedDownloadIsNotHandedToTheNextCaller()
    {
        var http = Book().On("GET", "/api/book/7/3", new HttpResponseMessage(HttpStatusCode.NotFound));
        using var provider = this.Provider(http);

        await Expect.ThrowsAsync<PageLoadException>(() => provider.PrefetchPageAsync(7, 3));
        await Expect.ThrowsAsync<PageLoadException>(() => provider.GetPageFileAsync(7, 3));

        Assert.AreEqual(2, http.CallsTo("GET", "/api/book/7/3"));
    }

    [TestMethod]
    public async Task ReleasingABookCancelsItsPageDownloads()
    {
        var gate = new TaskCompletionSource();
        var http = Book().On("GET", "/api/book/7/3", Gated(gate));
        using var provider = this.Provider(http);

        var download = provider.GetPageFileAsync(7, 3);
        await Eventually(() => http.CallsTo("GET", "/api/book/7/3") == 1);
        provider.Release(7);

        await Expect.ThrowsAsync<OperationCanceledException>(() => download);
    }

    private string _cacheDir = string.Empty;
}
