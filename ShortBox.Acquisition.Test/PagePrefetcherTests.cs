using ShortBox.Azure;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class PageWindowTests
{
    private static int[] Window(int current, int count, int direction = 1, int ahead = 3, int behind = 1) =>
        [.. PageWindow.Compute(current, count, direction, ahead, behind)];

    [TestMethod]
    public void ForwardWindowIsTheNextThreePagesAndThePreviousOneNearestFirst()
    {
        CollectionAssert.AreEqual(new[] { 6, 7, 4, 8 }, Window(5, 20));
    }

    [TestMethod]
    public void BackwardWindowMirrorsIt()
    {
        CollectionAssert.AreEqual(new[] { 4, 3, 6, 2 }, Window(5, 20, direction: -1));
    }

    [TestMethod]
    public void TheFirstPageHasNothingBehindIt()
    {
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Window(0, 20));
    }

    [TestMethod]
    public void TheLastPageHasNothingAheadOfIt()
    {
        CollectionAssert.AreEqual(new[] { 18 }, Window(19, 20));
    }

    [TestMethod]
    public void ReadingBackwardsFromTheFirstPageHasOnlyThePageAfterIt()
    {
        CollectionAssert.AreEqual(new[] { 1 }, Window(0, 20, direction: -1));
    }

    [TestMethod]
    public void TheWindowStopsAtTheRealLastPage()
    {
        CollectionAssert.AreEqual(new[] { 4, 2 }, Window(3, 5));
    }

    [TestMethod]
    public void ACurrentPagePastTheEndIsClampedToTheLastPage()
    {
        CollectionAssert.AreEqual(new[] { 25 }, Window(40, 27));
    }

    [TestMethod]
    public void ABookOfOnePageOrNoneHasNoWindow()
    {
        Assert.AreEqual(0, Window(0, 1).Length);
        Assert.AreEqual(0, Window(0, 0).Length);
        Assert.AreEqual(0, Window(3, -1).Length);
    }

    [TestMethod]
    public void TheCurrentPageIsNeverInItsOwnWindow()
    {
        for (var page = 0; page < 12; page++)
        {
            foreach (var direction in new[] { 1, -1 })
            {
                var window = Window(page, 12, direction);
                Assert.IsFalse(window.Contains(page));
                Assert.AreEqual(window.Length, window.Distinct().Count());
                Assert.IsTrue(window.All(p => p is >= 0 and < 12));
            }
        }
    }

    [TestMethod]
    public void SizesAreConfigurable()
    {
        CollectionAssert.AreEqual(new[] { 6, 7, 4, 3 }, Window(5, 20, ahead: 2, behind: 2));
        Assert.AreEqual(0, Window(5, 20, ahead: 0, behind: 0).Length);
    }
}

[TestClass]
public class PagePrefetcherTests
{
    /// <summary>Records what the prefetcher asks for, and lets a test decide how each page ends.</summary>
    private sealed class FakePages : IPageProvider
    {
        public List<(int Book, int Page)> Requests { get; } = [];

        public List<int> Cancelled { get; } = [];

        public int RequestsFor(int page) => this.Requests.Count(r => r.Page == page);

        public Task<string> PrefetchPageAsync(int bookId, int pageIndex, CancellationToken cancellationToken = default)
        {
            var source = new TaskCompletionSource<string>();
            this.Requests.Add((bookId, pageIndex));
            _pending[pageIndex] = source;
            cancellationToken.Register(() =>
            {
                this.Cancelled.Add(pageIndex);
                source.TrySetCanceled(cancellationToken);
            });
            return source.Task;
        }

        public void Complete(int page) => _pending[page].SetResult($"page{page}");

        public void Fail(int page, Exception error) => _pending[page].SetException(error);

        public Task<BookReadyInfo> PrepareAsync(int bookId, IProgress<PrepareProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> GetPageFileAsync(int bookId, int pageIndex, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Release(int bookId) { }

        private readonly Dictionary<int, TaskCompletionSource<string>> _pending = [];
    }

    [TestMethod]
    public void NothingIsFetchedUntilTheBookIsOpened()
    {
        var pages = new FakePages();
        using var prefetcher = new PagePrefetcher(pages);

        prefetcher.MoveTo(3);

        Assert.AreEqual(0, pages.Requests.Count);
    }

    [TestMethod]
    public void OpeningStartsTheWindowNearestPageFirst()
    {
        var pages = new FakePages();
        using var prefetcher = new PagePrefetcher(pages);

        prefetcher.Open(7, 20, 4);

        CollectionAssert.AreEqual(new[] { 5, 6, 3, 7 }, pages.Requests.Select(r => r.Page).ToArray());
        Assert.IsTrue(pages.Requests.All(r => r.Book == 7));
    }

    [TestMethod]
    public void TheWindowIsClampedToTheRealPageCount()
    {
        var pages = new FakePages();
        using var prefetcher = new PagePrefetcher(pages);

        prefetcher.Open(7, 5, 3);

        CollectionAssert.AreEquivalent(new[] { 4, 2 }, pages.Requests.Select(r => r.Page).ToArray());
    }

    [TestMethod]
    public void AJumpCancelsPagesThatLeftTheWindowAndKeepsTheRest()
    {
        var pages = new FakePages();
        using var prefetcher = new PagePrefetcher(pages);
        prefetcher.Open(7, 30, 5);

        prefetcher.MoveTo(6);

        CollectionAssert.AreEquivalent(new[] { 4, 6 }, pages.Cancelled);
        Assert.AreEqual(1, pages.RequestsFor(7), "A page that stays in the window is not asked for again.");
        Assert.AreEqual(1, pages.RequestsFor(8));
        Assert.AreEqual(1, pages.RequestsFor(9));
        Assert.AreEqual(1, pages.RequestsFor(5));
    }

    [TestMethod]
    public void AFarJumpReplacesTheWholeWindow()
    {
        var pages = new FakePages();
        using var prefetcher = new PagePrefetcher(pages);
        prefetcher.Open(7, 100, 2);

        prefetcher.MoveTo(80);

        CollectionAssert.AreEquivalent(new[] { 3, 4, 1, 5 }, pages.Cancelled);
        CollectionAssert.AreEquivalent(new[] { 81, 82, 79, 83 }, pages.Requests.Skip(4).Select(r => r.Page).ToArray());
    }

    [TestMethod]
    public void TurningBackwardsMakesTheWindowFollow()
    {
        var pages = new FakePages();
        using var prefetcher = new PagePrefetcher(pages);
        prefetcher.Open(7, 30, 10);

        prefetcher.MoveTo(9);

        CollectionAssert.AreEqual(new[] { 8, 7, 10, 6 }, pages.Requests.Skip(4).Select(r => r.Page).ToArray());
        CollectionAssert.AreEquivalent(new[] { 9, 11, 12, 13 }, pages.Cancelled);
    }

    [TestMethod]
    public void SuspendingCancelsEverythingAndTheNextMoveStartsAgain()
    {
        var pages = new FakePages();
        using var prefetcher = new PagePrefetcher(pages);
        prefetcher.Open(7, 30, 5);

        prefetcher.Suspend();
        Assert.AreEqual(4, pages.Cancelled.Count);

        prefetcher.MoveTo(5);
        Assert.AreEqual(8, pages.Requests.Count);
    }

    [TestMethod]
    public void OpeningAnotherBookDropsTheFirstBooksWindow()
    {
        var pages = new FakePages();
        using var prefetcher = new PagePrefetcher(pages);
        prefetcher.Open(7, 30, 5);

        prefetcher.Open(8, 30, 0);

        Assert.AreEqual(4, pages.Cancelled.Count);
        Assert.IsTrue(pages.Requests.Skip(4).All(r => r.Book == 8));
    }

    [TestMethod]
    public void ADisposedPrefetcherCancelsAndIgnoresMoves()
    {
        var pages = new FakePages();
        var prefetcher = new PagePrefetcher(pages);
        prefetcher.Open(7, 30, 5);

        prefetcher.Dispose();
        prefetcher.MoveTo(9);

        Assert.AreEqual(4, pages.Cancelled.Count);
        Assert.AreEqual(4, pages.Requests.Count);
    }

    [TestMethod]
    public void APageThatWasFetchedIsNotAskedForAgain()
    {
        var pages = new FakePages();
        using var prefetcher = new PagePrefetcher(pages);
        prefetcher.Open(7, 30, 5);

        pages.Complete(6);
        prefetcher.MoveTo(5);

        Assert.AreEqual(1, pages.RequestsFor(6));
    }

    [TestMethod]
    public void AFailedPrefetchIsSilentAndTriedAgainOnTheNextMove()
    {
        var pages = new FakePages();
        using var prefetcher = new PagePrefetcher(pages);
        prefetcher.Open(7, 30, 5);

        pages.Fail(6, new PageLoadException(PageLoadFailure.Network, "offline"));
        pages.Fail(7, new InvalidOperationException("something unexpected"));
        prefetcher.MoveTo(5);

        Assert.AreEqual(2, pages.RequestsFor(6));
        Assert.AreEqual(2, pages.RequestsFor(7));
        Assert.AreEqual(1, pages.RequestsFor(4), "Pages that did not fail are left alone.");
    }

    [TestMethod]
    public void APageThatCanNeverLoadIsNotRetried()
    {
        var pages = new FakePages();
        using var prefetcher = new PagePrefetcher(pages);
        prefetcher.Open(7, 30, 5);

        pages.Fail(6, new PageLoadException(PageLoadFailure.NotFound, "gone"));
        prefetcher.MoveTo(5);
        prefetcher.MoveTo(5);

        Assert.AreEqual(1, pages.RequestsFor(6));
    }

    [TestMethod]
    public void APageThatLeavesTheWindowAndComesBackIsAskedForAgain()
    {
        var pages = new FakePages();
        using var prefetcher = new PagePrefetcher(pages);
        prefetcher.Open(7, 30, 5);

        prefetcher.MoveTo(6);
        prefetcher.MoveTo(5);

        Assert.AreEqual(2, pages.RequestsFor(4));
    }
}
