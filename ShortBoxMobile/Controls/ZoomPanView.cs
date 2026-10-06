using ShortBox.Azure.Zoom;

namespace ShortBoxMobile;

/// <summary>
/// Lets its <see cref="ContentView.Content"/> (a page image) be pinched, panned and double-tapped. Pinch zooms about the
/// fingers, a double tap zooms in about the tap and back out, and the content cannot be dragged past its edges. The zoom rules
/// are in <see cref="ZoomGeometry"/>; native touch (Android) and pointer and key (Windows) input arrive through
/// <see cref="IZoomGestureSource"/> because MAUI's pinch has no reliable focal point and nothing handles a wheel or mouse drag.
/// </summary>
/// <remarks>
/// The control never moves itself, only its content, so touch coordinates stay stable and it can sit in a pager. Not zoomed,
/// it leaves horizontal drags to a parent pager (Android) or reports them as <see cref="NavigationRequested"/>. Zoomed, it keeps
/// them until the content hits an edge. Content is assumed to be aspect-fitted in the control; set
/// <see cref="ContentAspectRatio"/> so panning stops at the image rather than at the letterbox.
/// </remarks>
public partial class ZoomPanView : ContentView, IZoomGestureHost
{
    private const string AnimationName = "ZoomPanView.Move";

    /// <summary>A second tap farther than this from the first (device-independent units) is a new gesture.</summary>
    private const double DoubleTapSlop = 40;

    private ZoomGeometry _geometry = new(new ZoomSize(0, 0));
    private ZoomState _state = ZoomState.Fit;
    private TapDetector _taps;
    private IZoomGestureSource _gestures;

    public ZoomPanView()
    {
        this.IsClippedToBounds = true;
        this.SizeChanged += this.OnSizeChanged;
        this.RebuildTapDetector();
    }

    public static readonly BindableProperty MaxScaleProperty =
        Create(nameof(MaxScale), 4.0, (view, _) => view.OnGeometryInputsChanged());

    public static readonly BindableProperty DoubleTapScaleProperty = Create(nameof(DoubleTapScale), 2.5);

    public static readonly BindableProperty ContentAspectRatioProperty =
        Create<double?>(nameof(ContentAspectRatio), null, (view, _) => view.OnGeometryInputsChanged());

    public static readonly BindableProperty IsDoubleTapZoomEnabledProperty =
        Create(nameof(IsDoubleTapZoomEnabled), true, (view, _) => view.RebuildTapDetector());

    public static readonly BindableProperty DoubleTapDelayProperty =
        Create(nameof(DoubleTapDelay), TimeSpan.FromMilliseconds(250), (view, _) => view.RebuildTapDetector());

    public static readonly BindableProperty IsSwipeNavigationEnabledProperty = Create(nameof(IsSwipeNavigationEnabled), true);

    public static readonly BindableProperty IsKeyboardEnabledProperty = Create(nameof(IsKeyboardEnabled), true);

    private static readonly BindablePropertyKey IsZoomedPropertyKey =
        BindableProperty.CreateReadOnly(nameof(IsZoomed), typeof(bool), typeof(ZoomPanView), false);

    public static readonly BindableProperty IsZoomedProperty = IsZoomedPropertyKey.BindableProperty;

    private static readonly BindablePropertyKey ZoomScalePropertyKey =
        BindableProperty.CreateReadOnly(nameof(ZoomScale), typeof(double), typeof(ZoomPanView), 1.0);

    public static readonly BindableProperty ZoomScaleProperty = ZoomScalePropertyKey.BindableProperty;

    /// <summary>A tap that is not part of a double tap, once the double-tap delay has passed.</summary>
    public event EventHandler<ZoomTappedEventArgs> Tapped;

    /// <summary>A swipe (when not zoomed) or navigation key asked to move to the previous or next page.</summary>
    public event EventHandler<ZoomNavigationEventArgs> NavigationRequested;

    /// <summary>The largest scale, as a multiple of the fitted size. Programmatic zooms are limited to it too.</summary>
    public double MaxScale
    {
        get => (double)GetValue(MaxScaleProperty);
        set => SetValue(MaxScaleProperty, value);
    }

    /// <summary>The scale a double tap zooms in to.</summary>
    public double DoubleTapScale
    {
        get => (double)GetValue(DoubleTapScaleProperty);
        set => SetValue(DoubleTapScaleProperty, value);
    }

    /// <summary>Width over height of the content when it is not the control's shape, or null. Bounds panning to the image.</summary>
    public double? ContentAspectRatio
    {
        get => (double?)GetValue(ContentAspectRatioProperty);
        set => SetValue(ContentAspectRatioProperty, value);
    }

    /// <summary>
    /// Whether a double tap zooms. Turning it off makes <see cref="Tapped"/> fire at once, otherwise it waits
    /// <see cref="DoubleTapDelay"/> to be sure no second tap follows.
    /// </summary>
    public bool IsDoubleTapZoomEnabled
    {
        get => (bool)GetValue(IsDoubleTapZoomEnabledProperty);
        set => SetValue(IsDoubleTapZoomEnabledProperty, value);
    }

    /// <summary>How long a tap waits for a second one. This is also the lag before <see cref="Tapped"/>.</summary>
    public TimeSpan DoubleTapDelay
    {
        get => (TimeSpan)GetValue(DoubleTapDelayProperty);
        set => SetValue(DoubleTapDelayProperty, value);
    }

    /// <summary>Whether a swipe across a page that is not zoomed raises <see cref="NavigationRequested"/>. A pager turns this off.</summary>
    public bool IsSwipeNavigationEnabled
    {
        get => (bool)GetValue(IsSwipeNavigationEnabledProperty);
        set => SetValue(IsSwipeNavigationEnabledProperty, value);
    }

    /// <summary>Windows: whether keys zoom, pan and navigate. A pager sets this on the visible page only.</summary>
    public bool IsKeyboardEnabled
    {
        get => (bool)GetValue(IsKeyboardEnabledProperty);
        set => SetValue(IsKeyboardEnabledProperty, value);
    }

    public bool IsZoomed => (bool)GetValue(IsZoomedProperty);

    /// <summary>The current scale, 1 being the whole content fitted in the control.</summary>
    public double ZoomScale => (double)GetValue(ZoomScaleProperty);

    /// <summary>
    /// Moves to show <paramref name="normalizedRect"/> (0 to 1 across the image, which is not the control when the image is
    /// letterboxed) as large as it fits, centred where the edges allow. A rectangle that is empty or outside the image gives the
    /// whole page. A touch cancels the move. Completes with false when it was cancelled by a touch or a newer move.
    /// </summary>
    public Task<bool> ZoomToRect(Rect normalizedRect, bool animated = true, uint durationMs = 300, double padding = 0) =>
        this.MoveTo(
            _geometry.ZoomToRect(new ZoomRect(normalizedRect.X, normalizedRect.Y, normalizedRect.Width, normalizedRect.Height), padding),
            animated,
            durationMs);

    /// <summary>Back to the whole page.</summary>
    public Task<bool> Reset(bool animated = true, uint durationMs = 250) => this.MoveTo(ZoomState.Fit, animated, durationMs);

    /// <summary>The part of the image in view, 0 to 1 across the image. Chunk 15 uses this to know which panel is showing.</summary>
    public Rect VisibleRect
    {
        get
        {
            var visible = _geometry.VisibleRect(_state);
            return new Rect(visible.X, visible.Y, visible.Width, visible.Height);
        }
    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(Content))
        {
            this.ApplyState(_state);
        }
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
        _gestures?.Dispose();
        _gestures = null;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        _gestures = this.CreateGestureSource(this.Handler?.PlatformView);
        _gestures?.OnZoomChanged(this.IsZoomed);
    }

    private IZoomGestureSource CreateGestureSource(object platformView)
    {
#if ANDROID
        return platformView is Android.Views.View native ? new ZoomGestureSource(this, native) : null;
#elif WINDOWS
        return platformView is Microsoft.UI.Xaml.FrameworkElement native ? new ZoomGestureSource(this, native) : null;
#else
        return null;
#endif
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
        var previous = _geometry.View;
        _geometry = this.CreateGeometry();
        this.ApplyState(_geometry.Resize(_state, previous));
    }

    private void OnGeometryInputsChanged()
    {
        _geometry = this.CreateGeometry();
        this.ApplyState(_geometry.Clamp(_state));
    }

    private ZoomGeometry CreateGeometry() => new(new ZoomSize(this.Width, this.Height), this.ContentAspectRatio, this.MaxScale);

    private void RebuildTapDetector()
    {
        _taps?.Cancel();
        var delay = this.IsDoubleTapZoomEnabled ? this.DoubleTapDelay : TimeSpan.Zero;
        _taps = new TapDetector(delay, DoubleTapSlop, this.Schedule);
        _taps.SingleTap += point => this.Tapped?.Invoke(this, new ZoomTappedEventArgs(new Point(point.X, point.Y)));
        _taps.DoubleTap += point => _ = this.MoveTo(_geometry.DoubleTap(_state, point, this.DoubleTapScale), true, 250);
    }

    private IDisposable Schedule(TimeSpan delay, Action action)
    {
        var timer = this.Dispatcher.CreateTimer();
        timer.Interval = delay;
        timer.IsRepeating = false;
        void OnTick(object sender, EventArgs e)
        {
            timer.Tick -= OnTick;
            action();
        }

        timer.Tick += OnTick;
        timer.Start();
        return new Cancellation(() =>
        {
            timer.Tick -= OnTick;
            timer.Stop();
        });
    }

    private void ApplyState(ZoomState state)
    {
        var wasZoomed = this.IsZoomed;
        _state = state;
        if (this.Content is { } content)
        {
            // The origin is the top-left corner, which is what ZoomGeometry's transform assumes.
            content.AnchorX = 0;
            content.AnchorY = 0;
            content.Scale = state.Scale;
            content.TranslationX = state.TranslationX;
            content.TranslationY = state.TranslationY;
        }

        var isZoomed = ZoomGeometry.IsZoomed(state);
        this.SetValue(ZoomScalePropertyKey, state.Scale);
        this.SetValue(IsZoomedPropertyKey, isZoomed);
        if (isZoomed != wasZoomed)
        {
            _gestures?.OnZoomChanged(isZoomed);
        }
    }

    private Task<bool> MoveTo(ZoomState target, bool animated, uint durationMs)
    {
        this.CancelAnimation();
        target = _geometry.Clamp(target);
        if (!animated || durationMs == 0 || target == _state)
        {
            this.ApplyState(target);
            return Task.FromResult(true);
        }

        var from = _state;
        var finished = new TaskCompletionSource<bool>();
        var animation = new Animation(t => this.ApplyState(_geometry.Clamp(ZoomState.Lerp(from, target, t))), 0, 1, Easing.CubicInOut);
        animation.Commit(this, AnimationName, 16, durationMs, finished: (_, cancelled) => finished.TrySetResult(!cancelled));
        return finished.Task;
    }

    private void CancelAnimation() => this.AbortAnimation(AnimationName);

    // --- IZoomGestureHost -------------------------------------------------------------------

    Size IZoomGestureHost.ViewSize => new(this.Width, this.Height);

    bool IZoomGestureHost.CanPanHorizontally(double dx)
    {
        var bounds = _geometry.BoundsAt(_state.Scale);
        return dx switch
        {
            > 0 => _state.TranslationX < bounds.MaxX - 0.5,
            < 0 => _state.TranslationX > bounds.MinX + 0.5,
            _ => false,
        };
    }

    void IZoomGestureHost.GestureStarted() => this.CancelAnimation();

    void IZoomGestureHost.Pinch(double factor, Point previousFocus, Point focus) =>
        this.ApplyState(_geometry.Pinch(_state, factor, new ZoomPoint(previousFocus.X, previousFocus.Y), new ZoomPoint(focus.X, focus.Y)));

    void IZoomGestureHost.Pan(double dx, double dy) => this.ApplyState(_geometry.Pan(_state, dx, dy));

    void IZoomGestureHost.Tap(Point point) => _taps.Tap(new ZoomPoint(point.X, point.Y));

    void IZoomGestureHost.Wheel(double steps, Point focus) =>
        ((IZoomGestureHost)this).Pinch(Math.Pow(WheelStepFactor, steps), focus, focus);

    void IZoomGestureHost.Swipe(ZoomNavigation direction)
    {
        if (this.IsSwipeNavigationEnabled && !this.IsZoomed)
        {
            this.NavigationRequested?.Invoke(this, new ZoomNavigationEventArgs(direction));
        }
    }

    void IZoomGestureHost.Navigate(ZoomNavigation direction) =>
        this.NavigationRequested?.Invoke(this, new ZoomNavigationEventArgs(direction));

    void IZoomGestureHost.ResetZoom() => _ = this.Reset();

    /// <summary>The scale factor of one mouse wheel notch.</summary>
    private const double WheelStepFactor = 1.25;

    private static BindableProperty Create<T>(string name, T defaultValue, Action<ZoomPanView, T> changed = null) =>
        BindableProperty.Create(name, typeof(T), typeof(ZoomPanView), defaultValue,
            propertyChanged: changed is null ? null : (view, _, value) => changed((ZoomPanView)view, (T)value));

    private sealed class Cancellation(Action cancel) : IDisposable
    {
        public void Dispose() => cancel();
    }
}
