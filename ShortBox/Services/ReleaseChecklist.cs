namespace ShortBox.Services;

public interface IReleaseChecklist
{
    /// <summary>The releases of the Monday-based week containing <paramref name="date"/>, fetched from the release source when not known yet (or when <paramref name="refresh"/> is set).</summary>
    Task<IReadOnlyList<PullListEntry>> GetWeekAsync(DateOnly date, bool refresh, CancellationToken ct);
}

internal class ReleaseChecklist(IReleaseSource source, IPullListStore store) : IReleaseChecklist
{
    public async Task<IReadOnlyList<PullListEntry>> GetWeekAsync(DateOnly date, bool refresh, CancellationToken ct)
    {
        var weekStart = WeekStart(date);
        var entries = await _store.GetWeekAsync(weekStart, ct).ConfigureAwait(false);
        if (entries.Count == 0 || refresh)
        {
            var releases = await _source.GetReleasesAsync(weekStart, weekStart.AddDays(6), ct).ConfigureAwait(false);
            await _store.UpsertAsync(releases, ct).ConfigureAwait(false);
            entries = await _store.GetWeekAsync(weekStart, ct).ConfigureAwait(false);
        }

        // Release data tends to show up later than the files do, so an entry may be new to the checklist
        // while its book has been in the library for days.
        if (entries.Any(e => e.BookId is null) && await _store.LinkLibraryAsync(ct).ConfigureAwait(false) > 0)
        {
            entries = await _store.GetWeekAsync(weekStart, ct).ConfigureAwait(false);
        }
        return entries;
    }

    public static DateOnly WeekStart(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private readonly IReleaseSource _source = source;
    private readonly IPullListStore _store = store;
}
