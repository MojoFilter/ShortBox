namespace ShortBoxMobile;

public partial class BookPage : ContentPage
{
	public BookPage(BookPageViewModel vm)
	{
		InitializeComponent();
		this.BindingContext = vm;
	}

    private void PinchGestureRecognizer_PinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
    {
		switch (e.Status)
		{
			case GestureStatus.Started:
				this.startScale = Content.Scale;
				this.comicPageContainer.AnchorX = 0;
				this.comicPageContainer.AnchorY = 0;
				break;
			case GestureStatus.Running:
				(this.comicPageContainer.TranslationX,
					this.comicPageContainer.TranslationY,
					this.comicPageContainer.Scale) =
					this.CalculateScale(e.Scale, e.ScaleOrigin);
				break;
			case GestureStatus.Completed:
				this.xOffset = this.comicPageContainer.TranslationX;
				this.yOffset = this.comicPageContainer.TranslationX;
				break;
		}
    }

	private (double x, double y, double scale) CalculateScale(double scale, Point scaleOrigin)
	{
        // Calculate the scale factor to be applied.
        currentScale += (scale - 1) * startScale;
        currentScale = Math.Max(1, currentScale);

        // The ScaleOrigin is in relative coordinates to the wrapped user interface element,
        // so get the X pixel coordinate.
        double renderedX = Content.X + xOffset;
        double deltaX = renderedX / Width;
        double deltaWidth = Width / (Content.Width * startScale);
        double originX = (scaleOrigin.X - deltaX) * deltaWidth;

        // The ScaleOrigin is in relative coordinates to the wrapped user interface element,
        // so get the Y pixel coordinate.
        double renderedY = Content.Y + yOffset;
        double deltaY = renderedY / Height;
        double deltaHeight = Height / (Content.Height * startScale);
        double originY = (scaleOrigin.Y - deltaY) * deltaHeight;

        // Calculate the transformed element pixel coordinates.
        double targetX = xOffset - (originX * Content.Width) * (currentScale - startScale);
        double targetY = yOffset - (originY * Content.Height) * (currentScale - startScale);

		return (
			Math.Clamp(targetX, -Content.Width * (currentScale - 1), 0),
			Math.Clamp(targetY, -Content.Height * (currentScale - 1), 0),
			currentScale);
    }

    double currentScale = 1;
    double startScale = 1;
    double xOffset = 0;
    double yOffset = 0;
}

//[QueryProperty(nameof(PageNumber), "page")]
[QueryProperty(nameof(BookId), "bookId")]
public sealed partial class BookPageViewModel : ObservableObject
{

    public BookPageViewModel(IShortBoxApiClient client)
    {
        _client = client;
    }

    [ObservableProperty]
	private int _pageNumber;

	[ObservableProperty]
	private int _bookId;

	[ObservableProperty]
	private Book _book;

	[ObservableProperty]
	private string _title;

	[RelayCommand]
	private void NextPage() => this.ChangePage(1);

	[RelayCommand]
	private void PreviousPage() => this.ChangePage(-1);

	private void ChangePage(int delta) 
	{ 
		this.PageNumber = int.Max(0, int.Min(this.PageNumber + delta, this.Book.PageCount ?? 0 - 1));
	}

	

    protected override async void OnPropertyChanged(PropertyChangedEventArgs e)
    {
		base.OnPropertyChanged(e);
        switch (e.PropertyName) {
			case nameof(BookId):
				this.LoadBook();
				break;
			case nameof(Book):
				this.SetTitle();
				break;
			case nameof(PageNumber):
				await this.MarkPage();
				break;
			default:
				break;
		}
    }

    private void SetTitle()
    {
		this.Title = $"{this.Book?.Series} #{this.Book?.Number}";
	}

	private async Task MarkPage()
	{
		if (this.Book is not null && this.Book.CurrentPage != this.PageNumber)
		{
			await _client.MarkPageAsync(this.BookId, this.PageNumber, default);
			this.Book.CurrentPage = this.PageNumber;
		}
	}

    private async void LoadBook()
	{
		try
		{
			this.Book = await _client.GetBookAsync(this.BookId);
			this.PageNumber = this.Book.CurrentPage;
		} catch (Exception ex) { }
	}

	private readonly IShortBoxApiClient _client;

}