namespace ShortBox.Azure;

/// <summary>
/// Decides when page downloads may start. Visible pages start at once. Prefetches wait in line, at most
/// <c>maxPrefetch</c> run together, and none start while a visible page is downloading, so speculative work never
/// competes with the page the reader is waiting for.
/// </summary>
internal sealed class PageDownloadScheduler(int maxPrefetch)
{
    /// <summary>Registers a download. Await <see cref="Ticket.Started"/> before doing any work, and call <see cref="Finish"/> when done.</summary>
    public Ticket Enqueue(bool visible)
    {
        var ticket = new Ticket { Visible = visible };
        lock (_lock)
        {
            if (visible)
            {
                this.Begin(ticket);
            }
            else
            {
                _queue.Add(ticket);
                this.Pump();
            }
        }

        return ticket;
    }

    /// <summary>A prefetch the reader now wants on screen: it jumps the line, and holds back other prefetches while it runs.</summary>
    public void Promote(Ticket ticket)
    {
        lock (_lock)
        {
            if (ticket.Visible)
            {
                return;
            }

            ticket.Visible = true;
            if (ticket.Running)
            {
                _prefetchRunning--;
                _visibleRunning++;
            }
            else
            {
                _queue.Remove(ticket);
                this.Begin(ticket);
            }
        }
    }

    /// <summary>The download ended, however it ended (including being cancelled while still waiting in line).</summary>
    public void Finish(Ticket ticket)
    {
        lock (_lock)
        {
            if (ticket.Running)
            {
                ticket.Running = false;
                if (ticket.Visible)
                {
                    _visibleRunning--;
                }
                else
                {
                    _prefetchRunning--;
                }
            }
            else
            {
                _queue.Remove(ticket);
            }

            this.Pump();
        }
    }

    private void Begin(Ticket ticket)
    {
        ticket.Running = true;
        if (ticket.Visible)
        {
            _visibleRunning++;
        }
        else
        {
            _prefetchRunning++;
        }

        ticket.Start.TrySetResult();
    }

    private void Pump()
    {
        while (_visibleRunning == 0 && _prefetchRunning < maxPrefetch && _queue.Count > 0)
        {
            var next = _queue[0];
            _queue.RemoveAt(0);
            this.Begin(next);
        }
    }

    internal sealed class Ticket
    {
        /// <summary>Completes when the download may start.</summary>
        public Task Started => this.Start.Task;

        /// <summary>True for a page the reader is waiting on, which includes a prefetch that was promoted.</summary>
        public bool Visible { get; internal set; }

        internal bool Running { get; set; }

        internal TaskCompletionSource Start { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly List<Ticket> _queue = [];
    private readonly object _lock = new();
    private int _visibleRunning;
    private int _prefetchRunning;
}
