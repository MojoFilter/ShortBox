namespace ShortBox.Services;

public enum PageState
{
    /// <summary>Not extracted yet: queued, being extracted, or never requested. A reader can only wait and ask again.</summary>
    Pending,
    Ready,
    /// <summary>The last extraction failed. Requesting preparation again clears this and tries again.</summary>
    Failed,
}

/// <param name="PageCount">Known once the book is <see cref="PageState.Ready"/>.</param>
/// <param name="Error">Why the last extraction failed, when it did.</param>
public sealed record BookPageStatus(PageState State, int? PageCount = null, string? Error = null)
{
    public static BookPageStatus Pending { get; } = new(PageState.Pending);
    public static BookPageStatus Ready(int pageCount) => new(PageState.Ready, pageCount);
    public static BookPageStatus Failed(string error) => new(PageState.Failed, Error: error);
}
