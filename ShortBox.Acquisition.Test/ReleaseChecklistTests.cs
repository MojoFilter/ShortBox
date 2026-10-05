using ShortBox.Services;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class ReleaseChecklistTests
{
    [TestMethod]
    [DataRow("2026-10-07", "2026-10-05")] // Wednesday -> Monday
    [DataRow("2026-10-05", "2026-10-05")] // Monday
    [DataRow("2026-10-11", "2026-10-05")] // Sunday belongs to the week that started Monday
    [DataRow("2026-10-12", "2026-10-12")]
    public void WeekStartIsTheMonday(string date, string expected) =>
        Assert.AreEqual(DateOnly.Parse(expected), ReleaseChecklist.WeekStart(DateOnly.Parse(date)));

    [TestMethod]
    public async Task FetchesFromSourceWhenWeekIsUnknown()
    {
        var source = new FakeReleaseSource(Release(1, "2026-10-07"));
        var checklist = new ReleaseChecklist(source, new FakePullListStore());

        var entries = await checklist.GetWeekAsync(DateOnly.Parse("2026-10-08"), refresh: false, default);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual(1, source.Calls.Count);
        Assert.AreEqual((DateOnly.Parse("2026-10-05"), DateOnly.Parse("2026-10-11")), source.Calls[0]);
    }

    [TestMethod]
    public async Task DoesNotRefetchKnownWeekUnlessAsked()
    {
        var source = new FakeReleaseSource(Release(1, "2026-10-07"));
        var checklist = new ReleaseChecklist(source, new FakePullListStore());
        var date = DateOnly.Parse("2026-10-07");

        await checklist.GetWeekAsync(date, refresh: false, default);
        await checklist.GetWeekAsync(date, refresh: false, default);
        Assert.AreEqual(1, source.Calls.Count);

        await checklist.GetWeekAsync(date, refresh: true, default);
        Assert.AreEqual(2, source.Calls.Count);
    }

    private static ReleaseInfo Release(int id, string storeDate) =>
        new(id, "Darkhawk", "1", "Darkhawk #1", DateOnly.Parse(storeDate));
}
