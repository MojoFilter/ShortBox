namespace ShortBoxMobile;

/// <summary>
/// One page in the pager: a <see cref="ZoomPanView"/> around the image and a <see cref="LoadStateView"/> for that page's own
/// loading and error state, bound to a <see cref="ReaderPage"/>. A pager recycles these views between pages, so the zoom is put
/// back to fitted whenever the page or its image changes, and never carries over to another page.
/// </summary>
public partial class ReaderPageView : ContentView
{
    private ReaderPage _page;

    public ReaderPageView()
    {
        InitializeComponent();
    }

    /// <summary>A tap on the page, in this view's coordinates, once it is clear it is not a double tap.</summary>
    public event EventHandler<ZoomTappedEventArgs> Tapped;

    /// <summary>A navigation key (Windows) asked to move to the previous or next page. Swipes are the pager's.</summary>
    public event EventHandler<ZoomNavigationEventArgs> NavigationRequested;

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_page is not null)
        {
            _page.PropertyChanged -= this.OnPagePropertyChanged;
        }

        _page = this.BindingContext as ReaderPage;
        if (_page is not null)
        {
            _page.PropertyChanged += this.OnPagePropertyChanged;
        }

        _ = this.zoomView.Reset(animated: false);
    }

    private void OnPagePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReaderPage.Source))
        {
            _ = this.zoomView.Reset(animated: false);
        }
    }

    private void OnZoomTapped(object sender, ZoomTappedEventArgs e) => this.Tapped?.Invoke(this, e);

    private void OnZoomNavigationRequested(object sender, ZoomNavigationEventArgs e) => this.NavigationRequested?.Invoke(this, e);
}
