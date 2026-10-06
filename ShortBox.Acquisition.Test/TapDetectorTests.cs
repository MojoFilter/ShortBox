using ShortBox.Azure.Zoom;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class TapDetectorTests
{
    private sealed class FakeScheduler
    {
        public List<Timer> Timers { get; } = [];

        public IDisposable Schedule(TimeSpan delay, Action action)
        {
            var timer = new Timer(delay, action);
            this.Timers.Add(timer);
            return timer;
        }

        public Timer Last => this.Timers[^1];
    }

    private sealed class Timer(TimeSpan delay, Action action) : IDisposable
    {
        public TimeSpan Delay { get; } = delay;

        public bool Disposed { get; private set; }

        public void Fire()
        {
            if (!this.Disposed)
            {
                action();
            }
        }

        public void Dispose() => this.Disposed = true;
    }

    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(250);

    private readonly FakeScheduler _scheduler = new();
    private readonly List<string> _events = [];

    private TapDetector Create(TimeSpan? delay = null)
    {
        var detector = new TapDetector(delay ?? Delay, 24, _scheduler.Schedule);
        detector.SingleTap += p => _events.Add($"single {p.X},{p.Y}");
        detector.DoubleTap += p => _events.Add($"double {p.X},{p.Y}");
        return detector;
    }

    [TestMethod]
    public void ALoneTapIsSingleOnlyAfterTheDelay()
    {
        var detector = Create();
        detector.Tap(new ZoomPoint(10, 20));
        Assert.AreEqual(0, _events.Count, "held while a second tap could still arrive");

        Assert.AreEqual(Delay, _scheduler.Last.Delay);
        _scheduler.Last.Fire();
        CollectionAssert.AreEqual(new[] { "single 10,20" }, _events);
    }

    [TestMethod]
    public void ASecondTapInTimeIsADoubleTapAndTheSingleNeverFires()
    {
        var detector = Create();
        detector.Tap(new ZoomPoint(10, 20));
        var first = _scheduler.Last;
        detector.Tap(new ZoomPoint(14, 22));

        Assert.IsTrue(first.Disposed);
        first.Fire();
        CollectionAssert.AreEqual(new[] { "double 14,22" }, _events);
    }

    [TestMethod]
    public void ATapFarFromTheFirstReleasesTheFirstAsSingleAndHoldsTheNew()
    {
        var detector = Create();
        detector.Tap(new ZoomPoint(10, 20));
        detector.Tap(new ZoomPoint(300, 400));
        CollectionAssert.AreEqual(new[] { "single 10,20" }, _events);

        _scheduler.Last.Fire();
        CollectionAssert.AreEqual(new[] { "single 10,20", "single 300,400" }, _events);
    }

    [TestMethod]
    public void ThreeQuickTapsAreADoubleThenASingle()
    {
        var detector = Create();
        detector.Tap(new ZoomPoint(10, 10));
        detector.Tap(new ZoomPoint(10, 10));
        detector.Tap(new ZoomPoint(10, 10));
        _scheduler.Last.Fire();
        CollectionAssert.AreEqual(new[] { "double 10,10", "single 10,10" }, _events);
    }

    [TestMethod]
    public void ATapAfterTheDelayStartsOver()
    {
        var detector = Create();
        detector.Tap(new ZoomPoint(10, 10));
        _scheduler.Last.Fire();
        detector.Tap(new ZoomPoint(10, 10));
        _scheduler.Last.Fire();
        CollectionAssert.AreEqual(new[] { "single 10,10", "single 10,10" }, _events);
    }

    [TestMethod]
    public void ZeroDelayMakesEveryTapImmediate()
    {
        var detector = Create(TimeSpan.Zero);
        detector.Tap(new ZoomPoint(1, 2));
        detector.Tap(new ZoomPoint(1, 2));
        CollectionAssert.AreEqual(new[] { "single 1,2", "single 1,2" }, _events);
        Assert.AreEqual(0, _scheduler.Timers.Count);
    }

    [TestMethod]
    public void CancelDropsAHeldTap()
    {
        var detector = Create();
        detector.Tap(new ZoomPoint(10, 20));
        detector.Cancel();
        _scheduler.Last.Fire();
        Assert.AreEqual(0, _events.Count);
        Assert.IsTrue(_scheduler.Last.Disposed);
    }
}
