namespace ShortBox.Azure;

/// <summary>Which pages around the current one are worth having ready. Pure, so the policy is easy to test and tune.</summary>
public static class PageWindow
{
    /// <summary>
    /// The pages to prefetch, most wanted first. <paramref name="direction"/> is the way the reader is travelling (negative is
    /// backwards): <paramref name="ahead"/> pages lie that way and <paramref name="behind"/> pages the other way. Forward with
    /// 3 and 1 gives +1, +2, -1, +3. The current page is not included, and nothing falls outside the book.
    /// </summary>
    public static IReadOnlyList<int> Compute(int current, int pageCount, int direction, int ahead, int behind)
    {
        if (pageCount <= 0)
        {
            return [];
        }

        current = Math.Clamp(current, 0, pageCount - 1);
        var step = direction < 0 ? -1 : 1;
        var pages = new List<int>();
        // Behind page n is as close as ahead page n + 1, and the page the reader is heading for wins a tie.
        for (var rank = 1; rank <= Math.Max(ahead, behind + 1); rank++)
        {
            if (rank <= ahead)
            {
                Add(current + step * rank);
            }

            if (rank - 1 is >= 1 and var back && back <= behind)
            {
                Add(current - step * back);
            }
        }

        return pages;

        void Add(int page)
        {
            if (page >= 0 && page < pageCount)
            {
                pages.Add(page);
            }
        }
    }
}
