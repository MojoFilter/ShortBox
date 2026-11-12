namespace ShortBox.Services;

public interface IPullListStore
{
    /// <summary>Entries whose store date falls within the Monday-based week starting at <paramref name="weekStart"/>.</summary>
    Task<IReadOnlyList<PullListEntry>> GetWeekAsync(DateOnly weekStart, CancellationToken ct);

    /// <summary>Wanted entries that haven't shown up in the library yet.</summary>
    Task<IReadOnlyList<PullListEntry>> GetOutstandingAsync(CancellationToken ct);

    /// <summary>Inserts releases that aren't known yet and refreshes metadata of the ones that are. Returns the number inserted.</summary>
    Task<int> UpsertAsync(IEnumerable<ReleaseInfo> releases, CancellationToken ct);

    Task SetWantedAsync(int entryId, bool wanted, CancellationToken ct);

    /// <summary>
    /// Scans the whole library and links every entry that isn't linked yet (wanted or not) to the book it is.
    /// Covers issues that were already in the library by the time they showed up on the checklist as well as
    /// files that arrive later. Returns the number of entries linked.
    /// </summary>
    Task<int> LinkLibraryAsync(CancellationToken ct);
}
