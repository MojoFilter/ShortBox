using Microsoft.EntityFrameworkCore;
using ShortBox.DataAccess;
using ShortBox.Services;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class PullListStoreTests
{
    [TestMethod]
    public async Task UpsertInsertsNewAndKeepsUserChoicesOnExisting()
    {
        var store = NewStore();
        await store.UpsertAsync([Release(1, "Darkhawk", "4")], default);
        await store.SetWantedAsync(1, true, default);

        var inserted = await store.UpsertAsync(
            [Release(1, "Darkhawk", "4", title: "Darkhawk #4 (retitled)"), Release(2, "Daredevil", "9")], default);

        Assert.AreEqual(1, inserted);
        var week = await store.GetWeekAsync(DateOnly.Parse("2026-10-05"), default);
        Assert.AreEqual(2, week.Count);
        var darkhawk = week.Single(e => e.Series == "Darkhawk");
        Assert.IsTrue(darkhawk.IsWanted);
        Assert.AreEqual("Darkhawk #4 (retitled)", darkhawk.Title);
        Assert.AreEqual(4, darkhawk.IssueNumber);
        Assert.IsFalse(week.Single(e => e.Series == "Daredevil").IsWanted);
    }

    [TestMethod]
    public async Task SwitchingReleaseSourcesDoesNotDuplicateOrForgetWantedIssues()
    {
        var store = NewStore();
        await store.UpsertAsync([Release(1_234_567, "The Amazing Spider-Man (2025)", "012")], default);
        await store.SetWantedAsync(1_234_567, true, default);

        // Same issue, now reported by another source with a different id and slightly different naming.
        var inserted = await store.UpsertAsync(
            [Release(1_000_000_077, "Amazing Spider-Man", "12"), Release(1_000_000_078, "Daredevil", "9")], default);

        Assert.AreEqual(1, inserted, "only Daredevil is new");
        var week = await store.GetWeekAsync(DateOnly.Parse("2026-10-05"), default);
        Assert.AreEqual(2, week.Count);
        var spider = week.Single(e => e.Series.Contains("Spider"));
        Assert.AreEqual(1_234_567, spider.Id.Value, "keeps the id it was first stored under");
        Assert.IsTrue(spider.IsWanted);
    }

    [TestMethod]
    public async Task VariantsOfTheSameIssueInOneBatchAreStoredOnce()
    {
        var store = NewStore();

        var inserted = await store.UpsertAsync(
            [Release(1, "Darkhawk", "4"), Release(2, "Darkhawk", "4")], default);

        Assert.AreEqual(1, inserted);
    }

    [TestMethod]
    public async Task WeekIsBoundedToSevenDays()
    {
        var store = NewStore();
        await store.UpsertAsync(
        [
            Release(1, "Before", "1", storeDate: "2026-10-04"),
            Release(2, "In", "1", storeDate: "2026-10-11"),
            Release(3, "After", "1", storeDate: "2026-10-12")
        ], default);

        var week = await store.GetWeekAsync(DateOnly.Parse("2026-10-05"), default);

        CollectionAssert.AreEqual(new[] { "In" }, week.Select(e => e.Series).ToArray());
    }

    [TestMethod]
    public async Task LinkingMatchesOnlyWantedOutstandingEntries()
    {
        var store = NewStore();
        await store.UpsertAsync(
            [Release(1, "Darkhawk", "4"), Release(2, "Daredevil", "9"), Release(3, "X-Men", "1")], default);
        await store.SetWantedAsync(1, true, default);
        await store.SetWantedAsync(2, true, default);

        var linked = await store.LinkBooksAsync(
        [
            new Book { Id = new(100), FileName = "Darkhawk 004 (2026).cbz", Series = "Darkhawk", Number = "004" },
            new Book { Id = new(101), FileName = "X-Men 001 (2026).cbz", Series = "X-Men", Number = "001" }
        ], default);

        Assert.AreEqual(1, linked, "X-Men was never wanted, Daredevil has no book yet");
        var outstanding = await store.GetOutstandingAsync(default);
        CollectionAssert.AreEqual(new[] { "Daredevil" }, outstanding.Select(e => e.Series).ToArray());
        var week = await store.GetWeekAsync(DateOnly.Parse("2026-10-05"), default);
        Assert.AreEqual(100, week.Single(e => e.Series == "Darkhawk").BookId!.Value);
    }

    [TestMethod]
    public async Task SettingWantedOnUnknownEntryThrows()
    {
        await Assert.ThrowsExceptionAsync<KeyNotFoundException>(() => NewStore().SetWantedAsync(42, true, default));
    }

    [TestMethod]
    public async Task CatalogAddsOnlyBooksItDoesNotKnowYet()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString());
        var catalog = new BookCatalog(factory);

        var first = await catalog.AddBooksAsync([NewBook("a.cbz"), NewBook("b.cbz")], default);
        var second = await catalog.AddBooksAsync([NewBook("b.cbz"), NewBook("c.cbz")], default);

        Assert.AreEqual(2, first.Count);
        CollectionAssert.AreEqual(new[] { "c.cbz" }, second.Select(b => b.FileName).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "a.cbz", "b.cbz", "c.cbz" },
            (await catalog.GetFileNamesAsync(default)).ToArray());
    }

    // The in-memory provider has no identity column, so unlike SQL Server it needs distinct ids up front.
    private static int _nextId;
    private static Book NewBook(string name) => new() { Id = new(++_nextId), FileName = name };

    private static PullListStore NewStore() => new(new InMemoryFactory(Guid.NewGuid().ToString()));

    private static ReleaseInfo Release(int id, string series, string number, string storeDate = "2026-10-07", string? title = null) =>
        new(id, series, number, title ?? $"{series} #{number}", DateOnly.Parse(storeDate));

    private class InMemoryFactory(string name) : IDbContextFactory<ShortBoxContext>
    {
        public ShortBoxContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<ShortBoxContext>().UseInMemoryDatabase(name).Options);
    }
}
