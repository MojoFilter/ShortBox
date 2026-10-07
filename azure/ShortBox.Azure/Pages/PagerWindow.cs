namespace ShortBox.Azure;

/// <summary>How much a page of the book matters to the reader right now.</summary>
public enum PageRole
{
    /// <summary>Not near the reader: holds nothing, not even a decoded image.</summary>
    Far,
    /// <summary>Next to the page on screen: loaded ahead, so a swipe lands on a finished page.</summary>
    Neighbor,
    /// <summary>The page on screen.</summary>
    Current,
}

/// <summary>
/// Which pages the pager keeps loaded. This is the memory budget of the reader: the page on screen and the ones either side of
/// it, however long the book. <see cref="PageWindow"/> is the separate, wider window of files kept on disk.
/// </summary>
public static class PagerWindow
{
    /// <summary>How many pages each side of the current one are kept loaded.</summary>
    public const int Reach = 1;

    public static PageRole RoleOf(int index, int current) => Math.Abs(index - current) switch
    {
        0 => PageRole.Current,
        <= Reach => PageRole.Neighbor,
        _ => PageRole.Far,
    };

    /// <summary>The pages that are not <see cref="PageRole.Far"/>, current first, then the next page, then the previous one.</summary>
    public static IReadOnlyList<int> Live(int current, int pageCount)
    {
        if (pageCount <= 0)
        {
            return [];
        }

        current = Math.Clamp(current, 0, pageCount - 1);
        var pages = new List<int> { current };
        for (var distance = 1; distance <= Reach; distance++)
        {
            if (current + distance < pageCount)
            {
                pages.Add(current + distance);
            }

            if (current - distance >= 0)
            {
                pages.Add(current - distance);
            }
        }

        return pages;
    }
}
