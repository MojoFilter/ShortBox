using MauiPageFullScreen;
using System.Diagnostics;

namespace ShortBoxMobile;

public partial class BookPage : ContentPage
{
	public BookPage(BookPageViewModel vm)
	{
		InitializeComponent();
		this.BindingContext = vm;
	}

    public bool IsZoomed => this.comicPageContainer?.Scale > 1.0 == true;

    protected override void OnAppearing()
    {
		Controls.FullScreen();
    }

	protected override void OnDisappearing()
	{
		Controls.RestoreScreen();
	}

    private void OnPageTapped(object sender, TappedEventArgs e)
    {
		var command = GetTapArea(e.GetPosition(this.inputView), this.inputView) switch
		{
			TapArea.Left => this.ViewModel.PreviousPageCommand,
			TapArea.Right => this.ViewModel.NextPageCommand,
			_ => new AsyncRelayCommand(this.ToggleNavBar)
		};
		command?.Execute(default);
    }

	private async Task ToggleNavBar()
	{
		Shell.SetNavBarIsVisible(this, !Shell.GetNavBarIsVisible(this));
		//if (Shell.GetNavBarIsVisible(this))
		//{
		//	Controls.RestoreScreen();
		//}
		//else
		//{
		//	Controls.FullScreen();
		//}
	}

	private BookPageViewModel ViewModel => this.BindingContext as BookPageViewModel;

    private void OnLeftAreaTapped(object sender, TappedEventArgs e)
    {
		this.ViewModel.PreviousPageCommand.Execute(default);
    }

    private void OnRightAreaTapped(object sender, TappedEventArgs e)
    {
        this.ViewModel.NextPageCommand.Execute(default);
    }

    private void PinchGestureRecognizer_PinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
    {
		var newScale = Math.Clamp(1.0, this.comicPageContainer.Scale * e.Scale, 3.0);
		this.comicPageContainer.Scale = newScale;
		this.comicPageContainer.TranslationX = e.ScaleOrigin.X;
		this.comicPageContainer.TranslationY = e.ScaleOrigin.Y;
		Debug.WriteLine("Scale: {0}", newScale);
    }

	private async Task ToggleZoom()
	{
		var newScale = this.IsZoomed ? 1.0 : 2.0;
		await Task.WhenAll(
		this.comicPageContainer.ScaleTo(newScale, easing: Easing.CubicInOut),
			this.comicPageContainer.TranslateTo(0, 0, easing: Easing.SinInOut));
		this.OnPropertyChanged(nameof(IsZoomed));
	}

	private const double SideAreaPercent = 1.0 / 6.0;

	private TapArea GetTapArea(Point? point, View container) => point switch
	{
		Point p when (p.X < container.Width * SideAreaPercent) => TapArea.Left,
		Point p when (p.X > container.Width - (container.Width * SideAreaPercent)) => TapArea.Right,
		_ => TapArea.Main
	};

	private enum TapArea
	{
		Left, 
		Right,
		Main,
	}

    private async void OnPageDoubleTapped(object sender, TappedEventArgs e)
    {
		await this.ToggleZoom();
    }


    private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
		if (this.IsZoomed) {
			switch (e.StatusType) {
				case GestureStatus.Started:
					_panStart = new(this.comicPageContainer.TranslationX, this.comicPageContainer.TranslationY);
					break;
				case GestureStatus.Running:
					if (_panStart is Point start)
					{
						this.comicPageContainer.TranslationX = start.X + (e.TotalX * this.comicPageContainer.Scale);
						this.comicPageContainer.TranslationY = start.Y + (e.TotalY * this.comicPageContainer.Scale);
					}
					break;
				default:
					_panStart = default;
					break;
			}
		}
    }

	private Point? _panStart;
}

//[QueryProperty(nameof(PageNumber), "page")]
[QueryProperty(nameof(BookId), "bookId")]
public sealed partial class BookPageViewModel : ObservableObject
{

    public BookPageViewModel(IShortBoxApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
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
	private Task NextPage() => this.ChangePageAsync(1);

	[RelayCommand]
	private Task PreviousPage() => this.ChangePageAsync(-1);

	private async Task ChangePageAsync(int delta) 
	{ 
		var newPage = this.PageNumber + delta;
		var pageLimit = (this.Book?.PageCount ?? 0) - 1;
		switch (newPage)
		{
            case < 0 or _ when newPage > pageLimit:
				await Shell.Current.GoToAsync("..");
                break;
			default:
				this.PageNumber = newPage;
				break;
        };
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
				this.SetTitle();
				await this.MarkPage();
				break;
			default:
				break;
		}
    }

    private void SetTitle()
    {
		this.Title = $"{this.Book?.Series} #{this.Book?.Number} pg. {this.PageNumber}/{this.Book?.PageCount}";
    }

	private async Task MarkPage()
	{
		if (this.Book is not null && this.Book.CurrentPage != this.PageNumber)
		{
			using var client = _clientFactory.CreateClient();
			await client.MarkPageAsync(this.BookId, this.PageNumber, default);
			this.Book.CurrentPage = this.PageNumber;
		}
	}

    private async void LoadBook()
	{
		try
		{
			using var client = _clientFactory.CreateClient();
			this.Book = await client.GetBookAsync(this.BookId);
			this.PageNumber = this.Book.CurrentPage;
		} catch (Exception ex) { }
	}

	private readonly IShortBoxApiClientFactory _clientFactory;

}