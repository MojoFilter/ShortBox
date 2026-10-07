using ShortBox.Azure;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class PagerWindowTests
{
    [TestMethod]
    public void TheCurrentPageAndItsNeighboursAreLive()
    {
        CollectionAssert.AreEqual(new[] { 5, 6, 4 }, PagerWindow.Live(5, 20).ToArray());
    }

    [TestMethod]
    public void TheFirstPageHasNoPreviousNeighbour()
    {
        CollectionAssert.AreEqual(new[] { 0, 1 }, PagerWindow.Live(0, 20).ToArray());
    }

    [TestMethod]
    public void TheLastPageHasNoNextNeighbour()
    {
        CollectionAssert.AreEqual(new[] { 19, 18 }, PagerWindow.Live(19, 20).ToArray());
    }

    [TestMethod]
    public void AOnePageBookHasOnlyItself()
    {
        CollectionAssert.AreEqual(new[] { 0 }, PagerWindow.Live(0, 1).ToArray());
    }

    [TestMethod]
    public void AnEmptyBookHasNothingLive()
    {
        Assert.AreEqual(0, PagerWindow.Live(0, 0).Count);
    }

    [TestMethod]
    public void ACurrentPagePastTheEndIsClampedToTheLastPage()
    {
        CollectionAssert.AreEqual(new[] { 19, 18 }, PagerWindow.Live(25, 20).ToArray());
    }

    [TestMethod]
    public void RolesFollowDistanceFromTheCurrentPage()
    {
        Assert.AreEqual(PageRole.Current, PagerWindow.RoleOf(5, 5));
        Assert.AreEqual(PageRole.Neighbor, PagerWindow.RoleOf(4, 5));
        Assert.AreEqual(PageRole.Neighbor, PagerWindow.RoleOf(6, 5));
        Assert.AreEqual(PageRole.Far, PagerWindow.RoleOf(3, 5));
        Assert.AreEqual(PageRole.Far, PagerWindow.RoleOf(7, 5));
    }

    [TestMethod]
    public void EveryLivePageIsCurrentOrANeighbour()
    {
        foreach (var page in PagerWindow.Live(7, 30))
        {
            Assert.AreNotEqual(PageRole.Far, PagerWindow.RoleOf(page, 7));
        }
    }
}
