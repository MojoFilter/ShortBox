namespace ShortBoxMobile
{
    public partial class AppShell : Shell
    {
        private readonly IShortBoxApiClient _client;

        public AppShell(IShortBoxApiClient shortBoxApiClient)
        {
            _client = shortBoxApiClient;
            InitializeComponent();
            this.Loaded += AppShell_Loaded;
            Routing.RegisterRoute(nameof(BookPage), typeof(BookPage));
        }

        private async void AppShell_Loaded(object sender, EventArgs e)
        {
            var allSeries = await _client.GetAllSeriesAsync();
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
    }
}
