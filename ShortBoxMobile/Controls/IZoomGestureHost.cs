namespace ShortBoxMobile;

/// <summary>Which way a gesture or key asks the host to move through a book. Assumes left-to-right reading.</summary>
public enum ZoomNavigation
{
    Previous,
    Next,
}

public sealed class ZoomTappedEventArgs(Point position) : EventArgs
{
    /// <summary>Where the tap landed, in the <see cref="ZoomPanView"/>'s own coordinates (not the zoomed content's).</summary>
    public Point Position { get; } = position;
}

public sealed class ZoomNavigationEventArgs(ZoomNavigation direction) : EventArgs
{
    public ZoomNavigation Direction { get; } = direction;
}

/// <summary>
/// What a platform gesture source can ask of the <see cref="ZoomPanView"/> it is attached to. The sources only translate
/// native touch, pointer and key input into these calls; the zoom rules live in <c>ShortBox.Azure.Zoom</c> and the control.
/// Positions and distances are in device-independent units, relative to the control.
/// </summary>
internal interface IZoomGestureHost
{
    bool IsZoomed { get; }

    bool IsKeyboardEnabled { get; }

    Size ViewSize { get; }

    /// <summary>Whether the content can still move by <paramref name="dx"/>'s sign (positive is to the right) before hitting an edge.</summary>
    bool CanPanHorizontally(double dx);

    /// <summary>A finger or button went down: stop any animation that is running.</summary>
    void GestureStarted();

    /// <summary>Scale by <paramref name="factor"/>, keeping the content under <paramref name="previousFocus"/> under <paramref name="focus"/>.</summary>
    void Pinch(double factor, Point previousFocus, Point focus);

    void Pan(double dx, double dy);

    /// <summary>A completed tap, before single and double taps are told apart.</summary>
    void Tap(Point point);

    /// <summary>Mouse wheel notches (positive zooms in) about a point.</summary>
    void Wheel(double steps, Point focus);

    /// <summary>A swipe across the control. Only acted on when not zoomed and swipe navigation is enabled.</summary>
    void Swipe(ZoomNavigation direction);

    /// <summary>A key asked to move through the book.</summary>
    void Navigate(ZoomNavigation direction);

    void ResetZoom();
}

/// <summary>The native hook of one control. Disposed when the control's handler goes away.</summary>
internal interface IZoomGestureSource : IDisposable
{
    /// <summary>The zoomed state changed (Android uses it to claim the screen edges from the system back gesture).</summary>
    void OnZoomChanged(bool isZoomed);
}
