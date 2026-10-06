using ShortBox.Azure;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ShortBox.Acquisition.Test;

/// <summary>Plays canned replies per route. Each route's last step repeats once its earlier steps are used up.</summary>
internal sealed class ScriptedHandler : HttpMessageHandler, IHttpClientFactory
{
    public List<string> Calls { get; } = [];

    public int CallsTo(string method, string path) => this.Calls.Count(c => c.StartsWith($"{method} {path}"));

    public ScriptedHandler On(string method, string path, params Func<Task<HttpResponseMessage>>[] steps) =>
        this.On(method, path, [.. steps.Select(step => (Func<CancellationToken, Task<HttpResponseMessage>>)(_ => step()))]);

    /// <summary>Steps that receive the request's cancellation token, for tests that watch a download being abandoned.</summary>
    public ScriptedHandler On(string method, string path, params Func<CancellationToken, Task<HttpResponseMessage>>[] steps)
    {
        _routes[$"{method} {path}"] = new Queue<Func<CancellationToken, Task<HttpResponseMessage>>>(steps);
        return this;
    }

    /// <summary>A response is disposed once the client has read it, so each use is rebuilt from a snapshot of the original.</summary>
    public ScriptedHandler On(string method, string path, params HttpResponseMessage[] replies) =>
        this.On(method, path, [.. replies.Select(Snapshot).Select(copy => (Func<Task<HttpResponseMessage>>)(() => Task.FromResult(copy())))]);

    private static Func<HttpResponseMessage> Snapshot(HttpResponseMessage original)
    {
        var body = original.Content?.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        return () =>
        {
            var copy = new HttpResponseMessage(original.StatusCode);
            if (body is not null)
            {
                copy.Content = new ByteArrayContent(body);
                foreach (var header in original.Content!.Headers.Where(h => h.Key != "Content-Length"))
                {
                    copy.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            copy.Headers.RetryAfter = original.Headers.RetryAfter;
            return copy;
        };
    }

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new("http://test/") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        Func<CancellationToken, Task<HttpResponseMessage>> step;
        lock (_routes)
        {
            this.Calls.Add($"{request.Method} {request.RequestUri.PathAndQuery}");
            if (!_routes.TryGetValue($"{request.Method} {path}", out var queue))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            step = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        }

        return await step(cancellationToken).ConfigureAwait(false);
    }

    private readonly Dictionary<string, Queue<Func<CancellationToken, Task<HttpResponseMessage>>>> _routes = [];
}

[TestClass]
public class PageProviderTests
{
    private static HttpResponseMessage Json(HttpStatusCode code, string body, int? retryAfter = null)
    {
        var response = new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (retryAfter is { } seconds)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        }

        return response;
    }

    private static HttpResponseMessage Ready(int pages = 27) => Json(HttpStatusCode.OK, $$"""{"status":"ready","pageCount":{{pages}},"error":null}""");
    private static HttpResponseMessage Pending(int? retryAfter = 2) => Json(HttpStatusCode.Accepted, """{"status":"pending","pageCount":null,"error":null}""", retryAfter);
    private static HttpResponseMessage StatusPending() => Json(HttpStatusCode.OK, """{"status":"pending","pageCount":null,"error":null}""");
    private static HttpResponseMessage Failed(string error) => Json(HttpStatusCode.OK, $$"""{"status":"failed","pageCount":null,"error":"{{error}}"}""");

    private static HttpResponseMessage Image(byte[] bytes, string contentType = "image/jpeg")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return response;
    }

    [TestInitialize]
    public void CreateCacheFolder()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), $"pages-{Guid.NewGuid():N}");
        _delays = [];
    }

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
        var options = new PageProviderOptions
        {
            CacheDirectory = _cacheDir,
            Delay = (span, token) =>
            {
                _delays.Add(span);
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
        };
        tweak?.Invoke(options);
        return new PageProvider(http, options);
    }

    private sealed class Sink : IProgress<PrepareProgress>
    {
        public List<PrepareProgress> Reports { get; } = [];
        public void Report(PrepareProgress value) => this.Reports.Add(value);
    }

    [TestMethod]
    public async Task ReadyBookReturnsItsPageCountWithoutPolling()
    {
        var http = new ScriptedHandler().On("POST", "/api/book/7/prepare", Ready(27));
        using var provider = this.Provider(http);

        var info = await provider.PrepareAsync(7);

        Assert.AreEqual(27, info.PageCount);
        Assert.AreEqual(1, http.Calls.Count);
        Assert.AreEqual(0, _delays.Count);
    }

    [TestMethod]
    public async Task ColdBookIsPolledUntilReadyAndProgressIsReported()
    {
        var http = new ScriptedHandler()
            .On("POST", "/api/book/7/prepare", Pending(retryAfter: null))
            .On("GET", "/api/book/7/status", StatusPending(), StatusPending(), Ready(102));
        using var provider = this.Provider(http);
        var sink = new Sink();

        var info = await provider.PrepareAsync(7, sink);

        Assert.AreEqual(102, info.PageCount);
        Assert.AreEqual(3, http.CallsTo("GET", "/api/book/7/status"));
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2) }, _delays);
        Assert.AreEqual(PrepareStage.Starting, sink.Reports[0].Stage);
        Assert.IsTrue(sink.Reports.Skip(1).All(r => r.Stage == PrepareStage.Extracting));
    }

    [TestMethod]
    public async Task ServerRetryAfterSetsThePollIntervalWithinLimits()
    {
        var http = new ScriptedHandler()
            .On("POST", "/api/book/7/prepare", Pending(retryAfter: 5))
            .On("GET", "/api/book/7/status", Ready());
        using var provider = this.Provider(http);

        await provider.PrepareAsync(7);

        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(5) }, _delays);
    }

    [TestMethod]
    public async Task FailedExtractionSurfacesTheServerMessageAndTheNextPrepareStartsAgain()
    {
        var http = new ScriptedHandler()
            .On("POST", "/api/book/7/prepare", Pending())
            .On("GET", "/api/book/7/status", Failed("corrupt archive"));
        using var provider = this.Provider(http);

        var ex = await Expect.ThrowsAsync<PageLoadException>(() => provider.PrepareAsync(7));
        Assert.AreEqual(PageLoadFailure.PreparationFailed, ex.Kind);
        Assert.AreEqual("corrupt archive", ex.Message);
        Assert.IsTrue(ex.CanRetry);

        await Expect.ThrowsAsync<PageLoadException>(() => provider.PrepareAsync(7));
        Assert.AreEqual(2, http.CallsTo("POST", "/api/book/7/prepare"));
    }

    [TestMethod]
    public async Task UnknownBookIsNotRetried()
    {
        var http = new ScriptedHandler().On("POST", "/api/book/7/prepare", new HttpResponseMessage(HttpStatusCode.NotFound));
        using var provider = this.Provider(http);

        var ex = await Expect.ThrowsAsync<PageLoadException>(() => provider.PrepareAsync(7));

        Assert.AreEqual(PageLoadFailure.NotFound, ex.Kind);
        Assert.IsFalse(ex.CanRetry);
        Assert.AreEqual(1, http.Calls.Count);
    }

    [TestMethod]
    public async Task ServerErrorsAreRetriedWithBackoff()
    {
        var http = new ScriptedHandler().On("POST", "/api/book/7/prepare",
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), new HttpResponseMessage(HttpStatusCode.BadGateway), Ready());
        using var provider = this.Provider(http);

        await provider.PrepareAsync(7);

        Assert.AreEqual(3, http.Calls.Count);
        Assert.AreEqual(2, _delays.Count);
        Assert.IsTrue(_delays[0] >= TimeSpan.FromSeconds(1) && _delays[0] <= TimeSpan.FromSeconds(1.25), $"{_delays[0]}");
        Assert.IsTrue(_delays[1] >= TimeSpan.FromSeconds(2) && _delays[1] <= TimeSpan.FromSeconds(2.5), $"{_delays[1]}");
    }

    [TestMethod]
    public async Task NetworkErrorsGiveUpAfterTheConfiguredAttempts()
    {
        var http = new ScriptedHandler().On("POST", "/api/book/7/prepare", () => throw new HttpRequestException("offline"));
        using var provider = this.Provider(http);

        var ex = await Expect.ThrowsAsync<PageLoadException>(() => provider.PrepareAsync(7));

        Assert.AreEqual(PageLoadFailure.Network, ex.Kind);
        Assert.AreEqual(3, http.Calls.Count);
    }

    [TestMethod]
    public async Task ABookThatNeverFinishesTimesOut()
    {
        var http = new ScriptedHandler()
            .On("POST", "/api/book/7/prepare", Pending())
            .On("GET", "/api/book/7/status", StatusPending());
        using var provider = this.Provider(http, o =>
        {
            o.PrepareTimeout = TimeSpan.FromMilliseconds(150);
            o.Delay = (_, token) => Task.Delay(10, token);
        });

        var ex = await Expect.ThrowsAsync<PageLoadException>(() => provider.PrepareAsync(7));

        Assert.AreEqual(PageLoadFailure.Timeout, ex.Kind);
    }

    [TestMethod]
    public async Task ConcurrentCallersShareOnePrepareAndALateJoinerSeesProgress()
    {
        var gate = new TaskCompletionSource();
        var http = new ScriptedHandler().On("POST", "/api/book/7/prepare", async () =>
        {
            await gate.Task;
            return Ready(9);
        });
        using var provider = this.Provider(http);
        var first = new Sink();
        var late = new Sink();

        var a = provider.PrepareAsync(7, first);
        var b = provider.PrepareAsync(7, late);
        gate.SetResult();

        Assert.AreEqual(9, (await a).PageCount);
        Assert.AreEqual(9, (await b).PageCount);
        Assert.AreEqual(1, http.CallsTo("POST", "/api/book/7/prepare"));
        Assert.AreEqual(1, late.Reports.Count, "A caller who joins late is told the latest state.");
    }

    [TestMethod]
    public async Task ACallerWhoCancelsDoesNotStopTheSharedPrepare()
    {
        var gate = new TaskCompletionSource();
        var http = new ScriptedHandler().On("POST", "/api/book/7/prepare", async () =>
        {
            await gate.Task;
            return Ready(9);
        });
        using var provider = this.Provider(http);
        using var cancel = new CancellationTokenSource();

        var a = provider.PrepareAsync(7, null, cancel.Token);
        var b = provider.PrepareAsync(7);
        cancel.Cancel();
        await Expect.ThrowsAsync<OperationCanceledException>(() => a);
        gate.SetResult();

        Assert.AreEqual(9, (await b).PageCount);
        Assert.AreEqual(1, http.CallsTo("POST", "/api/book/7/prepare"));
    }

    [TestMethod]
    public async Task APageIsDownloadedOnceThenServedFromDisk()
    {
        byte[] bytes = [1, 2, 3, 4];
        var http = new ScriptedHandler()
            .On("POST", "/api/book/7/prepare", Ready())
            .On("GET", "/api/book/7/3", Image(bytes));
        using var provider = this.Provider(http);

        var path = await provider.GetPageFileAsync(7, 3);
        var calls = http.Calls.Count;
        var again = await provider.GetPageFileAsync(7, 3);

        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));
        Assert.AreEqual(path, again);
        Assert.AreEqual(calls, http.Calls.Count, "A cached page needs no network.");
        Assert.IsTrue(http.Calls.Contains("GET /api/book/7/3?wait=false"), "Pages must not use the blocking route.");
    }

    [TestMethod]
    public async Task APageRequestedWhileTheBookExtractsWaitsForIt()
    {
        var http = new ScriptedHandler()
            .On("POST", "/api/book/7/prepare", Pending())
            .On("GET", "/api/book/7/status", StatusPending(), Ready())
            .On("GET", "/api/book/7/0", Image([9]));
        using var provider = this.Provider(http);

        var path = await provider.GetPageFileAsync(7, 0);

        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual(2, http.CallsTo("GET", "/api/book/7/status"));
    }

    [TestMethod]
    public async Task ABookTheServerDroppedIsPreparedAgain()
    {
        var http = new ScriptedHandler()
            .On("POST", "/api/book/7/prepare", Ready())
            .On("GET", "/api/book/7/0", Pending(), Image([9]));
        using var provider = this.Provider(http);

        var path = await provider.GetPageFileAsync(7, 0);

        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual(2, http.CallsTo("POST", "/api/book/7/prepare"));
    }

    [TestMethod]
    public async Task ABookThatStaysUnpreparedStopsAfterTheRePrepareLimit()
    {
        var http = new ScriptedHandler()
            .On("POST", "/api/book/7/prepare", Ready())
            .On("GET", "/api/book/7/0", Pending());
        using var provider = this.Provider(http);

        var ex = await Expect.ThrowsAsync<PageLoadException>(() => provider.GetPageFileAsync(7, 0));

        Assert.AreEqual(PageLoadFailure.NotReady, ex.Kind);
        Assert.AreEqual(3, http.CallsTo("POST", "/api/book/7/prepare"));
    }

    [TestMethod]
    public async Task ANonImageAnswerIsNeitherCachedNorRetried()
    {
        var http = new ScriptedHandler()
            .On("POST", "/api/book/7/prepare", Ready())
            .On("GET", "/api/book/7/0", Image(Encoding.UTF8.GetBytes("<html>sign in</html>"), "text/html"));
        using var provider = this.Provider(http);

        var ex = await Expect.ThrowsAsync<PageLoadException>(() => provider.GetPageFileAsync(7, 0));

        Assert.AreEqual(PageLoadFailure.InvalidResponse, ex.Kind);
        Assert.IsFalse(ex.CanRetry);
        Assert.AreEqual(1, http.CallsTo("GET", "/api/book/7/0"));
        Assert.IsFalse(Directory.Exists(_cacheDir) && Directory.EnumerateFiles(_cacheDir, "*", SearchOption.AllDirectories).Any());
    }

    [TestMethod]
    public async Task APageOutsideTheBookIsNotFound()
    {
        var http = new ScriptedHandler()
            .On("POST", "/api/book/7/prepare", Ready())
            .On("GET", "/api/book/7/99", new HttpResponseMessage(HttpStatusCode.NotFound));
        using var provider = this.Provider(http);

        var ex = await Expect.ThrowsAsync<PageLoadException>(() => provider.GetPageFileAsync(7, 99));

        Assert.AreEqual(PageLoadFailure.NotFound, ex.Kind);
    }

    [TestMethod]
    public async Task ARejectedKeyIsReportedAndNotRetried()
    {
        var http = new ScriptedHandler().On("POST", "/api/book/7/prepare", new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var provider = this.Provider(http);

        var ex = await Expect.ThrowsAsync<PageLoadException>(() => provider.PrepareAsync(7));

        Assert.AreEqual(PageLoadFailure.Unauthorized, ex.Kind);
        Assert.IsFalse(ex.CanRetry);
        Assert.AreEqual(1, http.Calls.Count);
    }

    private string _cacheDir = string.Empty;
    private List<TimeSpan> _delays = [];
}

[TestClass]
public class PageDiskCacheTests
{
    [TestInitialize]
    public void CreateFolder() => _root = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid():N}");

    [TestCleanup]
    public void DeleteFolder()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [TestMethod]
    public async Task AStoredPageIsFoundAndLeavesNoTemporaryFile()
    {
        var cache = new PageDiskCache(_root, 1000);

        var path = await cache.StoreAsync(5, 2, new MemoryStream([1, 2, 3]), default);

        Assert.IsTrue(cache.TryGet(5, 2, out var found));
        Assert.AreEqual(path, found);
        Assert.IsFalse(cache.TryGet(5, 3, out _));
        Assert.AreEqual(1, Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Count());
    }

    [TestMethod]
    public async Task AFailedDownloadLeavesNothingBehind()
    {
        var cache = new PageDiskCache(_root, 1000);

        await Expect.ThrowsAsync<IOException>(() => cache.StoreAsync(5, 2, new FailingStream(), default));

        Assert.IsFalse(cache.TryGet(5, 2, out _));
        Assert.AreEqual(0, Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Count());
    }

    [TestMethod]
    public async Task TrimmingDeletesTheLeastRecentlyUsedPagesFirst()
    {
        var cache = new PageDiskCache(_root, maxBytes: 250);
        var oldest = await cache.StoreAsync(1, 0, new MemoryStream(new byte[100]), default);
        var middle = await cache.StoreAsync(1, 1, new MemoryStream(new byte[100]), default);
        var newest = await cache.StoreAsync(1, 2, new MemoryStream(new byte[100]), default);
        File.SetLastWriteTimeUtc(oldest, DateTime.UtcNow.AddHours(-3));
        File.SetLastWriteTimeUtc(middle, DateTime.UtcNow.AddHours(-2));
        File.SetLastWriteTimeUtc(newest, DateTime.UtcNow.AddHours(-1));

        // Reading the oldest page makes it the most recent.
        Assert.IsTrue(cache.TryGet(1, 0, out _));
        cache.Trim();

        Assert.IsTrue(File.Exists(oldest));
        Assert.IsFalse(File.Exists(middle));
        Assert.IsTrue(File.Exists(newest));
    }

    private sealed class FailingStream : MemoryStream
    {
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
            throw new IOException("connection reset");
    }

    private string _root = string.Empty;
}

internal static class Expect
{
    /// <summary>Awaits <paramref name="action"/> and returns the exception it throws (this MSTest version has no async Throws).</summary>
    public static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try
        {
            await action();
        }
        catch (T ex)
        {
            return ex;
        }

        throw new AssertFailedException($"Expected {typeof(T).Name}, but nothing was thrown.");
    }
}
