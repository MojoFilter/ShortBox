using CommunityToolkit.Mvvm.ComponentModel;
using ShortBox.Api.Data;
using ShortBox.Communication;
using System.ComponentModel;

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

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(this.Series))
        {
            this.LoadBooks();
        }
        base.OnPropertyChanged(e);
    }

    [ObservableProperty]
    private Series _series;

    [ObservableProperty]
    private IEnumerable<Book> _books;

    private async void LoadBooks()
    {
        if (this.Series is not null)
        {
            this.Books = await _client.GetIssuesAsync(this.Series.Name);
        }
    }

    private readonly IShortBoxApiClient _client;
}
