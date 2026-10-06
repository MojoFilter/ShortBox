namespace ShortBox.Azure.Zoom;

// Plain value types so the zoom math has no dependency on MAUI and can be unit tested.

public readonly record struct ZoomPoint(double X, double Y);

public readonly record struct ZoomSize(double Width, double Height)
{
    public bool IsEmpty => !(Width > 0 && Height > 0);
}

public readonly record struct ZoomRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double CenterX => X + Width / 2;

    public double CenterY => Y + Height / 2;
}

/// <summary>
/// How the content is shown inside the view. A content point <c>p</c> (in view coordinates at scale 1) lands on screen at
/// <c>Translation + Scale * p</c>, so the transform origin is the view's top-left corner.
/// </summary>
public readonly record struct ZoomState(double Scale, double TranslationX, double TranslationY)
{
    /// <summary>The whole page fitted in the view.</summary>
    public static ZoomState Fit => new(1, 0, 0);

    public static ZoomState Lerp(ZoomState from, ZoomState to, double t) => new(
        from.Scale + (to.Scale - from.Scale) * t,
        from.TranslationX + (to.TranslationX - from.TranslationX) * t,
        from.TranslationY + (to.TranslationY - from.TranslationY) * t);
}

/// <summary>The range the translation may take at a given scale.</summary>
public readonly record struct ZoomBounds(double MinX, double MaxX, double MinY, double MaxY);
