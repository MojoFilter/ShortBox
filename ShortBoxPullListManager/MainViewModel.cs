using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ShortBox.Api.Data;
using ShortBox.Communication;

namespace ShortBoxPullListManager;

public partial class MainViewModel(IShortBoxApiClient client, ILogger<MainViewModel> logger) : ObservableObject
{
    [RelayCommand]
    private async Task UpdateAndRefreshPullListAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.UpdatePullListAsync(cancellationToken).ConfigureAwait(false);
            this.PullList = await _client.GetPullListAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update pull list");
        }
    }

    [ObservableProperty]
    private IEnumerable<PullListEntry> _pullList = Array.Empty<PullListEntry>();

    private readonly IShortBoxApiClient _client = client;
    private readonly ILogger<MainViewModel> _logger = logger;
}
