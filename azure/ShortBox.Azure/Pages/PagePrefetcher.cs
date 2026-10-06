using System.Diagnostics;

namespace ShortBox.Azure;

/// <summary>
/// Keeps the pages around the one being read on disk before the reader turns to them. Holds the window for one book:
/// pages that enter it start loading, pages that leave it are cancelled. It does nothing until <see cref="Open"/>, which the
/// caller uses only once the book is prepared. Failures are silent; the reader retries a page itself when it becomes visible.
/// </summary>
/// <remarks>Only files are prefetched. Nothing here holds a decoded image, and the files are bounded by the page cache's own trimming.</remarks>
public sealed class PagePrefetcher(IPageProvider pages, int ahead = 3, int behind = 1) : IDisposable
{
    /// <summary>Starts a window for a prepared book. <paramref name="pageCount"/> is the server's, not the stored <c>Book.PageCount</c>.</summary>
    public void Open(int bookId, int pageCount, int currentPage)
    {
        List<Slot> toStart;
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            this.CancelAll();
            _bookId = bookId;
            _pageCount = pageCount;
            _current = currentPage;
            _direction = 1;
            _isOpen = true;
            toStart = this.Update();
        }

        this.Start(toStart);
    }

    /// <summary>The reader is now on <paramref name="page"/> (a turn or a jump). Also resumes after <see cref="Suspend"/>.</summary>
    public void MoveTo(int page)
    {
        List<Slot> toStart;
        lock (_gate)
        {
            if (_closed || !_isOpen)
            {
                return;
            }

            if (page != _current)
            {
                _direction = page > _current ? 1 : -1;
            }

            _current = page;
            toStart = this.Update();
        }

        this.Start(toStart);
    }

    /// <summary>Cancels everything in flight, for when the reader is not on screen. The next <see cref="MoveTo"/> starts again.</summary>
    public void Suspend()
    {
        lock (_gate)
        {
            this.CancelAll();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _closed = true;
            this.CancelAll();
        }
    }

    /// <summary>Cancels pages that left the window and creates slots for pages that entered it. Call under the lock.</summary>
    private List<Slot> Update()
    {
        var wanted = PageWindow.Compute(_current, _pageCount, _direction, ahead, behind);
        foreach (var index in _slots.Keys.Where(i => !wanted.Contains(i)).ToArray())
        {
            _slots.Remove(index, out var gone);
            gone!.Cancel();
        }

        var toStart = new List<Slot>();
        foreach (var index in wanted)
        {
            if (!_slots.ContainsKey(index))
            {
                var slot = new Slot(_bookId, index);
                _slots[index] = slot;
                toStart.Add(slot);
            }
        }

        return toStart;
    }

    private void CancelAll()
    {
        foreach (var slot in _slots.Values)
        {
            slot.Cancel();
        }

        _slots.Clear();
    }

    private void Start(List<Slot> slots)
    {
        // Outside the lock: a cached page completes inline and its continuation takes the lock again.
        foreach (var slot in slots)
        {
            _ = this.LoadAsync(slot);
        }
    }

    private async Task LoadAsync(Slot slot)
    {
        try
        {
            await pages.PrefetchPageAsync(slot.BookId, slot.Index, slot.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (slot.Token.IsCancellationRequested)
        {
            // Left the window, or the reader was suspended.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Prefetch of page {slot.Index} of book {slot.BookId} failed: {ex.Message}");
            lock (_gate)
            {
                // A passing failure is forgotten, so the next move that still wants the page tries again. One that cannot
                // pass (a missing page) stays recorded until the page leaves the window; the reader reports it when it gets there.
                if (ex is not PageLoadException { CanRetry: false } && _slots.TryGetValue(slot.Index, out var current) && ReferenceEquals(current, slot))
                {
                    _slots.Remove(slot.Index);
                }
            }
        }
    }

    private sealed class Slot(int bookId, int index)
    {
        public int BookId { get; } = bookId;

        public int Index { get; } = index;

        public CancellationToken Token => _cts.Token;

        public void Cancel() => _cts.Cancel();

        private readonly CancellationTokenSource _cts = new();
    }

    private readonly Dictionary<int, Slot> _slots = [];
    private readonly object _gate = new();
    private int _bookId;
    private int _pageCount;
    private int _current;
    private int _direction = 1;
    private bool _isOpen;
    private bool _closed;
}
