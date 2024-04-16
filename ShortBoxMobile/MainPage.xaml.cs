using System.Diagnostics;

namespace ShortBoxMobile;

public partial class MainPage : ContentPage
{
    public MainPage(MainPageViewModel vm)
    {
        InitializeComponent();
        this.BindingContext = vm;
    }

    protected override async void OnAppearing() 
    { 
        try
        {
            await (this.BindingContext as MainPageViewModel).RefreshBooksAsync();
        }
        catch (Exception ex)
        {
            Debugger.Break();
        }
    }
}

public sealed partial class MainPageViewModel : ObservableObject 
{
    public MainPageViewModel(IShortBoxApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    private async Task LoadBooksAsync()
    {
        using var client = _clientFactory.CreateClient();
        this.Books = await client.GetAllBooksAsync();
    }

    [RelayCommand]
    private Task OpenBook(Book book) => Shell.Current.GoToAsync($"{nameof(BookPage)}?bookId={book.Id.Value}");

    [RelayCommand]
    public async Task RefreshBooksAsync()
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

    [ObservableProperty]
    private IEnumerable<Book> _books;

    [ObservableProperty]
    private bool _isRefreshing;

    private readonly IShortBoxApiClientFactory _clientFactory;
}
