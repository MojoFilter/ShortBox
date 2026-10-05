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
        if (entries.Count > 0 && !refresh)
        {
            return entries;
        }

        var releases = await _source.GetReleasesAsync(weekStart, weekStart.AddDays(6), ct).ConfigureAwait(false);
        await _store.UpsertAsync(releases, ct).ConfigureAwait(false);
        return await _store.GetWeekAsync(weekStart, ct).ConfigureAwait(false);
    }

    public static DateOnly WeekStart(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private readonly IReleaseSource _source = source;
    private readonly IPullListStore _store = store;
}
