using ShortBox.Azure.Zoom;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class ZoomGeometryTests
{
    private const double Tolerance = 1e-6;

    private static readonly ZoomSize Phone = new(400, 800);

    /// <summary>A double page spread (3975x3056) in a portrait view: letterboxed top and bottom.</summary>
    private const double SpreadRatio = 3975.0 / 3056.0;

    private static ZoomGeometry Plain(double max = 4) => new(Phone, null, max);

    private static void AreClose(double expected, double actual, string? what = null) =>
        Assert.AreEqual(expected, actual, Tolerance, what);

    private static ZoomPoint ContentPointUnder(ZoomState state, ZoomPoint screen) =>
        new((screen.X - state.TranslationX) / state.Scale, (screen.Y - state.TranslationY) / state.Scale);

    // --- aspect-fit rectangle -------------------------------------------------------------

    [TestMethod]
    public void WithoutAnAspectRatioTheContentIsTheWholeView()
    {
        Assert.AreEqual(new ZoomRect(0, 0, 400, 800), Plain().ContentRect);
    }

    [TestMethod]
    public void AWideSpreadIsLetterboxedTopAndBottom()
    {
        var rect = new ZoomGeometry(Phone, SpreadRatio).ContentRect;
        AreClose(0, rect.X);
        AreClose(400, rect.Width);
        AreClose(400 / SpreadRatio, rect.Height);
        AreClose((800 - rect.Height) / 2, rect.Y);
    }

    [TestMethod]
    public void ANarrowPageIsPillarboxedLeftAndRight()
    {
        var rect = new ZoomGeometry(Phone, 0.4).ContentRect;
        Assert.AreEqual(new ZoomRect(40, 0, 320, 800), rect);
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void ABadAspectRatioFallsBackToTheView(double ratio)
    {
        Assert.AreEqual(new ZoomRect(0, 0, 400, 800), new ZoomGeometry(Phone, ratio).ContentRect);
    }

    // --- clamping -------------------------------------------------------------------------

    [TestMethod]
    public void ScaleIsClampedBetweenFitAndTheMaximum()
    {
        var geometry = Plain(3);
        AreClose(1, geometry.Clamp(new ZoomState(0.4, 0, 0)).Scale);
        AreClose(3, geometry.Clamp(new ZoomState(10, 0, 0)).Scale);
        AreClose(1, geometry.Clamp(new ZoomState(double.NaN, 0, 0)).Scale);
    }

    [TestMethod]
    public void AMaximumBelowFitStillAllowsFit()
    {
        AreClose(1, Plain(0.2).MaxScale);
    }

    [TestMethod]
    public void TranslationIsClampedToTheEdgesOfTheContent()
    {
        var geometry = Plain();
        var pannedFarRight = geometry.Pan(new ZoomState(2, 0, 0), 10_000, 10_000);
        AreClose(0, pannedFarRight.TranslationX);
        AreClose(0, pannedFarRight.TranslationY);

        var pannedFarLeft = geometry.Pan(new ZoomState(2, 0, 0), -10_000, -10_000);
        AreClose(400 - 2 * 400, pannedFarLeft.TranslationX);
        AreClose(800 - 2 * 800, pannedFarLeft.TranslationY);
    }

    [TestMethod]
    public void AtFitThereIsNothingToPan()
    {
        Assert.AreEqual(ZoomState.Fit, Plain().Pan(ZoomState.Fit, 50, -50));
    }

    [TestMethod]
    public void NonFiniteTranslationDoesNotPoisonTheState()
    {
        var state = Plain().Clamp(new ZoomState(2, double.NaN, double.PositiveInfinity));
        Assert.IsTrue(double.IsFinite(state.TranslationX) && double.IsFinite(state.TranslationY));
    }

    [TestMethod]
    public void AnEmptyViewIsAlwaysFit()
    {
        var geometry = new ZoomGeometry(new ZoomSize(0, 0));
        Assert.AreEqual(ZoomState.Fit, geometry.Clamp(new ZoomState(3, 40, 40)));
    }

    // --- letterbox-aware bounds -----------------------------------------------------------

    [TestMethod]
    public void ASpreadDoesNotPanVerticallyWhileItIsShorterThanTheView()
    {
        var geometry = new ZoomGeometry(Phone, SpreadRatio);
        var state = geometry.Pan(new ZoomState(2, 0, 0), 0, 300);

        // 2x of a 307 tall spread is 615, still shorter than the 800 view, so it stays centred.
        var centred = geometry.BoundsAt(2);
        Assert.AreEqual(centred.MinY, centred.MaxY);
        AreClose(centred.MinY, state.TranslationY);
    }

    [TestMethod]
    public void ASpreadStopsPanningAtItsRealTopAndBottomOnceItIsTallerThanTheView()
    {
        var geometry = new ZoomGeometry(Phone, SpreadRatio);
        var content = geometry.ContentRect;

        var top = geometry.Pan(new ZoomState(4, 0, 0), 0, 10_000);
        var bottom = geometry.Pan(new ZoomState(4, 0, 0), 0, -10_000);

        // At the extremes the content edge sits exactly on the view edge, so there is no empty space to pan into.
        AreClose(0, top.TranslationY + 4 * content.Y, "content top meets the view top");
        AreClose(800, bottom.TranslationY + 4 * content.Bottom, "content bottom meets the view bottom");
    }

    [TestMethod]
    public void ANarrowPageCannotPanIntoThePillarbox()
    {
        var geometry = new ZoomGeometry(Phone, 0.4);
        var left = geometry.Pan(new ZoomState(2, 0, 0), 10_000, 0);
        var right = geometry.Pan(new ZoomState(2, 0, 0), -10_000, 0);

        // Page spans x 40..360 at fit, so at 2x it spans 80..720 plus the translation.
        AreClose(0, left.TranslationX + 2 * 40);
        AreClose(400, right.TranslationX + 2 * 360);
    }

    [TestMethod]
    public void FitIsAlwaysTheZeroTranslationState()
    {
        foreach (var ratio in new double?[] { null, SpreadRatio, 0.4, 0.5, 0.75 })
        {
            Assert.AreEqual(ZoomState.Fit, new ZoomGeometry(Phone, ratio).Clamp(ZoomState.Fit), $"ratio {ratio}");
        }
    }

    // --- focal point ----------------------------------------------------------------------

    [TestMethod]
    public void PinchKeepsThePointUnderTheFingersFixed()
    {
        var geometry = Plain();
        var focus = new ZoomPoint(100, 200);
        var before = ContentPointUnder(ZoomState.Fit, focus);

        var after = geometry.Pinch(ZoomState.Fit, 2, focus, focus);

        AreClose(2, after.Scale);
        var under = ContentPointUnder(after, focus);
        AreClose(before.X, under.X);
        AreClose(before.Y, under.Y);
    }

    [TestMethod]
    public void RepeatedPinchStepsKeepTheSamePointFixed()
    {
        var geometry = Plain();
        var focus = new ZoomPoint(150, 300);
        var anchor = ContentPointUnder(ZoomState.Fit, focus);
        var state = ZoomState.Fit;

        foreach (var factor in new[] { 1.2, 1.1, 1.3, 0.9, 1.05 })
        {
            state = geometry.Pinch(state, factor, focus, focus);
        }

        var under = ContentPointUnder(state, focus);
        AreClose(anchor.X, under.X);
        AreClose(anchor.Y, under.Y);
    }

    [TestMethod]
    public void PinchingAtACornerStaysWithinTheBounds()
    {
        var geometry = Plain();
        var corner = geometry.Pinch(ZoomState.Fit, 2, new ZoomPoint(400, 800), new ZoomPoint(400, 800));
        AreClose(-400, corner.TranslationX);
        AreClose(-800, corner.TranslationY);
    }

    [TestMethod]
    public void MovingTheFingersWhilePinchingPans()
    {
        var geometry = Plain();
        var start = new ZoomState(2, -200, -200);
        var panned = geometry.Pinch(start, 1, new ZoomPoint(200, 400), new ZoomPoint(230, 440));
        AreClose(2, panned.Scale);
        AreClose(-170, panned.TranslationX);
        AreClose(-160, panned.TranslationY);
    }

    [TestMethod]
    public void PinchingPastTheMaximumStopsThereAndStillFollowsTheFingers()
    {
        var geometry = Plain(3);
        var state = geometry.Pinch(new ZoomState(2.9, -100, -100), 100, new ZoomPoint(200, 400), new ZoomPoint(210, 400));
        AreClose(3, state.Scale);
    }

    [TestMethod]
    public void PinchingBelowFitStopsAtFit()
    {
        var state = Plain().Pinch(new ZoomState(1.2, -40, -80), 0.1, new ZoomPoint(200, 400), new ZoomPoint(200, 400));
        Assert.AreEqual(ZoomState.Fit, state);
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-2.0)]
    [DataRow(double.NaN)]
    public void ANonsenseFactorLeavesTheScaleAlone(double factor)
    {
        var start = new ZoomState(2, -100, -100);
        Assert.AreEqual(start, Plain().Pinch(start, factor, new ZoomPoint(10, 10), new ZoomPoint(10, 10)));
    }

    // --- double tap -----------------------------------------------------------------------

    [TestMethod]
    public void DoubleTapZoomsInAboutTheTap()
    {
        var geometry = Plain();
        var tap = new ZoomPoint(100, 200);
        var state = geometry.DoubleTap(ZoomState.Fit, tap, 2.5);

        AreClose(2.5, state.Scale);
        var under = ContentPointUnder(state, tap);
        AreClose(100, under.X);
        AreClose(200, under.Y);
    }

    [TestMethod]
    public void DoubleTapWhileZoomedGoesBackToFit()
    {
        Assert.AreEqual(ZoomState.Fit, Plain().DoubleTap(new ZoomState(2, -100, -100), new ZoomPoint(10, 10), 2.5));
    }

    // --- ZoomToRect -----------------------------------------------------------------------

    [TestMethod]
    public void ZoomToRectFitsAndCentresTheRectangle()
    {
        var geometry = Plain();
        var state = geometry.ZoomToRect(new ZoomRect(0.25, 0.25, 0.5, 0.25));

        // The rectangle is 200x200 in a 400x800 view, so width is the limit.
        AreClose(2, state.Scale);
        AreClose(-200, state.TranslationX);
        AreClose(-200, state.TranslationY);

        var visible = geometry.VisibleRect(state);
        AreClose(0.5, visible.CenterX);
        AreClose(0.375, visible.CenterY);
    }

    [TestMethod]
    public void ZoomToRectWithPaddingLeavesMargins()
    {
        var state = Plain().ZoomToRect(new ZoomRect(0.25, 0.25, 0.5, 0.25), padding: 0.1);
        AreClose(1.6, state.Scale);
    }

    [TestMethod]
    public void ZoomToRectOfTheWholePageIsFit()
    {
        Assert.AreEqual(ZoomState.Fit, Plain().ZoomToRect(new ZoomRect(0, 0, 1, 1)));
    }

    [TestMethod]
    public void ZoomToRectOfAWholeLetterboxedPageIsFit()
    {
        var state = new ZoomGeometry(Phone, SpreadRatio).ZoomToRect(new ZoomRect(0, 0, 1, 1));
        AreClose(1, state.Scale);
        AreClose(0, state.TranslationX);
        AreClose(0, state.TranslationY);
    }

    [TestMethod]
    public void ZoomToRectIsRelativeToTheImageNotTheView()
    {
        var geometry = new ZoomGeometry(Phone, SpreadRatio);
        var state = geometry.ZoomToRect(new ZoomRect(0.5, 0, 0.5, 1));

        // The right half of the spread: 200 wide in the view, so 2x, pinned to the right edge.
        AreClose(2, state.Scale);
        AreClose(-400, state.TranslationX);
        var visible = geometry.VisibleRect(state);
        AreClose(0.5, visible.X);
        AreClose(0.5, visible.Width);
        AreClose(0, visible.Y);
        AreClose(1, visible.Height);
    }

    [TestMethod]
    public void ZoomToATinyRectIsLimitedToTheMaximumScale()
    {
        var state = Plain().ZoomToRect(new ZoomRect(0, 0, 0.1, 0.1));

        // 10x would be wanted; 4x is the limit, and the corner rectangle then sits at the top left.
        AreClose(4, state.Scale);
        AreClose(0, state.TranslationX);
        AreClose(0, state.TranslationY);
    }

    [TestMethod]
    public void ZoomToARectAtTheBottomRightCornerStopsAtTheBounds()
    {
        var state = Plain().ZoomToRect(new ZoomRect(0.9, 0.9, 0.1, 0.1));
        AreClose(4, state.Scale);
        AreClose(400 - 4 * 400, state.TranslationX);
        AreClose(800 - 4 * 800, state.TranslationY);
    }

    [TestMethod]
    public void ZoomToRectOnALongThinRectUsesTheTighterAxis()
    {
        // 0.1 wide and 0.9 tall is 40x720: height gives 1.11, width 10, so height rules.
        var state = Plain().ZoomToRect(new ZoomRect(0.45, 0.05, 0.1, 0.9));
        AreClose(800.0 / 720, state.Scale);
    }

    [TestMethod]
    public void ZoomToRectOutsideTheContentIsClippedToIt()
    {
        Assert.AreEqual(ZoomState.Fit, Plain().ZoomToRect(new ZoomRect(-1, -1, 3, 3)));

        // Clipped to x 0.5..1, y 0..0.5: 200x400 in the view, so 2x.
        var halfOff = Plain().ZoomToRect(new ZoomRect(0.5, 0, 1, 0.5));
        AreClose(2, halfOff.Scale);
    }

    [TestMethod]
    public void ZoomToAnEmptyOrBrokenRectIsFit()
    {
        var geometry = Plain();
        Assert.AreEqual(ZoomState.Fit, geometry.ZoomToRect(new ZoomRect(0.5, 0.5, 0, 0.2)));
        Assert.AreEqual(ZoomState.Fit, geometry.ZoomToRect(new ZoomRect(0.5, 0.5, -0.2, 0.2)));
        Assert.AreEqual(ZoomState.Fit, geometry.ZoomToRect(new ZoomRect(2, 2, 0.5, 0.5)));
        Assert.AreEqual(ZoomState.Fit, geometry.ZoomToRect(new ZoomRect(double.NaN, double.NaN, double.NaN, double.NaN)));
    }

    // --- visible rect, resize, interpolation ----------------------------------------------

    [TestMethod]
    public void AtFitTheWholeContentIsVisible()
    {
        Assert.AreEqual(new ZoomRect(0, 0, 1, 1), Plain().VisibleRect(ZoomState.Fit));
        var letterboxed = new ZoomGeometry(Phone, SpreadRatio).VisibleRect(ZoomState.Fit);
        AreClose(0, letterboxed.X);
        AreClose(0, letterboxed.Y);
        AreClose(1, letterboxed.Width);
        AreClose(1, letterboxed.Height);
    }

    [TestMethod]
    public void ResizingKeepsTheSamePartOfThePageInView()
    {
        var before = Plain();
        var state = before.Pan(new ZoomState(2, 0, 0), -100, -200);
        var visibleBefore = before.VisibleRect(state);

        var taller = new ZoomGeometry(new ZoomSize(400, 700));
        var resized = taller.Resize(state, Phone);
        var visibleAfter = taller.VisibleRect(resized);

        AreClose(2, resized.Scale);
        AreClose(visibleBefore.X, visibleAfter.X);
        AreClose(visibleBefore.Y, visibleAfter.Y);
    }

    [TestMethod]
    public void ResizingFromAnEmptyViewJustClamps()
    {
        var geometry = Plain();
        Assert.AreEqual(geometry.Clamp(new ZoomState(2, -10, -10)), geometry.Resize(new ZoomState(2, -10, -10), new ZoomSize(0, 0)));
    }

    [TestMethod]
    public void LerpIsLinearBetweenTheEnds()
    {
        var from = new ZoomState(1, 0, 0);
        var to = new ZoomState(3, -200, -400);
        Assert.AreEqual(from, ZoomState.Lerp(from, to, 0));
        Assert.AreEqual(to, ZoomState.Lerp(from, to, 1));
        Assert.AreEqual(new ZoomState(2, -100, -200), ZoomState.Lerp(from, to, 0.5));
    }

    [TestMethod]
    public void ADoubleTapAnimationKeepsTheTapPointStillThroughout()
    {
        var geometry = Plain();
        var tap = new ZoomPoint(120, 330);
        var target = geometry.DoubleTap(ZoomState.Fit, tap, 2.5);
        var anchor = ContentPointUnder(ZoomState.Fit, tap);

        for (var t = 0.0; t <= 1.0; t += 0.1)
        {
            var under = ContentPointUnder(ZoomState.Lerp(ZoomState.Fit, target, t), tap);
            AreClose(anchor.X, under.X);
            AreClose(anchor.Y, under.Y);
        }
    }

    // --- pan room (what a pager needs to know) --------------------------------------------

    [TestMethod]
    public void BoundsShowWhetherThereIsRoomToPanEachWay()
    {
        var geometry = Plain();
        var bounds = geometry.BoundsAt(2);
        Assert.AreEqual(-400, bounds.MinX, Tolerance);
        Assert.AreEqual(0, bounds.MaxX, Tolerance);

        // At the left edge (translation 0) there is room to pan left, none to pan right.
        var atLeftEdge = new ZoomState(2, 0, 0);
        Assert.IsTrue(atLeftEdge.TranslationX > bounds.MinX);
        Assert.IsFalse(atLeftEdge.TranslationX < bounds.MaxX);
    }
}
