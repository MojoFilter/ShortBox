using Android.Views;
using AView = Android.Views.View;

namespace ShortBoxMobile;

/// <summary>
/// Android input for <see cref="ZoomPanView"/>: a <see cref="ScaleGestureDetector"/> for pinch (pixel focus, per-event factor)
/// and a <see cref="GestureDetector"/> for scroll, tap and fling, fed from one touch listener so the two can never disagree about
/// which pointer does what. The view is the control's own native view, which never moves, so coordinates stay stable.
/// </summary>
/// <remarks>
/// Parent cooperation follows the usual nested-scrolling contract (as PhotoView does inside a ViewPager). Not zoomed, a single
/// finger leaves the parent free to take horizontal drags, so a pager can turn pages. Zoomed, or with a second finger down, the
/// parent is told not to intercept, until a horizontal drag has nothing left to pan, when it is released again.
/// </remarks>
internal sealed class ZoomGestureSource : IZoomGestureSource
{
    /// <summary>A swipe must travel at least this far, in device-independent units, to turn a page.</summary>
    private const double SwipeDistance = 64;

    /// <summary>Width of the strip along each side claimed from the system back gesture while zoomed.</summary>
    private const double ExclusionWidth = 32;

    /// <summary>Android limits exclusion to about this much height per edge.</summary>
    private const double ExclusionHeight = 200;

    private readonly IZoomGestureHost _host;
    private readonly AView _view;
    private readonly ScaleGestureDetector _scaleDetector;
    private readonly GestureDetector _gestureDetector;
    private readonly float _density;
    private Point _lastFocus;
    private bool _disallowing;

    public ZoomGestureSource(IZoomGestureHost host, AView view)
    {
        _host = host;
        _view = view;
        _density = view.Resources?.DisplayMetrics?.Density ?? 1f;
        _scaleDetector = new ScaleGestureDetector(view.Context, new ScaleListener(this)) { QuickScaleEnabled = false };
        _gestureDetector = new GestureDetector(view.Context, new GestureListener(this)) { IsLongpressEnabled = false };
        view.Touch += this.OnTouch;
        view.LayoutChange += this.OnLayoutChange;
        this.OnZoomChanged(host.IsZoomed);
    }

    public void OnZoomChanged(bool isZoomed) => this.UpdateExclusion(isZoomed);

    public void Dispose()
    {
        _view.Touch -= this.OnTouch;
        _view.LayoutChange -= this.OnLayoutChange;
        _scaleDetector.Dispose();
        _gestureDetector.Dispose();
        this.UpdateExclusion(false);
    }

    private void OnTouch(object sender, AView.TouchEventArgs e)
    {
        var motion = e.Event;
        if (motion is null)
        {
            return;
        }

        switch (motion.ActionMasked)
        {
            case MotionEventActions.Down:
                _disallowing = false;
                _host.GestureStarted();
                this.Disallow(_host.IsZoomed);
                break;
            case MotionEventActions.PointerDown:
                this.Disallow(true);
                break;
        }

        _scaleDetector.OnTouchEvent(motion);
        _gestureDetector.OnTouchEvent(motion);
        e.Handled = true;
    }

    private void Disallow(bool disallow)
    {
        if (_disallowing != disallow)
        {
            _disallowing = disallow;
            _view.Parent?.RequestDisallowInterceptTouchEvent(disallow);
        }
    }

    private Point ToDip(float x, float y) => new(x / _density, y / _density);

    private void OnLayoutChange(object sender, AView.LayoutChangeEventArgs e) => this.UpdateExclusion(_host.IsZoomed);

    /// <summary>
    /// While zoomed, panning from the screen edge must not be taken as the system back gesture. Android only allows a strip
    /// of limited height per edge (and only from API 29), so it is centred on the screen.
    /// </summary>
    private void UpdateExclusion(bool isZoomed)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            return;
        }

        var rects = new List<Android.Graphics.Rect>();
        if (isZoomed && _view.Width > 0 && _view.Height > 0)
        {
            var width = (int)(ExclusionWidth * _density);
            var height = Math.Min(_view.Height, (int)(ExclusionHeight * _density));
            var top = (_view.Height - height) / 2;
            rects.Add(new Android.Graphics.Rect(0, top, width, top + height));
            rects.Add(new Android.Graphics.Rect(_view.Width - width, top, _view.Width, top + height));
        }

        _view.SystemGestureExclusionRects = rects;
    }

    private sealed class ScaleListener(ZoomGestureSource owner) : ScaleGestureDetector.SimpleOnScaleGestureListener
    {
        public override bool OnScaleBegin(ScaleGestureDetector detector)
        {
            owner._lastFocus = owner.ToDip(detector.FocusX, detector.FocusY);
            owner.Disallow(true);
            return true;
        }

        public override bool OnScale(ScaleGestureDetector detector)
        {
            var focus = owner.ToDip(detector.FocusX, detector.FocusY);
            owner._host.Pinch(detector.ScaleFactor, owner._lastFocus, focus);
            owner._lastFocus = focus;
            return true;
        }
    }

    private sealed class GestureListener(ZoomGestureSource owner) : GestureDetector.SimpleOnGestureListener
    {
        public override bool OnDown(MotionEvent e) => true;

        public override bool OnScroll(MotionEvent e1, MotionEvent e2, float distanceX, float distanceY)
        {
            if (owner._scaleDetector.IsInProgress)
            {
                // The pinch's moving focus already pans.
                return false;
            }

            // Scroll distances are how far the content should move the other way.
            var dx = -distanceX / owner._density;
            var dy = -distanceY / owner._density;
            owner._host.Pan(dx, dy);
            if (owner._host.IsZoomed)
            {
                // Out of room to pan sideways: let a parent pager have the rest of the drag.
                var handOff = Math.Abs(dx) > Math.Abs(dy) && !owner._host.CanPanHorizontally(dx);
                owner.Disallow(!handOff);
            }

            return true;
        }

        public override bool OnSingleTapUp(MotionEvent e)
        {
            owner._host.Tap(owner.ToDip(e.GetX(), e.GetY()));
            return true;
        }

        public override bool OnFling(MotionEvent e1, MotionEvent e2, float velocityX, float velocityY)
        {
            if (e1 is null || owner._host.IsZoomed || owner._scaleDetector.IsInProgress)
            {
                return false;
            }

            var dx = (e2.GetX() - e1.GetX()) / owner._density;
            var dy = (e2.GetY() - e1.GetY()) / owner._density;
            if (Math.Abs(dx) < SwipeDistance || Math.Abs(dx) < Math.Abs(dy) * 1.5)
            {
                return false;
            }

            // A swipe towards the left reveals the next page.
            owner._host.Swipe(dx < 0 ? ZoomNavigation.Next : ZoomNavigation.Previous);
            return true;
        }
    }
}
