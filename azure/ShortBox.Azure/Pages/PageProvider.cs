using System.Diagnostics;

namespace ShortBox.Azure;

/// <summary>
/// Fetches book pages for the reader: asks the server to extract a book, waits for it patiently, downloads pages with
/// retry, and keeps them in a disk cache. Uses the page routes' <c>wait=false</c> mode, so no request blocks on extraction.
/// </summary>
public sealed class PageProvider : IPageProvider, IDisposable
{
    public PageProvider(IHttpClientFactory clientFactory, PageProviderOptions options)
    {
        _clientFactory = clientFactory;
        _options = options;
        _cache = new PageDiskCache(Path.Combine(options.CacheDirectory, "pages"), options.MaxCacheBytes);
        _scheduler = new PageDownloadScheduler(Math.Max(1, options.MaxPrefetchDownloads));
    }

    public Task<BookReadyInfo> PrepareAsync(int bookId, IProgress<PrepareProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Preparation preparation;
        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PageProvider));
            }

            // A failed or cancelled attempt may not have been forgotten yet, but it must never be handed to a retry.
            if (!_preparations.TryGetValue(bookId, out preparation!) || (preparation.Task.IsCompleted && !preparation.Task.IsCompletedSuccessfully))
            {
                preparation = new Preparation();
                _preparations[bookId] = preparation;
                preparation.Task = Task.Run(() => this.RunPreparationAsync(bookId, preparation));
                _ = preparation.Task.ContinueWith(
                    finished =>
                    {
                        _ = finished.Exception; // observed here; callers still waiting see it through their own await
                        this.Forget(bookId, preparation);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.NotOnRanToCompletion,
                    TaskScheduler.Default);
            }
        }

        if (progress is not null)
        {
            preparation.Subscribe(progress);
        }

        // The wait is shared, so one caller giving up must not stop it for the rest.
        return preparation.Task.WaitAsync(cancellationToken);
    }

    public Task<string> GetPageFileAsync(int bookId, int pageIndex, CancellationToken cancellationToken = default) =>
        this.LoadPageAsync(bookId, pageIndex, visible: true, cancellationToken);

    public Task<string> PrefetchPageAsync(int bookId, int pageIndex, CancellationToken cancellationToken = default) =>
        this.LoadPageAsync(bookId, pageIndex, visible: false, cancellationToken);

    /// <summary>
    /// Callers asking for one page share a single download. It is abandoned when the last of them stops waiting, and a visible
    /// caller joining a prefetch promotes it.
    /// </summary>
    private async Task<string> LoadPageAsync(int bookId, int pageIndex, bool visible, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_cache.TryGet(bookId, pageIndex, out var cached))
        {
            return cached;
        }

        PageDownload download;
        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PageProvider));
            }

            var key = (bookId, pageIndex);
            // A failed download may not have been forgotten yet, but it must never be handed to a new caller.
            if (!_downloads.TryGetValue(key, out download!) || (download.Task.IsCompleted && !download.Task.IsCompletedSuccessfully))
            {
                var created = new PageDownload(key, _scheduler.Enqueue(visible));
                download = created;
                _downloads[key] = created;
                created.Task = Task.Run(() => this.RunDownloadAsync(created));
                _ = created.Task.ContinueWith(finished => _ = finished.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            }
            else if (visible)
            {
                _scheduler.Promote(download.Ticket);
            }

            download.Waiters++;
        }

        try
        {
            return await download.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.Leave(download);
        }
    }

    private void Leave(PageDownload download)
    {
        lock (_gate)
        {
            if (--download.Waiters > 0 || download.Task.IsCompleted)
            {
                return;
            }

            // Nobody is waiting any more: stop the download, and let the next caller start a fresh one.
            if (_downloads.TryGetValue(download.Key, out var current) && ReferenceEquals(current, download))
            {
                _downloads.Remove(download.Key);
            }

            download.Cancel();
        }
    }

    private async Task<string> RunDownloadAsync(PageDownload download)
    {
        var (bookId, pageIndex) = download.Key;
        var token = download.Token;
        try
        {
            await download.Ticket.Started.WaitAsync(token).ConfigureAwait(false);
            for (var rePrepares = 0; ; rePrepares++)
            {
                await this.PrepareAsync(bookId, null, token).ConfigureAwait(false);
                var path = await this.RetryAsync(t => this.DownloadPageAsync(bookId, pageIndex, t), token).ConfigureAwait(false);
                if (path is not null)
                {
                    return path;
                }

                // 202: the server no longer has the book extracted (a decache since we prepared it). Start over, but only for
                // a page somebody is waiting on; a speculative load must not make the server extract a book again.
                if (rePrepares >= (download.Ticket.Visible ? _options.MaxRePrepares : 0))
                {
                    throw new PageLoadException(PageLoadFailure.NotReady, "The server did not have the book ready.");
                }

                this.Forget(bookId, null);
            }
        }
        finally
        {
            _scheduler.Finish(download.Ticket);
            lock (_gate)
            {
                if (_downloads.TryGetValue(download.Key, out var current) && ReferenceEquals(current, download))
                {
                    _downloads.Remove(download.Key);
                }
            }
        }
    }

    public void Release(int bookId)
    {
        Preparation? preparation;
        lock (_gate)
        {
            if (_preparations.Remove(bookId, out preparation))
            {
                preparation.Cancel();
            }

            foreach (var key in _downloads.Keys.Where(k => k.BookId == bookId).ToArray())
            {
                _downloads.Remove(key, out var download);
                download!.Cancel();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (var preparation in _preparations.Values)
            {
                preparation.Cancel();
            }

            _preparations.Clear();
            foreach (var download in _downloads.Values)
            {
                download.Cancel();
            }

            _downloads.Clear();
        }
    }

    private async Task<BookReadyInfo> RunPreparationAsync(int bookId, Preparation preparation)
    {
        var clock = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(preparation.Token);
        timeout.CancelAfter(_options.PrepareTimeout);
        var token = timeout.Token;
        try
        {
            preparation.Report(new(PrepareStage.Starting, clock.Elapsed));
            var reply = await this.RetryAsync(t => this.SendStatusAsync(HttpMethod.Post, $"api/book/{bookId}/prepare", t), token).ConfigureAwait(false);
            while (true)
            {
                switch (reply)
                {
                    case { Status: "ready" } or { Code: HttpStatusCode.OK, Status: null }:
                        return new(reply.PageCount ?? throw new PageLoadException(PageLoadFailure.InvalidResponse, "The server did not report a page count."));
                    case { Status: "failed" }:
                        throw new PageLoadException(PageLoadFailure.PreparationFailed, reply.Error ?? "The server could not prepare the book.");
                }

                preparation.Report(new(PrepareStage.Extracting, clock.Elapsed));
                await _options.Delay(reply.RetryAfter ?? _options.PollInterval, token).ConfigureAwait(false);
                reply = await this.RetryAsync(t => this.SendStatusAsync(HttpMethod.Get, $"api/book/{bookId}/status", t), token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !preparation.Token.IsCancellationRequested)
        {
            throw new PageLoadException(PageLoadFailure.Timeout, "The book took too long to prepare.");
        }
    }

    private async Task<StatusReply> SendStatusAsync(HttpMethod method, string uri, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, uri);
        using var response = await _clientFactory.CreateClient(nameof(ShortBoxAzureClient))
            .SendAsync(request, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false);
        if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Accepted))
        {
            throw FailureFor(response);
        }

        var body = await ReadBodyAsync(response, token).ConfigureAwait(false);
        // A 202 means "not ready yet" whatever the body says; only a 200 can be ready.
        var status = response.StatusCode == HttpStatusCode.OK ? body?.Status : "pending";
        return new(response.StatusCode, status, body?.PageCount, body?.Error, ClampRetryAfter(response));
    }

    /// <summary>Returns the cached file, or null when the server says the book is not ready.</summary>
    private async Task<string?> DownloadPageAsync(int bookId, int pageIndex, CancellationToken token)
    {
        using var response = await _clientFactory.CreateClient(nameof(ShortBoxAzureClient))
            .GetAsync($"api/book/{bookId}/{pageIndex}?wait=false", HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        switch (response.StatusCode)
        {
            case HttpStatusCode.Accepted:
                return null;
            case HttpStatusCode.OK:
                break;
            default:
                throw FailureFor(response);
        }

        // Anything else with a 200 (a captive portal's login page, say) must not be cached as a page.
        if (response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
        {
            throw new PageLoadException(PageLoadFailure.InvalidResponse, "The server did not send an image.");
        }

        await using var content = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        return await _cache.StoreAsync(bookId, pageIndex, content, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <paramref name="attempt"/>, trying again with backoff when it fails in a way that may pass (network errors, timeouts,
    /// 5xx, 408, 429). Anything that is a <see cref="PageLoadException"/> is final.
    /// </summary>
    private async Task<T> RetryAsync<T>(Func<CancellationToken, Task<T>> attempt, CancellationToken token)
    {
        for (var attemptNumber = 1; ; attemptNumber++)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
            limit.CancelAfter(_options.RequestTimeout);
            try
            {
                return await attempt(limit.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!token.IsCancellationRequested && ex is HttpRequestException or IOException or TransientStatusException or OperationCanceledException)
            {
                if (attemptNumber >= _options.MaxAttempts)
                {
                    var kind = ex is OperationCanceledException ? PageLoadFailure.Timeout : PageLoadFailure.Network;
                    throw new PageLoadException(kind, kind == PageLoadFailure.Timeout ? "The server took too long to answer." : "Could not reach the server.", ex);
                }

                var backoff = TimeSpan.FromTicks(_options.RetryBaseDelay.Ticks << (attemptNumber - 1));
                var jitter = TimeSpan.FromTicks((long)(backoff.Ticks * 0.25 * Random.Shared.NextDouble()));
                await _options.Delay(backoff + jitter, token).ConfigureAwait(false);
            }
        }
    }

    private static Exception FailureFor(HttpResponseMessage response) => response.StatusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new PageLoadException(PageLoadFailure.Unauthorized, "The server rejected the app's key."),
        HttpStatusCode.NotFound => new PageLoadException(PageLoadFailure.NotFound, "That book or page was not found."),
        HttpStatusCode.UnprocessableEntity => new PageLoadException(PageLoadFailure.NoImages, "That book has no page images."),
        HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError
            => new TransientStatusException(response.StatusCode),
        _ => new PageLoadException(PageLoadFailure.InvalidResponse, $"The server answered {(int)response.StatusCode}."),
    };

    private static async Task<StatusBody?> ReadBodyAsync(HttpResponseMessage response, CancellationToken token)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync(PageJsonContext.Default.StatusBody, token).ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static TimeSpan? ClampRetryAfter(HttpResponseMessage response) =>
        response.Headers.RetryAfter?.Delta is { } delta
            ? TimeSpan.FromTicks(Math.Clamp(delta.Ticks, MinPoll.Ticks, MaxPoll.Ticks))
            : null;

    private void Forget(int bookId, Preparation? only)
    {
        lock (_gate)
        {
            if (_preparations.TryGetValue(bookId, out var current) && (only is null || ReferenceEquals(current, only)))
            {
                _preparations.Remove(bookId);
            }
        }
    }

    private static readonly TimeSpan MinPoll = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxPoll = TimeSpan.FromSeconds(10);


    private sealed record StatusReply(HttpStatusCode Code, string? Status, int? PageCount, string? Error, TimeSpan? RetryAfter);

    private sealed class TransientStatusException(HttpStatusCode status) : Exception($"The server answered {(int)status}.");

    /// <summary>One shared download of a page, with the number of callers still waiting for it.</summary>
    private sealed class PageDownload((int BookId, int PageIndex) key, PageDownloadScheduler.Ticket ticket)
    {
        public (int BookId, int PageIndex) Key { get; } = key;

        public PageDownloadScheduler.Ticket Ticket { get; } = ticket;

        public Task<string> Task { get; set; } = null!;

        public int Waiters { get; set; }

        public CancellationToken Token => _cts.Token;

        public void Cancel() => _cts.Cancel();

        private readonly CancellationTokenSource _cts = new();
    }

    /// <summary>One shared wait for a book to be ready, with every interested caller's progress sink.</summary>
    private sealed class Preparation
    {
        public Task<BookReadyInfo> Task { get; set; } = null!;

        public CancellationToken Token => _cts.Token;

        public void Cancel() => _cts.Cancel();

        public void Report(PrepareProgress progress)
        {
            IProgress<PrepareProgress>[] sinks;
            lock (_sync)
            {
                _latest = progress;
                sinks = [.. _sinks];
            }

            foreach (var sink in sinks)
            {
                sink.Report(progress);
            }
        }

        public void Subscribe(IProgress<PrepareProgress> sink)
        {
            PrepareProgress? latest;
            lock (_sync)
            {
                _sinks.Add(sink);
                latest = _latest;
            }

            if (latest is not null)
            {
                sink.Report(latest);
            }
        }

        private readonly CancellationTokenSource _cts = new();
        private readonly List<IProgress<PrepareProgress>> _sinks = [];
        private readonly object _sync = new();
        private PrepareProgress? _latest;
    }

    private readonly IHttpClientFactory _clientFactory;
    private readonly PageProviderOptions _options;
    private readonly PageDiskCache _cache;
    private readonly PageDownloadScheduler _scheduler;
    private readonly Dictionary<int, Preparation> _preparations = [];
    private readonly Dictionary<(int BookId, int PageIndex), PageDownload> _downloads = [];
    private readonly object _gate = new();
    private bool _disposed;
}
