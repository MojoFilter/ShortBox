namespace ShortBoxMobile;

public partial class AppShell : Shell
{

    public AppShell(IShortBoxApiClientFactory shortBoxApiClientFactory)
    {
        _clientFactory = shortBoxApiClientFactory;
        InitializeComponent();
        this.Loaded += AppShell_Loaded;
        Routing.RegisterRoute(nameof(BookPage), typeof(BookPage));
    }

    private async void AppShell_Loaded(object sender, EventArgs e)
    {
        await this.RefreshSeriesAsync();
    }

    private async Task RefreshSeriesAsync()
    {
        using var client = _clientFactory.CreateClient();
        var allSeries = await client.GetAllSeriesAsync();
        var seriesStyle = this.Resources["seriesStyle"] as Style;
        foreach (var series in allSeries)
        {
            var item = new ShellContent()
            {
                BindingContext = series,
                Style = seriesStyle,
                Route = series.Name
            };
            this.seriesContainer.Items.Add(item);
        }
    }

    private readonly IShortBoxApiClientFactory _clientFactory;
}


