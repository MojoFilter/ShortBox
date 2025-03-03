namespace ShortBox.DataAccess;

internal static class EfExtensions
{
    private const double ReadThreshold = 0.91; // 91%

    public static IQueryable<Book> WhereUnread(this IQueryable<Book> books, bool unread = true) =>
        books.Where(b => (b.PageCount == null || (b.CurrentPage / (double)b.PageCount) < ReadThreshold) == unread);

}
