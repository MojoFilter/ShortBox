using ShortBox.Services;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class ReleaseMatcherTests
{
    [TestMethod]
    [DataRow("Amazing Spider-Man", "12", "Amazing Spider-Man", "012", true)]
    [DataRow("The Amazing Spider-Man (2025)", "12", "Amazing Spider-Man", "12", true)]
    [DataRow("Daredevil & Echo", "1", "Daredevil and Echo", "1", false)]
    [DataRow("X-Men", "1", "X-Men", "1.5", false)]
    [DataRow("Captain America", "1AU", "captain america", "1au", true)]
    [DataRow("Darkhawk", "4", "Darkhawk", "5", false)]
    [DataRow("", "1", "", "1", false)]
    public void MatchesBySeriesAndNumber(string entrySeries, string entryNumber, string bookSeries, string bookNumber, bool expected)
    {
        var entry = new PullListEntry { Id = new(1), Title = "t", IssueNumber = 0, Series = entrySeries, Number = entryNumber };
        var book = new Book { Id = new(1), FileName = "f.cbz", Series = bookSeries, Number = bookNumber };

        Assert.AreEqual(expected, ReleaseMatcher.IsMatch(entry, book));
    }

    [TestMethod]
    public void BookWithoutSeriesNeverMatches()
    {
        var entry = new PullListEntry { Id = new(1), Title = "t", IssueNumber = 1, Series = "Darkhawk", Number = "1" };
        var book = new Book { Id = new(1), FileName = "f.cbz" };

        Assert.IsFalse(ReleaseMatcher.IsMatch(entry, book));
    }
}
