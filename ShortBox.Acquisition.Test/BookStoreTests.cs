using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShortBox.DataAccess;
using ShortBox.Services;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class BookStoreTests
{
    [TestMethod]
    public async Task MarkingReadPutsTheBookPastTheReadThreshold()
    {
        var (store, factory) = NewStore(new Book { Id = new(1), FileName = "a.cbz", PageCount = 20, CurrentPage = 3 });

        await store.MarkReadAsync(new(1), true, default);

        var book = await GetBookAsync(factory, 1);
        Assert.AreEqual(20, book.CurrentPage);
        Assert.IsTrue(book.IsRead);
    }

    [TestMethod]
    public async Task MarkingUnreadRewindsToTheFirstPage()
    {
        var (store, factory) = NewStore(new Book { Id = new(1), FileName = "a.cbz", PageCount = 20, CurrentPage = 20 });

        await store.MarkReadAsync(new(1), false, default);

        var book = await GetBookAsync(factory, 1);
        Assert.AreEqual(0, book.CurrentPage);
        Assert.IsFalse(book.IsRead);
    }

    [TestMethod]
    public async Task MarkingReadWithoutAPageCountIsRejectedAndLeavesTheBookAlone()
    {
        var (store, factory) = NewStore(new Book { Id = new(1), FileName = "a.cbz", PageCount = null, CurrentPage = 2 });

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => store.MarkReadAsync(new(1), true, default));

        Assert.AreEqual(2, (await GetBookAsync(factory, 1)).CurrentPage);
    }

    [TestMethod]
    public async Task MarkingAnUnknownBookThrows()
    {
        var (store, _) = NewStore();

        await Assert.ThrowsExceptionAsync<KeyNotFoundException>(() => store.MarkReadAsync(new(42), true, default));
    }

    [TestMethod]
    public void BooksWithoutAPageCountAreNeverRead()
    {
        Assert.IsFalse(new Book { Id = new(1), FileName = "a.cbz", PageCount = null, CurrentPage = 99 }.IsRead);
        Assert.IsFalse(new Book { Id = new(1), FileName = "a.cbz", PageCount = 0 }.IsRead);
    }

    [TestMethod]
    public async Task APageListForAnUnknownBookIsNotFound()
    {
        var (store, _) = NewStore(pages: new(BookPages.NotReady(BookPageStatus.Pending)));

        await Assert.ThrowsExceptionAsync<KeyNotFoundException>(() => store.GetPagesAsync(new(42), default));
    }

    [TestMethod]
    public async Task APageListCorrectsTheStoredPageCountWithoutTouchingModified()
    {
        var modified = new DateTime(2026, 1, 2);
        var (store, factory) = NewStore(
            new FakePageCache(new BookPages(BookPageStatus.Ready(3), [new(0, "image/png", 1, 1), new(1, "image/png", 1, 1), new(2, "image/png", 1, 1)])),
            new Book { Id = new(1), FileName = "a.cbz", PageCount = 2, Modified = modified });

        var pages = await store.GetPagesAsync(new(1), default);

        Assert.AreEqual(3, pages.Pages.Count);
        var book = await GetBookAsync(factory, 1);
        Assert.AreEqual(3, book.PageCount);
        Assert.AreEqual(modified, book.Modified);
    }

    [TestMethod]
    public async Task APageListForABookThatIsNotReadyLeavesThePageCountAlone()
    {
        var (store, factory) = NewStore(
            new FakePageCache(BookPages.NotReady(BookPageStatus.Pending)),
            new Book { Id = new(1), FileName = "a.cbz", PageCount = 20 });

        var pages = await store.GetPagesAsync(new(1), default);

        Assert.AreEqual(PageState.Pending, pages.Status.State);
        Assert.AreEqual(0, pages.Pages.Count);
        Assert.AreEqual(20, (await GetBookAsync(factory, 1)).PageCount);
    }

    private static (BookStore Store, InMemoryFactory Factory) NewStore(params Book[] books) => NewStore(null, books);

    private static (BookStore Store, InMemoryFactory Factory) NewStore(FakePageCache? pages, params Book[] books)
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString());
        using (var context = factory.CreateDbContext())
        {
            context.Books.AddRange(books);
            context.SaveChanges();
        }
        return (new BookStore(factory, pages!, NullLogger<BookStore>.Instance), factory);
    }

    /// <summary>Answers page-list questions with a fixed result; anything else is a test mistake.</summary>
    private class FakePageCache(BookPages pages) : IPageCache
    {
        public Task<BookPages> GetPagesAsync(BookId bookId, CancellationToken ct) => Task.FromResult(pages);
        public Task<BookPageStatus> GetStatusAsync(BookId bookId, CancellationToken ct) => Task.FromResult(pages.Status);

        public Task DecacheBooksAsync(IEnumerable<Book> booksToDecache, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IEnumerable<BookId>> GetCachedBookIdsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream> GetCoverAsync(BookId bookId, string bookFileName, CancellationToken ct) => throw new NotSupportedException();
        public Task<PageImage> GetPageAsync(BookId bookId, string bookFileName, int pageNumber, CancellationToken ct) => throw new NotSupportedException();
        public Task ClearFailureAsync(BookId bookId, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> PrepareAsync(BookId bookId, string bookFileName, CancellationToken ct) => throw new NotSupportedException();
    }

    private static async Task<Book> GetBookAsync(InMemoryFactory factory, int id)
    {
        using var context = factory.CreateDbContext();
        return await context.Books.SingleAsync(b => b.Id == new BookId(id));
    }

    private class InMemoryFactory(string name) : IDbContextFactory<ShortBoxContext>
    {
        public ShortBoxContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<ShortBoxContext>().UseInMemoryDatabase(name).Options);
    }
}
