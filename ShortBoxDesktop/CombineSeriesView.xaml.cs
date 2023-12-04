using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using ShortBox.Communication;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Controls;

namespace ShortBoxDesktop;

/// <summary>
/// Interaction logic for CombineSeriesView.xaml
/// </summary>
public partial class CombineSeriesView : UserControl
{
    public CombineSeriesView(ICombineSeriesViewModel vm)
    {
        this.ViewModel = vm;
        InitializeComponent();
        _ = vm.InitializeAsync();
    }

    private ICombineSeriesViewModel ViewModel
    {
        get => (this.DataContext as ICombineSeriesViewModel)!;
        set => this.DataContext = value;
    }

    private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        this.ViewModel.CombinedName = sender switch
        {
            ListBox listBox when listBox.SelectedItem is string val => val,
            _ => string.Empty
        };
    }

    private void SeriesOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        this.ViewModel.SelectedSeries.AddRange(e.AddedItems?.OfType<string>() ?? Enumerable.Empty<string>());
        this.ViewModel.SelectedSeries.RemoveMany(e.RemovedItems?.OfType<string>() ?? Enumerable.Empty<string>());
    }
}

public interface ICombineSeriesViewModel 
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    string CombinedName { get; set; }
    IList<string> SelectedSeries { get; }

}

internal sealed partial class CombineSeriesViewModel : ObservableObject, ICombineSeriesViewModel
{
    public CombineSeriesViewModel(IShortBoxApiClient client)
    {
        var whenSomeSelected =
            _selectedSeries.ObserveCollectionChanges()
            .Select(_ => _selectedSeries.Count() > 0);

        var whenHasACombinedName =
            this.WhenValueChanged(p => p.CombinedName)
                .Select(name => !string.IsNullOrWhiteSpace(name));

        Observable.CombineLatest(
            whenSomeSelected,
            whenHasACombinedName,
            (someSelected, hasACombinedName) => someSelected && hasACombinedName)
            .Subscribe(_canSetCombinedName);
        
        _client = client;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _seriesOptions.Clear();
        var allSeries = await _client.GetAllSeriesAsync();
        var options = allSeries.Select(s => s.Name);
        _seriesOptions.AddRange(options);
    }

    [RelayCommand(CanExecute=(nameof(CanSetCombinedName)))]
    public async Task SetCombinedNameAsync(CancellationToken cancellationToken)
    {
        var seriesToCombine = this.SelectedSeries.ToArray();
        await _client.CombineSeriesNamesAsync(seriesToCombine, this.CombinedName, cancellationToken);
    }

    public bool CanSetCombinedName => _canSetCombinedName.Value;

    public IEnumerable<string> SeriesOptions => _seriesOptions;

    public IList<string> SelectedSeries => _selectedSeries;

    [ObservableProperty]
    private string _combinedName = "";

    private readonly ObservableCollection<string> _seriesOptions = new();
    private readonly ObservableCollection<string> _selectedSeries = new();
    private readonly IShortBoxApiClient _client;
    private readonly BehaviorSubject<bool> _canSetCombinedName = new BehaviorSubject<bool>(false);
}

internal static class SelectableOption
{
    public static SelectableOption<TOpt> Create<TOpt>(TOpt value) => new SelectableOption<TOpt>(value?.ToString() ?? string.Empty, value);
}

internal partial class SelectableOption<T> : ObservableObject
{
    public SelectableOption(string label, T value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    public T Value { get; }

    [ObservableProperty]
    private bool _isSelected;

}