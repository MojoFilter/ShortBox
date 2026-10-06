using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinPoint = Windows.Foundation.Point;

namespace ShortBoxMobile;

/// <summary>
/// Windows input for <see cref="ZoomPanView"/>: the mouse wheel zooms about the pointer, a drag pans, a click is a tap, and keys
/// zoom, pan and turn pages. A single touch or pen contact pans like a mouse drag; there is no two-finger pinch.
/// </summary>
/// <remarks>
/// Keys are caught on the window's root so they work without the control holding keyboard focus, which a panel cannot take.
/// </remarks>
internal sealed class ZoomGestureSource : IZoomGestureSource
{
    /// <summary>Movement under this, in device-independent units, is still a click.</summary>
    private const double TapSlop = 6;

    private static readonly TimeSpan TapTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>A drag across a page that is not zoomed turns it past this distance.</summary>
    private const double SwipeDistance = 80;

    private const double KeyPanStep = 80;

    private readonly IZoomGestureHost _host;
    private readonly FrameworkElement _element;
    private UIElement _root;
    private uint? _pointerId;
    private WinPoint _start;
    private WinPoint _last;
    private DateTime _pressedAt;
    private bool _moved;

    public ZoomGestureSource(IZoomGestureHost host, FrameworkElement element)
    {
        _host = host;
        _element = element;

        // A panel with no background is invisible to hit testing, so it would see no pointer input at all.
        if (element is Microsoft.UI.Xaml.Controls.Panel { Background: null } panel)
        {
            panel.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        element.PointerWheelChanged += this.OnWheel;
        element.PointerPressed += this.OnPressed;
        element.PointerMoved += this.OnMoved;
        element.PointerReleased += this.OnReleased;
        element.PointerCanceled += this.OnCanceled;
        element.PointerCaptureLost += this.OnCanceled;
        element.Loaded += this.OnLoaded;
        element.Unloaded += this.OnUnloaded;
        if (element.IsLoaded)
        {
            this.OnLoaded(element, null);
        }
    }

    public void OnZoomChanged(bool isZoomed)
    {
    }

    public void Dispose()
    {
        _element.PointerWheelChanged -= this.OnWheel;
        _element.PointerPressed -= this.OnPressed;
        _element.PointerMoved -= this.OnMoved;
        _element.PointerReleased -= this.OnReleased;
        _element.PointerCanceled -= this.OnCanceled;
        _element.PointerCaptureLost -= this.OnCanceled;
        _element.Loaded -= this.OnLoaded;
        _element.Unloaded -= this.OnUnloaded;
        this.DetachKeys();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        this.DetachKeys();
        _root = _element.XamlRoot?.Content;
        if (_root is not null)
        {
            _root.KeyDown += this.OnKeyDown;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => this.DetachKeys();

    private void DetachKeys()
    {
        if (_root is not null)
        {
            _root.KeyDown -= this.OnKeyDown;
            _root = null;
        }
    }

    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_element);
        _host.GestureStarted();
        _host.Wheel(point.Properties.MouseWheelDelta / 120.0, ToPoint(point.Position));
        e.Handled = true;
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_element);
        var isMouse = point.PointerDeviceType == PointerDeviceType.Mouse;
        if (_pointerId is not null || (isMouse && !point.Properties.IsLeftButtonPressed))
        {
            return;
        }

        _pointerId = point.PointerId;
        _start = _last = point.Position;
        _pressedAt = DateTime.UtcNow;
        _moved = false;
        _host.GestureStarted();
        _element.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerId != e.Pointer.PointerId)
        {
            return;
        }

        var position = e.GetCurrentPoint(_element).Position;
        if (!_moved && Distance(position, _start) > TapSlop)
        {
            _moved = true;
        }

        if (_moved)
        {
            _host.Pan(position.X - _last.X, position.Y - _last.Y);
        }

        _last = position;
        e.Handled = true;
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerId != e.Pointer.PointerId)
        {
            return;
        }

        var position = e.GetCurrentPoint(_element).Position;
        _pointerId = null;
        _element.ReleasePointerCapture(e.Pointer);

        if (!_moved)
        {
            if (DateTime.UtcNow - _pressedAt < TapTimeout)
            {
                _host.Tap(ToPoint(position));
            }
        }
        else if (!_host.IsZoomed)
        {
            var dx = position.X - _start.X;
            var dy = position.Y - _start.Y;
            if (Math.Abs(dx) >= SwipeDistance && Math.Abs(dx) > Math.Abs(dy) * 1.5)
            {
                _host.Swipe(dx < 0 ? ZoomNavigation.Next : ZoomNavigation.Previous);
            }
        }

        e.Handled = true;
    }

    private void OnCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerId == e.Pointer.PointerId)
        {
            _pointerId = null;
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_host.IsKeyboardEnabled)
        {
            return;
        }

        var centre = new Point(_host.ViewSize.Width / 2, _host.ViewSize.Height / 2);
        switch (e.Key)
        {
            case VirtualKey.Add or (VirtualKey)187: // numpad + and the =/+ key
                _host.Wheel(1, centre);
                break;
            case VirtualKey.Subtract or (VirtualKey)189: // numpad - and the -/_ key
                _host.Wheel(-1, centre);
                break;
            case VirtualKey.Number0 or VirtualKey.NumberPad0:
                _host.ResetZoom();
                break;
            case VirtualKey.Left:
                this.Arrow(KeyPanStep, ZoomNavigation.Previous);
                break;
            case VirtualKey.Right:
                this.Arrow(-KeyPanStep, ZoomNavigation.Next);
                break;
            case VirtualKey.Up when _host.IsZoomed:
                _host.Pan(0, KeyPanStep);
                break;
            case VirtualKey.Down when _host.IsZoomed:
                _host.Pan(0, -KeyPanStep);
                break;
            case VirtualKey.PageDown or VirtualKey.Space:
                _host.Navigate(ZoomNavigation.Next);
                break;
            case VirtualKey.PageUp:
                _host.Navigate(ZoomNavigation.Previous);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>Pans while the page has room that way, otherwise turns the page.</summary>
    private void Arrow(double dx, ZoomNavigation navigation)
    {
        if (_host.IsZoomed && _host.CanPanHorizontally(dx))
        {
            _host.Pan(dx, 0);
        }
        else
        {
            _host.Navigate(navigation);
        }
    }

    private static Point ToPoint(WinPoint point) => new(point.X, point.Y);

    private static double Distance(WinPoint a, WinPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
