namespace ShortBox.Azure;

public enum PrepareStage
{
    /// <summary>Asking the server to get the book ready.</summary>
    Starting,
    /// <summary>The server is extracting the book; checking until it is done.</summary>
    Extracting,
}

public sealed record PrepareProgress(PrepareStage Stage, TimeSpan Elapsed);

/// <param name="PageCount">The count the server has for the extracted book. Prefer it over the stored <c>Book.PageCount</c>, which can be stale.</param>
public sealed record BookReadyInfo(int PageCount);

public interface IPageProvider
{
    /// <summary>
    /// Gets a book extracted and ready to serve pages. Concurrent calls for one book share a single wait, which does not
    /// stop when a caller cancels. <paramref name="progress"/> is told how the wait is going, including late joiners.
    /// </summary>
    /// <exception cref="PageLoadException"/>
    Task<BookReadyInfo> PrepareAsync(int bookId, IProgress<PrepareProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Returns the path of a local copy of the page, downloading it first if the disk cache lacks it.</summary>
    /// <exception cref="PageLoadException"/>
    Task<string> GetPageFileAsync(int bookId, int pageIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="GetPageFileAsync"/>, for a page the reader may want soon. Callers asking for one page share a single
    /// download, which stops when the last of them cancels. A prefetch yields to visible pages (it waits while one is
    /// downloading, and joins its download if the reader asks for the same page), and never makes the server extract a book again.
    /// </summary>
    /// <exception cref="PageLoadException"/>
    Task<string> PrefetchPageAsync(int bookId, int pageIndex, CancellationToken cancellationToken = default);

    /// <summary>Stops preparing a book and forgets that it was ready.</summary>
    void Release(int bookId);
}
