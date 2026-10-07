using ShortBox.Azure;
using ShortBox.Azure.Zoom;
using System.Diagnostics;

namespace ShortBoxMobile;

/// <summary>
/// One page of the book as the pager sees it: its image, and its own loading and error state. The pager creates one for every
/// page up front (they are cheap) and the reader tells each how close it is through <see cref="SetRole"/>, so only the page on
/// screen and its neighbours ever hold an image.
/// </summary>
public sealed partial class ReaderPage : ObservableObject
{
    private static readonly TimeSpan LoadingIndicatorDelay = TimeSpan.FromMilliseconds(300);

    private readonly IPageProvider _pages;
    private readonly int _bookId;
    private CancellationTokenSource _load;
    private PageRole _role = PageRole.Far;
    private PageRole _loadRole;
    private bool _ready;
    private bool _failed;
    private bool _failedForGood;

    public ReaderPage(IPageProvider pages, int bookId, int index)
    {
        _pages = pages;
        _bookId = bookId;
        this.Index = index;
    }

    public int Index { get; }

    /// <summary>The image once loaded, otherwise null.</summary>
    [ObservableProperty]
    private ImageSource _source;

    /// <summary>Width over height of <see cref="Source"/>, or null if unknown. Set just before the source, so zoom bounds never describe another image.</summary>
    [ObservableProperty]
    private double? _aspectRatio;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorText;

    [ObservableProperty]
    private bool _canRetry = true;

    /// <summary>Whether this is the page on screen. A pager has several pages alive, and only this one should answer keys.</summary>
    [ObservableProperty]
    private bool _isCurrent;

    public string LoadingText => "Loading page…";

    [RelayCommand]
    private void Retry() => this.StartLoad(_role == PageRole.Far ? PageRole.Current : _role);

    /// <summary>
    /// Tells the page how near the reader is. A page that gets far lets go of its image. A page that comes near starts loading,
    /// and a failed one tries again; the page on screen loads with priority and reports errors, a neighbour loads quietly.
    /// </summary>
    public void SetRole(PageRole role)
    {
        _role = role;
        this.IsCurrent = role == PageRole.Current;
        if (role == PageRole.Far)
        {
            this.Clear();
            return;
        }

        if (_ready)
        {
            return;
        }

        var inFlight = _load is not null && !_failed;
        var retry = _failed && (role == PageRole.Current || !_failedForGood);
        if (!inFlight || _loadRole != role || retry)
        {
            this.StartLoad(role);
        }
    }

    private void Clear()
    {
        this.CancelLoad();
        _ready = false;
        _failed = false;
        _failedForGood = false;
        _loadRole = PageRole.Far;
        this.Source = null;
        this.AspectRatio = null;
        this.IsLoading = false;
        this.ErrorText = null;
    }

    private void CancelLoad() => Interlocked.Exchange(ref _load, null)?.Cancel();

    /// <summary>The new load joins the page's download before the old one lets go of it, so a page changing role is never dropped in between.</summary>
    private void StartLoad(PageRole role)
    {
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _load, next);
        _loadRole = role;
        _failed = false;
        this.ErrorText = null;
        if (role != PageRole.Current)
        {
            // A page that stopped being the current one must not keep a spinner nobody can see.
            this.IsLoading = false;
        }

        _ = this.LoadAsync(role, next.Token);
        previous?.Cancel();
    }

    private async Task LoadAsync(PageRole role, CancellationToken token)
    {
        var visible = role == PageRole.Current;
        var finished = false;
        if (visible)
        {
            _ = ShowSpinnerSoonAsync();
        }

        try
        {
            var path = visible
                ? await _pages.GetPageFileAsync(_bookId, this.Index, token)
                : await _pages.PrefetchPageAsync(_bookId, this.Index, token);
            var aspectRatio = await Task.Run(() => ReadAspectRatio(path), token);
            token.ThrowIfCancellationRequested();
            this.AspectRatio = aspectRatio;
            this.Source = ImageSource.FromStream(() => File.OpenRead(path));
            _ready = true;
            this.ErrorText = null;
            this.IsLoading = false;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (Exception ex)
        {
            _failed = true;
            this.IsLoading = false;
            _failedForGood = ex is PageLoadException { CanRetry: false };
            if (visible)
            {
                this.ErrorText = LoadErrors.Describe(ex, out var canRetry);
                this.CanRetry = canRetry;
            }
            else
            {
                // Nobody is looking at this page. If the reader gets here, it loads again with an error to show.
                Debug.WriteLine($"Page {this.Index} of book {_bookId} did not load ahead: {ex.Message}");
            }

            return;
        }
        finally { finished = true; }

        async Task ShowSpinnerSoonAsync()
        {
            try
            {
                // Cached pages arrive at once, so only a slow one gets a spinner.
                await Task.Delay(LoadingIndicatorDelay, token);
            }
            catch (OperationCanceledException) { return; }

            if (!finished)
            {
                this.IsLoading = true;
            }
        }
    }

    /// <summary>Reads the page's shape from its header so the zoom bounds follow the image, not the letterbox. Null if unreadable.</summary>
    private static double? ReadAspectRatio(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return ImageDimensions.TryReadAspectRatio(stream);
        }
        catch (IOException)
        {
            return null;
        }
    }
}

/// <summary>What to tell the reader when a book or page would not load.</summary>
internal static class LoadErrors
{
    public static string Describe(Exception ex, out bool canRetry)
    {
        canRetry = ex is not PageLoadException { CanRetry: false };
        return ex switch
        {
            PageLoadException { Kind: PageLoadFailure.PreparationFailed } => $"The server could not prepare this book.\n{ex.Message}",
            PageLoadException { Kind: PageLoadFailure.Timeout } => "This is taking too long. Try again in a moment.",
            PageLoadException { Kind: PageLoadFailure.Network } => "Couldn't reach the server.",
            PageLoadException => ex.Message,
            _ => "Something went wrong opening this book.",
        };
    }
}
