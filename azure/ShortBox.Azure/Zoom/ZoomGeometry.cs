namespace ShortBox.Azure.Zoom;

/// <summary>
/// The pan and zoom rules for one content in one view: the scale and translation limits, focal-point zooming, and the
/// conversion from a normalized rectangle to a scale and translation. Immutable and free of UI types; the control makes a
/// new one when the view size, the content aspect ratio or the maximum scale changes.
/// </summary>
/// <remarks>
/// Content is assumed to be aspect-fitted and centred in the view (an <c>Image</c> with <c>AspectFit</c>), so its real
/// rectangle is smaller than the view when letterboxed. Bounds use that rectangle, so a page never pans into empty space.
/// </remarks>
public sealed class ZoomGeometry
{
    /// <summary>The scale at which the whole content fits the view. Zooming out further is not allowed.</summary>
    public const double MinScale = 1;

    /// <summary>Above this a state counts as zoomed in. A little slack avoids float noise at the fit scale.</summary>
    public const double ZoomedThreshold = 1.001;

    public ZoomGeometry(ZoomSize view, double? contentAspectRatio = null, double maxScale = 4)
    {
        this.View = view;
        this.MaxScale = double.IsFinite(maxScale) ? Math.Max(maxScale, MinScale) : MinScale;
        this.ContentRect = FitRect(view, contentAspectRatio);
    }

    public ZoomSize View { get; }

    public double MaxScale { get; }

    /// <summary>Where the content sits in the view at scale 1, in view coordinates.</summary>
    public ZoomRect ContentRect { get; }

    public static bool IsZoomed(ZoomState state) => state.Scale > ZoomedThreshold;

    /// <summary>The aspect-fit rectangle of content with the given width / height ratio, centred in the view.</summary>
    public static ZoomRect FitRect(ZoomSize view, double? aspectRatio)
    {
        var whole = new ZoomRect(0, 0, view.Width, view.Height);
        if (view.IsEmpty || aspectRatio is not double ratio || !(ratio > 0) || !double.IsFinite(ratio))
        {
            return whole;
        }

        if (ratio > view.Width / view.Height)
        {
            var height = view.Width / ratio;
            return new ZoomRect(0, (view.Height - height) / 2, view.Width, height);
        }

        var width = view.Height * ratio;
        return new ZoomRect((view.Width - width) / 2, 0, width, view.Height);
    }

    /// <summary>
    /// The limits of the translation at <paramref name="scale"/>. An axis on which the content is smaller than the view
    /// has equal limits (the content is centred), otherwise the content edges may not leave the view.
    /// </summary>
    public ZoomBounds BoundsAt(double scale)
    {
        scale = ClampScale(scale);
        var (minX, maxX) = AxisBounds(scale, this.View.Width, this.ContentRect.X, this.ContentRect.Width);
        var (minY, maxY) = AxisBounds(scale, this.View.Height, this.ContentRect.Y, this.ContentRect.Height);
        return new ZoomBounds(minX, maxX, minY, maxY);
    }

    /// <summary>Brings a state back into the allowed scale and translation range.</summary>
    public ZoomState Clamp(ZoomState state)
    {
        if (this.View.IsEmpty)
        {
            return ZoomState.Fit;
        }

        var scale = ClampScale(state.Scale);
        var bounds = this.BoundsAt(scale);
        return new ZoomState(
            scale,
            Math.Clamp(Finite(state.TranslationX), bounds.MinX, bounds.MaxX),
            Math.Clamp(Finite(state.TranslationY), bounds.MinY, bounds.MaxY));
    }

    public ZoomState Pan(ZoomState state, double dx, double dy) =>
        this.Clamp(state with { TranslationX = state.TranslationX + dx, TranslationY = state.TranslationY + dy });

    /// <summary>
    /// One step of a pinch. The content point that was under <paramref name="previousFocus"/> ends up under
    /// <paramref name="focus"/>, so zooming stays on the fingers and moving them pans. Once a limit is hit the point drifts
    /// rather than the zoom jumping.
    /// </summary>
    public ZoomState Pinch(ZoomState state, double factor, ZoomPoint previousFocus, ZoomPoint focus)
    {
        if (!(factor > 0) || !double.IsFinite(factor))
        {
            factor = 1;
        }

        state = this.Clamp(state);
        var scale = ClampScale(state.Scale * factor);
        var anchorX = (previousFocus.X - state.TranslationX) / state.Scale;
        var anchorY = (previousFocus.Y - state.TranslationY) / state.Scale;
        return this.Clamp(new ZoomState(scale, focus.X - scale * anchorX, focus.Y - scale * anchorY));
    }

    /// <summary>Zooms to an absolute scale keeping the content point under <paramref name="focus"/> fixed.</summary>
    public ZoomState ZoomAbout(ZoomState state, ZoomPoint focus, double newScale)
    {
        state = this.Clamp(state);
        return this.Pinch(state, newScale / state.Scale, focus, focus);
    }

    /// <summary>Double tap: back to fit when zoomed in, otherwise in to <paramref name="zoomedScale"/> about the tap.</summary>
    public ZoomState DoubleTap(ZoomState state, ZoomPoint focus, double zoomedScale) =>
        IsZoomed(this.Clamp(state)) ? ZoomState.Fit : this.ZoomAbout(state, focus, zoomedScale);

    /// <summary>
    /// The state that shows <paramref name="normalized"/> (0..1 across the content, not the view) as large as it fits, centred
    /// where the bounds allow. <paramref name="padding"/> keeps that fraction of the view free on each side. The scale is limited
    /// to <see cref="MaxScale"/>, so a tiny rectangle is shown at the maximum rather than exactly. A rectangle that is empty or
    /// outside the content gives the fit state.
    /// </summary>
    public ZoomState ZoomToRect(ZoomRect normalized, double padding = 0)
    {
        var left = Math.Clamp(Finite(normalized.X), 0, 1);
        var top = Math.Clamp(Finite(normalized.Y), 0, 1);
        var right = Math.Clamp(Finite(normalized.Right), 0, 1);
        var bottom = Math.Clamp(Finite(normalized.Bottom), 0, 1);
        if (this.View.IsEmpty || right <= left || bottom <= top)
        {
            return ZoomState.Fit;
        }

        var content = this.ContentRect;
        var target = new ZoomRect(
            content.X + left * content.Width,
            content.Y + top * content.Height,
            (right - left) * content.Width,
            (bottom - top) * content.Height);
        var free = 1 - 2 * Math.Clamp(padding, 0, 0.4);
        var scale = ClampScale(Math.Min(this.View.Width * free / target.Width, this.View.Height * free / target.Height));
        return this.Clamp(new ZoomState(
            scale,
            this.View.Width / 2 - scale * target.CenterX,
            this.View.Height / 2 - scale * target.CenterY));
    }

    /// <summary>The part of the content in view, normalized the same way as <see cref="ZoomToRect"/>.</summary>
    public ZoomRect VisibleRect(ZoomState state)
    {
        state = this.Clamp(state);
        var content = this.ContentRect;
        if (content.Width <= 0 || content.Height <= 0)
        {
            return new ZoomRect(0, 0, 1, 1);
        }

        var left = Math.Max(-state.TranslationX / state.Scale, content.X);
        var top = Math.Max(-state.TranslationY / state.Scale, content.Y);
        var right = Math.Min((this.View.Width - state.TranslationX) / state.Scale, content.Right);
        var bottom = Math.Min((this.View.Height - state.TranslationY) / state.Scale, content.Bottom);
        return new ZoomRect(
            (left - content.X) / content.Width,
            (top - content.Y) / content.Height,
            Math.Max(right - left, 0) / content.Width,
            Math.Max(bottom - top, 0) / content.Height);
    }

    /// <summary>
    /// Carries a state made for a view of <paramref name="previousView"/> over to this view: the same part of the page stays
    /// in view when the view grows or shrinks (the nav bar showing, a rotation).
    /// </summary>
    public ZoomState Resize(ZoomState state, ZoomSize previousView)
    {
        if (previousView.IsEmpty)
        {
            return this.Clamp(state);
        }

        return this.Clamp(state with
        {
            TranslationX = state.TranslationX * this.View.Width / previousView.Width,
            TranslationY = state.TranslationY * this.View.Height / previousView.Height,
        });
    }

    private double ClampScale(double scale) =>
        double.IsNaN(scale) ? MinScale : Math.Clamp(scale, MinScale, this.MaxScale);

    private static (double Min, double Max) AxisBounds(double scale, double viewLength, double contentStart, double contentLength)
    {
        if (scale * contentLength <= viewLength)
        {
            var centred = viewLength / 2 - scale * (contentStart + contentLength / 2);
            return (centred, centred);
        }

        return (viewLength - scale * (contentStart + contentLength), -scale * contentStart);
    }

    private static double Finite(double value) => double.IsFinite(value) ? value : 0;
}
