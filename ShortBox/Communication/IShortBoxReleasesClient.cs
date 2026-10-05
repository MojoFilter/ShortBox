using ShortBox.Services;

namespace ShortBox.Communication;

public interface IShortBoxReleasesClient
{
    /// <summary>The releases of the week containing <paramref name="week"/> (this week when null), with the user's wanted flags.</summary>
    Task<IEnumerable<PullListEntry>> GetReleasesAsync(DateOnly? week = null, bool refresh = false, CancellationToken cancellationToken = default);

    /// <summary>Wanted issues that haven't shown up in the library yet.</summary>
    Task<IEnumerable<PullListEntry>> GetWantedAsync(CancellationToken cancellationToken = default);

    Task SetWantedAsync(int entryId, bool wanted, CancellationToken cancellationToken = default);

    /// <summary>Scans the archive folder now instead of waiting for the next scheduled scan.</summary>
    Task<IngestResult> ScanLibraryAsync(CancellationToken cancellationToken = default);
}
