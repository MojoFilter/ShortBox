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
            await (this.BindingContext as MainPageViewModel).LoadBooksAsync();
        }
        catch (Exception ex)
        {
            Debugger.Break();
        }
    }
}

public sealed partial class MainPageViewModel : ObservableObject 
{
    public MainPageViewModel(IShortBoxApiClient client)
    {
        _client = client;
    }

    public async Task LoadBooksAsync()
    {
        this.Books = await _client.GetAllBooksAsync();
    }

    [ObservableProperty]
    private IEnumerable<Book> _books;

    private readonly IShortBoxApiClient _client;
}
