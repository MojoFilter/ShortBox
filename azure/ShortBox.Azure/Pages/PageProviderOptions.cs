namespace ShortBox.Azure;

public sealed class PageProviderOptions
{
    /// <summary>Where downloaded pages are kept. The app passes its cache directory (<c>FileSystem.CacheDirectory</c>).</summary>
    public required string CacheDirectory { get; set; }

    /// <summary>Least recently used pages are deleted once the cache grows past this.</summary>
    public long MaxCacheBytes { get; set; } = 512L * 1024 * 1024;

    /// <summary>How long to wait between status checks while a book extracts. A server Retry-After overrides it.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Total time to wait for a cold book. Extraction runs about 0.6s a page, and the first queue poll can add a minute.</summary>
    public TimeSpan PrepareTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Limit for a single request, including a page download.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Attempts per request for network errors, timeouts, 5xx, 408 and 429.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>First retry delay; it doubles on each further attempt.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>How often a page request that finds its book gone (202) may re-prepare it before giving up.</summary>
    public int MaxRePrepares { get; set; } = 2;

    /// <summary>Prefetch downloads that may run at once. None start while a page the reader is waiting on is downloading.</summary>
    public int MaxPrefetchDownloads { get; set; } = 2;

    /// <summary>Pages to prefetch beyond the current one, in the direction of travel.</summary>
    public int PrefetchAhead { get; set; } = 3;

    /// <summary>Pages to prefetch behind the current one, against the direction of travel.</summary>
    public int PrefetchBehind { get; set; } = 1;

    /// <summary>Replaces <see cref="Task.Delay(TimeSpan, CancellationToken)"/>. Exists so tests need not wait.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;
}
