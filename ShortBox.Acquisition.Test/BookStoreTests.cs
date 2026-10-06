using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShortBox.DataAccess;

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

    private static (BookStore Store, InMemoryFactory Factory) NewStore(params Book[] books)
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString());
        using (var context = factory.CreateDbContext())
        {
            context.Books.AddRange(books);
            context.SaveChanges();
        }
        return (new BookStore(factory, null!, NullLogger<BookStore>.Instance), factory);
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
