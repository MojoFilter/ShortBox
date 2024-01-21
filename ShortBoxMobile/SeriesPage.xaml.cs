namespace ShortBoxMobile;

public partial class SeriesPage : ContentPage
{
    public SeriesPage()
    {
        InitializeComponent();
        _vm = App.CurrentApp.ViewModelFactory.CreateSeriesPageViewModel();
        this.booksView.BindingContext = _vm;
    }

    protected override void OnBindingContextChanged()
    {
        _vm.Series = this.BindingContext as Series;
    }

    private readonly SeriesPageViewModel _vm;
}

public sealed partial class SeriesPageViewModel : ObservableObject 
{
    public SeriesPageViewModel(IShortBoxApiClient client)
    {
        _client = client;
    }

    protected override async void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(this.Series))
        {
            await this.RefreshBooksAsync();
        }
        base.OnPropertyChanged(e);
    }

    [ObservableProperty]
    private Series _series;

    [ObservableProperty]
    private IEnumerable<BookGroup> _bookGroups;

    [ObservableProperty]
    private bool _isRefreshing;

    [RelayCommand]
    private Task OpenBookAsync(Book book) => book switch
    {
        Book => Shell.Current.GoToAsync($"{nameof(BookPage)}?bookId={book.Id}"),
        _ => Task.CompletedTask
    };

    [RelayCommand]
    private async Task RefreshBooksAsync()
    {
        try
        {
            this.IsRefreshing = true;
            await this.LoadBooksAsync();
        }
        finally
        {
            this.IsRefreshing = false;
        }
    }

    private async Task LoadBooksAsync()
    {
        if (this.Series is not null)
        {
            var unreadTask = _client.GetIssuesAsync(this.Series.Name);
            var readTask = _client.GetSeriesArchiveAsync(this.Series.Name);
            this.BookGroups = new BookGroup[]
            {
                new("New", await unreadTask),
                new("Archive", await readTask)
            }.Where(group => group.Any());
        }
    }

    private readonly IShortBoxApiClient _client;
}

public class BookGroup : List<Book>
{
    public BookGroup(string name, IEnumerable<Book> books) : base(books)
    {
        this.Name = name;
    }

    public string Name { get; }
}
