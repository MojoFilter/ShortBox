namespace ShortBox.Azure;

public enum PageLoadFailure
{
    /// <summary>The server could not be reached, or kept failing, after retries.</summary>
    Network,
    /// <summary>A request, or the wait for a book to be prepared, ran out of time.</summary>
    Timeout,
    /// <summary>The book or page does not exist.</summary>
    NotFound,
    /// <summary>The archive has no page images.</summary>
    NoImages,
    /// <summary>The host key was rejected.</summary>
    Unauthorized,
    /// <summary>The server tried to extract the book and failed. Preparing again retries it.</summary>
    PreparationFailed,
    /// <summary>The server stopped serving a prepared book (for example after a decache) and would not recover.</summary>
    NotReady,
    /// <summary>The server answered with something that is not a page.</summary>
    InvalidResponse,
}

public sealed class PageLoadException(PageLoadFailure kind, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public PageLoadFailure Kind { get; } = kind;

    /// <summary>False when asking again cannot help (wrong book, wrong key, no images).</summary>
    public bool CanRetry => Kind is not (PageLoadFailure.NotFound or PageLoadFailure.NoImages or PageLoadFailure.Unauthorized or PageLoadFailure.InvalidResponse);
}
