namespace ShortBoxMobile;

public partial class BookPage : ContentPage
{
	public BookPage(BookPageViewModel vm)
	{
		InitializeComponent();
		this.BindingContext = vm;
	}

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