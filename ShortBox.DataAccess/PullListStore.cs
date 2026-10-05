using ShortBox.Services;

namespace ShortBox.DataAccess;

internal class PullListStore(IDbContextFactory<ShortBoxContext> contextFactory) : IPullListStore
{
    public async Task<IReadOnlyList<PullListEntry>> GetWeekAsync(DateOnly weekStart, CancellationToken ct)
    {
        var from = weekStart.ToDateTime(TimeOnly.MinValue);
        var to = from.AddDays(7);
        using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await context.PullList
            .Where(e => e.StoreDate >= from && e.StoreDate < to)
            .OrderBy(e => e.Series)
            .ThenBy(e => e.IssueNumber)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PullListEntry>> GetOutstandingAsync(CancellationToken ct)
    {
        using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await context.PullList
            .Where(e => e.IsWanted && e.BookId == null)
            .OrderBy(e => e.StoreDate)
            .ThenBy(e => e.Series)
            .ThenBy(e => e.IssueNumber)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<int> UpsertAsync(IEnumerable<ReleaseInfo> releases, CancellationToken ct)
    {
        var incoming = releases.DistinctBy(r => r.ExternalId).ToList();
        if (incoming.Count == 0)
        {
            return 0;
        }

        var ids = incoming.Select(r => new PullListEntryId(r.ExternalId)).ToList();
        var from = incoming.Min(r => r.StoreDate).ToDateTime(TimeOnly.MinValue);
        var to = incoming.Max(r => r.StoreDate).AddDays(1).ToDateTime(TimeOnly.MinValue);
        using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var known = await context.PullList
            .Where(e => ids.Contains(e.Id) || (e.StoreDate >= from && e.StoreDate < to))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // An issue may already be known under another source's id (after switching release sources);
        // recognise it by series and number so it keeps its wanted flag instead of being duplicated.
        var byId = known.ToDictionary(e => e.Id.Value);
        var byIssue = known
            .GroupBy(IssueKey)
            .ToDictionary(g => g.Key, g => g.First());

        var inserted = 0;
        foreach (var release in incoming)
        {
            if (byId.TryGetValue(release.ExternalId, out var entry)
                || byIssue.TryGetValue(IssueKey(release), out entry))
            {
                Apply(release, entry);
            }
            else
            {
                var created = new PullListEntry
                {
                    Id = new(release.ExternalId),
                    Title = release.Title,
                    IssueNumber = ParseNumber(release.Number)
                };
                Apply(release, created);
                context.PullList.Add(created);
                byIssue[IssueKey(created)] = created;
                inserted++;
            }
        }
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return inserted;
    }

    public async Task SetWantedAsync(int entryId, bool wanted, CancellationToken ct)
    {
        using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var id = new PullListEntryId(entryId);
        var entry = await context.PullList.FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Pull list entry {entryId} not found");
        entry.IsWanted = wanted;
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> LinkBooksAsync(IEnumerable<Book> books, CancellationToken ct)
    {
        var newBooks = books.ToList();
        using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var outstanding = await context.PullList
            .Where(e => e.IsWanted && e.BookId == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var linked = 0;
        foreach (var entry in outstanding)
        {
            if (newBooks.FirstOrDefault(b => ReleaseMatcher.IsMatch(entry, b)) is { } book)
            {
                entry.BookId = book.Id;
                linked++;
            }
        }
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return linked;
    }

    private static (string Series, string Number) IssueKey(PullListEntry entry) =>
        (ReleaseMatcher.NormalizeSeries(entry.Series), ReleaseMatcher.NormalizeNumber(entry.Number));

    private static (string Series, string Number) IssueKey(ReleaseInfo release) =>
        (ReleaseMatcher.NormalizeSeries(release.Series), ReleaseMatcher.NormalizeNumber(release.Number));

    private static void Apply(ReleaseInfo release, PullListEntry entry)
    {
        entry.Title = release.Title;
        entry.Series = release.Series;
        entry.Number = release.Number;
        entry.IssueNumber = ParseNumber(release.Number);
        entry.Description = release.Description;
        entry.ThumbnailUri = release.CoverUri;
        entry.StoreDate = release.StoreDate.ToDateTime(TimeOnly.MinValue);
    }

    private static double ParseNumber(string number) =>
        double.TryParse(number, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private readonly IDbContextFactory<ShortBoxContext> _contextFactory = contextFactory;
}
