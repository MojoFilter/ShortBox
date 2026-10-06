namespace ShortBox.Azure.Zoom;

/// <summary>
/// Tells a single tap from the first half of a double tap. A tap is held for <c>doubleTapDelay</c>; a second tap close to it
/// in that time is a <see cref="DoubleTap"/> (the first never fires), otherwise the first is released as a
/// <see cref="SingleTap"/>. The timer is injected so the rules are testable. With a zero delay every tap is single at once.
/// </summary>
public sealed class TapDetector
{
    private readonly TimeSpan _doubleTapDelay;
    private readonly double _slop;
    private readonly Func<TimeSpan, Action, IDisposable> _schedule;
    private ZoomPoint? _pending;
    private IDisposable? _timer;

    /// <param name="schedule">Runs the action once after the delay on the UI thread, and cancels it when disposed.</param>
    public TapDetector(TimeSpan doubleTapDelay, double slop, Func<TimeSpan, Action, IDisposable> schedule)
    {
        _doubleTapDelay = doubleTapDelay;
        _slop = slop;
        _schedule = schedule;
    }

    public event Action<ZoomPoint>? SingleTap;

    public event Action<ZoomPoint>? DoubleTap;

    public void Tap(ZoomPoint point)
    {
        if (_doubleTapDelay <= TimeSpan.Zero)
        {
            this.SingleTap?.Invoke(point);
            return;
        }

        if (_pending is ZoomPoint first)
        {
            this.ClearPending();
            if (Distance(first, point) <= _slop)
            {
                this.DoubleTap?.Invoke(point);
                return;
            }

            // A tap somewhere else is a different gesture: the first one was a single tap after all.
            this.SingleTap?.Invoke(first);
        }

        _pending = point;
        _timer = _schedule(_doubleTapDelay, this.OnElapsed);
    }

    /// <summary>Drops a held tap without raising it, e.g. when a pinch or pan starts.</summary>
    public void Cancel() => this.ClearPending();

    private void OnElapsed()
    {
        if (_pending is ZoomPoint point)
        {
            this.ClearPending();
            this.SingleTap?.Invoke(point);
        }
    }

    private void ClearPending()
    {
        _pending = null;
        _timer?.Dispose();
        _timer = null;
    }

    private static double Distance(ZoomPoint a, ZoomPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
