namespace ShortBoxMobile;

public partial class ReleasesPage : ContentPage
{
    public ReleasesPage(ReleasesPageViewModel vm)
    {
        InitializeComponent();
        this.BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((ReleasesPageViewModel)this.BindingContext).LoadAsync();
    }
}

/// <summary>One line of the checklist. Ticking the box tells the server to wait for that issue.</summary>
public sealed partial class ReleaseRow : ObservableObject
{
    public ReleaseRow(PullListEntry entry, Func<ReleaseRow, Task> persistAsync)
    {
        _entry = entry;
        _persistAsync = persistAsync;
        _isWanted = entry.IsWanted;
    }

    public int Id => _entry.Id.Value;
    public string Series => _entry.Series;
    public string Number => _entry.Number;
    public Uri? CoverUri => _entry.ThumbnailUri;
    public bool CanChange => _entry.BookId is null;

    public string Status =>
        !this.CanChange ? "In library"
        : this.IsWanted ? "Waiting for file"
        : string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status))]
    private bool _isWanted;

    /// <summary>Set by the view model when the server rejected a change, without persisting again.</summary>
    public void Revert(bool wanted)
    {
        _reverting = true;
        this.IsWanted = wanted;
        _reverting = false;
    }

    partial void OnIsWantedChanged(bool value)
    {
        if (!_reverting)
        {
            _ = _persistAsync(this);
        }
    }

    private bool _reverting;
    private readonly PullListEntry _entry;
    private readonly Func<ReleaseRow, Task> _persistAsync;
}

public sealed partial class ReleasesPageViewModel(IShortBoxReleasesClient client) : ObservableObject
{
    public async Task LoadAsync(bool refreshFromSource = false)
    {
        try
        {
            this.IsRefreshing = true;
            var entries = await _client.GetReleasesAsync(_week, refreshFromSource);
            this.Releases = entries.Select(e => new ReleaseRow(e, this.PersistAsync)).ToList();
            this.Message = "No releases found for this week.";
        }
        catch (HttpRequestException ex)
        {
            this.Releases = [];
            this.Message = $"Couldn't load releases: {ex.Message}";
        }
        finally
        {
            this.IsRefreshing = false;
        }
        this.WeekTitle = $"Week of {_week.ToString("MMM d", CultureInfo.CurrentCulture)}";
    }

    [RelayCommand]
    private Task RefreshAsync() => this.LoadAsync(refreshFromSource: true);

    [RelayCommand]
    private Task PreviousWeekAsync() => this.ChangeWeekAsync(-7);

    [RelayCommand]
    private Task NextWeekAsync() => this.ChangeWeekAsync(7);

    [RelayCommand]
    private async Task ScanLibraryAsync()
    {
        try
        {
            var result = await _client.ScanLibraryAsync();
            this.Message = $"Added {result.Added.Count} book(s). {result.Remaining} left for the next scan.";
            await this.LoadAsync();
        }
        catch (HttpRequestException ex)
        {
            this.Message = $"Scan failed: {ex.Message}";
        }
    }

    private Task ChangeWeekAsync(int days)
    {
        _week = _week.AddDays(days);
        return this.LoadAsync();
    }

    private async Task PersistAsync(ReleaseRow row)
    {
        try
        {
            await _client.SetWantedAsync(row.Id, row.IsWanted);
        }
        catch (HttpRequestException)
        {
            row.Revert(!row.IsWanted);
        }
    }

    [ObservableProperty]
    private IReadOnlyList<ReleaseRow> _releases = [];

    [ObservableProperty]
    private string _weekTitle = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    private DateOnly _week = ThisMonday();

    private static DateOnly ThisMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
    }

    private readonly IShortBoxReleasesClient _client = client;
}
