namespace ShortBox.DataAccess;

internal static class EfExtensions
{
    public static IQueryable<Book> WhereUnread(this IQueryable<Book> books, bool unread = true) =>
        books.Where(b => (b.PageCount == null || (b.CurrentPage / (double)b.PageCount) < Book.ReadThreshold) == unread);

    public static IQueryable<Book> WhereRead(this IQueryable<Book> books) => books.WhereUnread(false);

}
