using Microsoft.Extensions.Logging.Abstractions;
using ShortBox.Azure.Services;
using ShortBox.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class PageImageExtensionsTests
{
    [TestMethod]
    public void ArchiverMetadataAndNonImagesAreNotPages()
    {
        Assert.IsTrue(PageImageExtensions.IsPageImage("01.jpg"));
        Assert.IsTrue(PageImageExtensions.IsPageImage("Chapter 1/01.PNG"));
        Assert.IsFalse(PageImageExtensions.IsPageImage("ComicInfo.xml"));
        Assert.IsFalse(PageImageExtensions.IsPageImage("Chapter 1/"));
        Assert.IsFalse(PageImageExtensions.IsPageImage("__MACOSX/Chapter 1/._01.jpg"));
        Assert.IsFalse(PageImageExtensions.IsPageImage("Chapter 1/._01.jpg"));
        Assert.IsFalse(PageImageExtensions.IsPageImage(@"__MACOSX\01.jpg"));
        Assert.IsFalse(PageImageExtensions.IsPageImage(null));
    }

    [TestMethod]
    public void PagesAreOrderedByFullPath()
    {
        var names = new[] { "b/01.jpg", "a/02.jpg", "cover.jpg", "a/01.jpg", "info.txt" };

        CollectionAssert.AreEqual(
            new[] { "a/01.jpg", "a/02.jpg", "b/01.jpg", "cover.jpg" },
            names.InReadingOrder(n => n).ToArray());
    }

    [TestMethod]
    public void ContentTypeFollowsTheExtension()
    {
        Assert.AreEqual("image/jpeg", PageImageExtensions.GetContentType("a.JPG"));
        Assert.AreEqual("image/jpeg", PageImageExtensions.GetContentType("a.jpeg"));
        Assert.AreEqual("image/png", PageImageExtensions.GetContentType("a.png"));
        Assert.AreEqual("image/gif", PageImageExtensions.GetContentType("a.gif"));
        Assert.AreEqual("image/webp", PageImageExtensions.GetContentType("a.webp"));
    }
}

[TestClass]
public class ArchiveReadingTests
{
    [TestMethod]
    public async Task PageCountIsTheNumberOfImagesNotTheNumberOfEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cbz");
        try
        {
            await File.WriteAllBytesAsync(path, PageFixtures.Zip(
                ("ComicInfo.xml", "<ComicInfo/>"u8.ToArray()),
                ("01.jpg", [1]),
                ("02.jpg", [2]),
                ("Chapter 2/", []),
                ("Chapter 2/01.png", [3]),
                ("__MACOSX/._01.jpg", [4])));

            Assert.AreEqual(3, await new ZipReader().GetPageCountAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task SameNamedPagesInDifferentFoldersAreAllExtracted()
    {
        using var archive = new MemoryStream(PageFixtures.Zip(
            ("Chapter 2/01.jpg", [3]),
            ("Chapter 1/01.jpg", [1]),
            ("Chapter 1/02.jpg", [2])));

        var pages = await PageFixtures.CollectAsync(new ZipExtractor().ExtractPagesAsync(archive, default));

        CollectionAssert.AreEqual(
            new[] { "Chapter 1/01.jpg", "Chapter 1/02.jpg", "Chapter 2/01.jpg" },
            pages.Select(p => p.Name).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, pages.Select(p => p.Index).ToArray());
    }

    [TestMethod]
    public async Task ExtractingLeavesTheCallersStreamOpen()
    {
        using var archive = new MemoryStream(PageFixtures.Zip(("01.jpg", [1])));

        await PageFixtures.CollectAsync(new ZipExtractor().ExtractPagesAsync(archive, default));

        Assert.IsTrue(archive.CanRead);
    }
}

[TestClass]
public class PageExtractionTests
{
    [TestMethod]
    public async Task TheManifestDescribesEveryPage()
    {
        var uploads = new List<(string Blob, string ContentType, byte[] Bytes)>();
        var pages = Entries(("Chapter 1/01.jpg", PageFixtures.Png(30, 40)), ("Chapter 2/01.PNG", PageFixtures.Png(5, 6)));

        var manifest = await PageExtraction.ExtractAsync(pages, async (p, ct) =>
        {
            using var copy = new MemoryStream();
            await p.Content.CopyToAsync(copy, ct);
            uploads.Add((p.Blob, p.ContentType, copy.ToArray()));
        }, default);

        Assert.AreEqual(2, manifest.PageCount);
        Assert.AreEqual(new ManifestPage(0, "0000.jpg", "Chapter 1/01.jpg", "image/jpeg", 30, 40), manifest.Pages[0]);
        Assert.AreEqual(new ManifestPage(1, "0001.png", "Chapter 2/01.PNG", "image/png", 5, 6), manifest.Pages[1]);
        CollectionAssert.AreEqual(new[] { "0000.jpg", "0001.png" }, uploads.Select(u => u.Blob).ToArray());
        Assert.IsTrue(uploads[1].Bytes.Length > 0, "the upload received the page bytes");
    }

    [TestMethod]
    public async Task AnUnreadableImageIsStillStoredButHasNoDimensions()
    {
        var manifest = await PageExtraction.ExtractAsync(Entries(("01.jpg", [1, 2, 3])), (_, _) => Task.CompletedTask, default);

        Assert.AreEqual(1, manifest.PageCount);
        Assert.IsNull(manifest.Pages[0].Width);
        Assert.IsNull(manifest.Pages[0].Height);
    }

    [TestMethod]
    public async Task AnArchiveWithoutPagesIsRejected()
    {
        await PageFixtures.CatchAsync<InvalidDataException>(() =>
            PageExtraction.ExtractAsync(Entries(), (_, _) => Task.CompletedTask, default));
    }

    [TestMethod]
    public void TheManifestSurvivesJson()
    {
        var manifest = new PageManifest(PageManifest.CurrentVersion, [new(0, "0000.jpg", "a/01.jpg", "image/jpeg", 10, null)]);

        var parsed = PageManifest.TryParse(manifest.ToJson());

        Assert.IsNotNull(parsed);
        Assert.AreEqual(manifest.Pages[0], parsed.Pages[0]);
    }

    [TestMethod]
    public void UnusableManifestsAreNotReady()
    {
        Assert.IsNull(PageManifest.TryParse("not json"));
        Assert.IsNull(PageManifest.TryParse(""));
        Assert.IsNull(PageManifest.TryParse("""{"version":1,"pages":[]}"""));
        Assert.IsNull(PageManifest.TryParse("""{"version":99,"pages":[{"index":0,"blob":"0000.jpg","entry":"a.jpg","contentType":"image/jpeg"}]}"""));
    }

    private static IAsyncEnumerable<IPageEntry> Entries(params (string Name, byte[] Bytes)[] files) =>
        PageFixtures.AsAsync(files.Select((f, i) => (IPageEntry)new PageEntry(i, f.Name, () => new MemoryStream(f.Bytes))));
}

[TestClass]
public class PageCacheTests
{
    [TestMethod]
    public async Task TheFirstRequestExtractsAndTheManifestIsStoredAfterEveryPage()
    {
        var (cache, blobs, archives) = NewCache(("01.png", PageFixtures.Png(4, 3)), ("02.jpg", [9, 9]));

        var page = await cache.GetPageAsync(new(101), "a.cbz", 0, default);

        Assert.AreEqual("image/png", page.ContentType);
        CollectionAssert.AreEqual(new[] { "page 0000.png", "page 0001.jpg", "manifest", "delete-unlisted" }, blobs.Log.ToArray());
        Assert.AreEqual(1, archives.Calls);
    }

    [TestMethod]
    public async Task EachPageComesBackWithItsOwnContentType()
    {
        var (cache, _, _) = NewCache(("01.png", PageFixtures.Png(4, 3)), ("02.jpg", [9, 9]), ("03.webp", [8]));

        Assert.AreEqual("image/png", (await cache.GetPageAsync(new(102), "a.cbz", 0, default)).ContentType);
        Assert.AreEqual("image/jpeg", (await cache.GetPageAsync(new(102), "a.cbz", 1, default)).ContentType);
        Assert.AreEqual("image/webp", (await cache.GetPageAsync(new(102), "a.cbz", 2, default)).ContentType);
    }

    [TestMethod]
    public async Task ABookThatIsAlreadyReadyIsServedWithoutTouchingTheArchive()
    {
        var (cache, blobs, archives) = NewCache(("01.png", PageFixtures.Png(4, 3)));
        await cache.GetPageAsync(new(103), "a.cbz", 0, default);
        blobs.Log.Clear();

        await cache.GetPageAsync(new(103), "a.cbz", 0, default);

        Assert.AreEqual(1, archives.Calls);
        Assert.AreEqual(0, blobs.Log.Count);
    }

    [TestMethod]
    public async Task PagesWithoutAManifestAreAPartialExtractionAndAreNotServed()
    {
        var (cache, blobs, archives) = NewCache(("01.png", PageFixtures.Png(4, 3)), ("02.png", PageFixtures.Png(4, 3)));
        blobs.SeedStray(104, "0000.jpg", [0xFF]);       // what a crashed extraction leaves behind
        blobs.SeedStray(104, "old-style-name.jpg", [0xFF]);

        var page = await cache.GetPageAsync(new(104), "a.cbz", 0, default);

        Assert.AreEqual("image/png", page.ContentType, "the real first page, not the stray blob");
        Assert.AreEqual(1, archives.Calls);
        CollectionAssert.AreEquivalent(new[] { "0000.png", "0001.png", PageManifest.BlobName }, blobs.BlobNames(104).ToArray(), "strays are cleaned up");
    }

    [TestMethod]
    public async Task AFailedExtractionNeverMarksTheBookReadyAndTheNextRequestTriesAgain()
    {
        var (cache, blobs, archives) = NewCache(("01.png", PageFixtures.Png(4, 3)), ("02.png", PageFixtures.Png(4, 3)));
        blobs.FailPageWrite = "0001.png";

        await PageFixtures.CatchAsync<IOException>(() => cache.GetPageAsync(new(105), "a.cbz", 0, default));
        Assert.IsFalse(blobs.HasManifest(105));

        blobs.FailPageWrite = null;
        await cache.GetPageAsync(new(105), "a.cbz", 1, default);
        Assert.IsTrue(blobs.HasManifest(105));
        Assert.AreEqual(2, archives.Calls);
    }

    [TestMethod]
    public async Task ConcurrentRequestsForOneBookShareOneExtraction()
    {
        var gate = new TaskCompletionSource();
        var (cache, _, archives) = NewCache(("01.png", PageFixtures.Png(4, 3)), ("02.png", PageFixtures.Png(4, 3)));
        archives.Gate = gate.Task;

        var requests = Enumerable.Range(0, 4)
            .Select(i => cache.GetPageAsync(new(106), "a.cbz", i % 2, default))
            .ToArray();
        await PageFixtures.UntilAsync(() => archives.Calls > 0);
        await Task.Delay(50);
        gate.SetResult();
        var pages = await Task.WhenAll(requests);

        Assert.AreEqual(1, archives.Calls);
        Assert.AreEqual(4, pages.Length);
    }

    [TestMethod]
    public async Task ACallerWhoGivesUpDoesNotAbortTheExtractionOthersAreWaitingOn()
    {
        var gate = new TaskCompletionSource();
        var (cache, blobs, archives) = NewCache(("01.png", PageFixtures.Png(4, 3)));
        archives.Gate = gate.Task;
        using var impatient = new CancellationTokenSource();

        var gaveUp = cache.GetPageAsync(new(107), "a.cbz", 0, impatient.Token);
        var patient = cache.GetPageAsync(new(107), "a.cbz", 0, default);
        await PageFixtures.UntilAsync(() => archives.Calls > 0);
        impatient.Cancel();
        await PageFixtures.CatchAsync<OperationCanceledException>(() => gaveUp);
        gate.SetResult();

        var page = await patient;
        Assert.AreEqual("image/png", page.ContentType);
        Assert.IsTrue(blobs.HasManifest(107));
    }

    [TestMethod]
    public async Task APageOutsideTheBookIsNotFound()
    {
        var (cache, _, _) = NewCache(("01.png", PageFixtures.Png(4, 3)));

        await PageFixtures.CatchAsync<KeyNotFoundException>(() => cache.GetPageAsync(new(108), "a.cbz", 1, default));
        await PageFixtures.CatchAsync<KeyNotFoundException>(() => cache.GetPageAsync(new(108), "a.cbz", -1, default));
    }

    [TestMethod]
    public async Task AnUnextractedBookIsPendingAndStatusNeverExtractsIt()
    {
        var (cache, blobs, archives) = NewCache(("01.png", PageFixtures.Png(4, 3)));

        var status = await cache.GetStatusAsync(new(110), default);

        Assert.AreEqual(PageState.Pending, status.State);
        Assert.IsNull(status.PageCount);
        Assert.AreEqual(0, archives.Calls);
        Assert.IsFalse(blobs.HasManifest(110));
    }

    [TestMethod]
    public async Task ReadyPagesAreListedInReadingOrderWithTheirDimensions()
    {
        var (cache, _, _) = NewCache(
            ("Chapter 2/01.jpg", [9, 9]),
            ("Chapter 1/02.png", PageFixtures.Png(30, 20)),
            ("Chapter 1/01.png", PageFixtures.Png(10, 20)));
        await cache.PrepareAsync(new(114), "a.cbz", default);

        var pages = await cache.GetPagesAsync(new(114), default);

        Assert.AreEqual(PageState.Ready, pages.Status.State);
        Assert.AreEqual(3, pages.Status.PageCount);
        CollectionAssert.AreEqual(
            new[]
            {
                new PageInfo(0, "image/png", 10, 20),
                new PageInfo(1, "image/png", 30, 20),
                new PageInfo(2, "image/jpeg", null, null),
            },
            pages.Pages.ToArray());
        Assert.AreEqual(0.5, pages.Pages[0].AspectRatio);
        Assert.AreEqual(1.5, pages.Pages[1].AspectRatio);
        Assert.IsNull(pages.Pages[2].AspectRatio, "an unreadable image has no known shape");
    }

    [TestMethod]
    public async Task ThePageListNeverExtractsAndIsEmptyUntilTheBookIsReady()
    {
        var (cache, blobs, archives) = NewCache(("01.png", PageFixtures.Png(4, 3)));

        var pages = await cache.GetPagesAsync(new(115), default);

        Assert.AreEqual(PageState.Pending, pages.Status.State);
        Assert.AreEqual(0, pages.Pages.Count);
        Assert.AreEqual(0, archives.Calls);
        Assert.IsFalse(blobs.HasManifest(115));
    }

    [TestMethod]
    public async Task AFailedBookHasNoPageListAndSaysWhy()
    {
        var (cache, blobs, _) = NewCache(("01.png", PageFixtures.Png(4, 3)));
        blobs.FailPageWrite = "0000.png";
        await PageFixtures.CatchAsync<IOException>(() => cache.PrepareAsync(new(116), "a.cbz", default));

        var pages = await cache.GetPagesAsync(new(116), default);

        Assert.AreEqual(PageState.Failed, pages.Status.State);
        Assert.AreEqual("simulated storage failure", pages.Status.Error);
        Assert.AreEqual(0, pages.Pages.Count);
    }

    [TestMethod]
    public void AnEmptyOrZeroSizedImageHasNoAspectRatio()
    {
        Assert.IsNull(new PageInfo(0, "image/png", 0, 10).AspectRatio);
        Assert.IsNull(new PageInfo(0, "image/png", 10, 0).AspectRatio);
        Assert.IsNull(new PageInfo(0, "image/png", 10, null).AspectRatio);
    }

    [TestMethod]
    public async Task PreparingExtractsTheBookAndReportsItsRealPageCount()
    {
        var (cache, _, archives) = NewCache(("ComicInfo.xml", "<ComicInfo/>"u8.ToArray()), ("01.png", PageFixtures.Png(4, 3)), ("02.png", PageFixtures.Png(4, 3)));

        Assert.AreEqual(2, await cache.PrepareAsync(new(111), "a.cbz", default));

        var status = await cache.GetStatusAsync(new(111), default);
        Assert.AreEqual(PageState.Ready, status.State);
        Assert.AreEqual(2, status.PageCount);
        Assert.AreEqual(2, await cache.PrepareAsync(new(111), "a.cbz", default));
        Assert.AreEqual(1, archives.Calls, "preparing a ready book does not touch the archive");
    }

    [TestMethod]
    public async Task AFailedExtractionIsReportedAsFailedUntilTheFailureIsCleared()
    {
        var (cache, blobs, _) = NewCache(("01.png", PageFixtures.Png(4, 3)));
        blobs.FailPageWrite = "0000.png";

        await PageFixtures.CatchAsync<IOException>(() => cache.PrepareAsync(new(112), "a.cbz", default));

        var failed = await cache.GetStatusAsync(new(112), default);
        Assert.AreEqual(PageState.Failed, failed.State);
        Assert.AreEqual("simulated storage failure", failed.Error);

        await cache.ClearFailureAsync(new(112), default);
        Assert.AreEqual(PageState.Pending, (await cache.GetStatusAsync(new(112), default)).State);
    }

    [TestMethod]
    public async Task ARetryThatSucceedsLeavesNoFailureBehind()
    {
        var (cache, blobs, _) = NewCache(("01.png", PageFixtures.Png(4, 3)));
        blobs.FailPageWrite = "0000.png";
        await PageFixtures.CatchAsync<IOException>(() => cache.PrepareAsync(new(113), "a.cbz", default));

        blobs.FailPageWrite = null;
        await cache.PrepareAsync(new(113), "a.cbz", default);

        Assert.AreEqual(PageState.Ready, (await cache.GetStatusAsync(new(113), default)).State);
        Assert.IsNull(await blobs.ReadFailureAsync(new(113), default));
    }

    [TestMethod]
    public async Task AnArchiveWithoutPagesIsReportedAndNeverMarkedReady()
    {
        var (cache, blobs, _) = NewCache(("ComicInfo.xml", "<ComicInfo/>"u8.ToArray()));

        await PageFixtures.CatchAsync<InvalidDataException>(() => cache.GetPageAsync(new(109), "a.cbz", 0, default));

        Assert.IsFalse(blobs.HasManifest(109));
    }

    private static (AzureStoragePageCache Cache, FakePageBlobs Blobs, FakeArchiveStore Archives) NewCache(params (string Name, byte[] Bytes)[] files)
    {
        var blobs = new FakePageBlobs();
        var archives = new FakeArchiveStore(PageFixtures.Zip(files));
        var cache = new AzureStoragePageCache(
            null!, blobs, archives, new ArchiveBusiness(new ZipExtractor(), new RarExtractor()), null!, null!,
            NullLogger<AzureStoragePageCache>.Instance);
        return (cache, blobs, archives);
    }
}

internal class FakeArchiveStore(byte[] zip) : IArchiveStore
{
    public int Calls;
    public Task? Gate { get; set; }

    public async Task<Stream> GetArchiveAsync(string archiveName, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Calls);
        if (Gate is not null)
        {
            await Gate;
        }
        return new MemoryStream(zip);
    }
}

internal class FakePageBlobs : IPageBlobs
{
    public List<string> Log { get; } = [];
    public string? FailPageWrite { get; set; }

    public void SeedStray(int bookId, string blob, byte[] bytes) => _blobs[(bookId, blob)] = (bytes, "image/jpeg");

    public bool HasManifest(int bookId) => _manifests.ContainsKey(bookId);

    public IEnumerable<string> BlobNames(int bookId)
    {
        lock (_gate)
        {
            return _blobs.Keys.Where(k => k.BookId == bookId).Select(k => k.Blob)
                .Concat(_manifests.ContainsKey(bookId) ? [PageManifest.BlobName] : [])
                .ToList();
        }
    }

    public Task<PageManifest?> ReadManifestAsync(BookId bookId, CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult(_manifests.GetValueOrDefault(bookId.Value));
        }
    }

    public async Task WritePageAsync(BookId bookId, PageUpload page, CancellationToken ct)
    {
        if (page.Blob == FailPageWrite)
        {
            throw new IOException("simulated storage failure");
        }
        using var copy = new MemoryStream();
        await page.Content.CopyToAsync(copy, ct);
        lock (_gate)
        {
            _blobs[(bookId.Value, page.Blob)] = (copy.ToArray(), page.ContentType);
            Log.Add($"page {page.Blob}");
        }
    }

    public Task WriteManifestAsync(BookId bookId, PageManifest manifest, CancellationToken ct)
    {
        lock (_gate)
        {
            _manifests[bookId.Value] = manifest;
            Log.Add("manifest");
        }
        return Task.CompletedTask;
    }

    public Task DeleteUnlistedAsync(BookId bookId, PageManifest manifest, CancellationToken ct)
    {
        lock (_gate)
        {
            var keep = manifest.Pages.Select(p => p.Blob).ToHashSet();
            foreach (var key in _blobs.Keys.Where(k => k.BookId == bookId.Value && !keep.Contains(k.Blob)).ToList())
            {
                _blobs.Remove(key);
            }
            Log.Add("delete-unlisted");
        }
        return Task.CompletedTask;
    }

    public Task<string?> ReadFailureAsync(BookId bookId, CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult(_failures.GetValueOrDefault(bookId.Value));
        }
    }

    public Task WriteFailureAsync(BookId bookId, string message, CancellationToken ct)
    {
        lock (_gate)
        {
            _failures[bookId.Value] = message;
        }
        return Task.CompletedTask;
    }

    public Task ClearFailureAsync(BookId bookId, CancellationToken ct)
    {
        lock (_gate)
        {
            _failures.Remove(bookId.Value);
        }
        return Task.CompletedTask;
    }

    public Task<PageImage> OpenPageAsync(BookId bookId, ManifestPage page, CancellationToken ct)
    {
        lock (_gate)
        {
            var (bytes, _) = _blobs[(bookId.Value, page.Blob)];
            return Task.FromResult(new PageImage(new MemoryStream(bytes), page.ContentType));
        }
    }

    public Task DeleteBookAsync(BookId bookId, CancellationToken ct)
    {
        lock (_gate)
        {
            Log.Add($"delete-book {bookId.Value}");
            _manifests.Remove(bookId.Value);
            foreach (var key in _blobs.Keys.Where(k => k.BookId == bookId.Value).ToList())
            {
                _blobs.Remove(key);
            }
        }
        return Task.CompletedTask;
    }

    private readonly object _gate = new();
    private readonly Dictionary<(int BookId, string Blob), (byte[] Bytes, string ContentType)> _blobs = [];
    private readonly Dictionary<int, PageManifest> _manifests = [];
    private readonly Dictionary<int, string> _failures = [];
}

internal static class PageFixtures
{
    public static byte[] Zip(params (string Path, byte[] Bytes)[] files)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, bytes) in files)
            {
                var entry = zip.CreateEntry(path);
                using var stream = entry.Open();
                stream.Write(bytes);
            }
        }
        return buffer.ToArray();
    }

    public static byte[] Png(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var buffer = new MemoryStream();
        image.SaveAsPng(buffer);
        return buffer.ToArray();
    }

    /// <summary>Like Assert.ThrowsExceptionAsync, but also accepts a derived exception type.</summary>
    public static async Task<T> CatchAsync<T>(Func<Task> act) where T : Exception
    {
        try
        {
            await act();
        }
        catch (T ex)
        {
            return ex;
        }
        throw new AssertFailedException($"Expected {typeof(T).Name}, but nothing was thrown.");
    }

    public static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    public static async IAsyncEnumerable<T> AsAsync<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }

    public static async Task UntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
        Assert.IsTrue(condition(), "timed out waiting");
    }
}
