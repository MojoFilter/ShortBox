using ShortBox.Services;

namespace ShortBox.Acquisition.Test;

internal class FakeReleaseSource(params ReleaseInfo[] releases) : IReleaseSource
{
    public List<(DateOnly From, DateOnly To)> Calls { get; } = [];

    public Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        Calls.Add((from, to));
        return Task.FromResult<IReadOnlyList<ReleaseInfo>>(releases);
    }
}

internal class FakePullListStore : IPullListStore
{
    public List<PullListEntry> Entries { get; } = [];
    public List<Book> LinkedBooks { get; } = [];

    public Task<IReadOnlyList<PullListEntry>> GetWeekAsync(DateOnly weekStart, CancellationToken ct)
    {
        var from = weekStart.ToDateTime(TimeOnly.MinValue);
        return Task.FromResult<IReadOnlyList<PullListEntry>>(
            Entries.Where(e => e.StoreDate >= from && e.StoreDate < from.AddDays(7)).ToList());
    }

    public Task<IReadOnlyList<PullListEntry>> GetOutstandingAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PullListEntry>>(Entries.Where(e => e.IsWanted && e.BookId is null).ToList());

    public Task<int> UpsertAsync(IEnumerable<ReleaseInfo> releases, CancellationToken ct)
    {
        foreach (var r in releases.Where(r => Entries.All(e => e.Id.Value != r.ExternalId)))
        {
            Entries.Add(new()
            {
                Id = new(r.ExternalId),
                Title = r.Title,
                IssueNumber = 0,
                Series = r.Series,
                Number = r.Number,
                StoreDate = r.StoreDate.ToDateTime(TimeOnly.MinValue)
            });
        }
        return Task.FromResult(releases.Count());
    }

    public Task SetWantedAsync(int entryId, bool wanted, CancellationToken ct) => Task.CompletedTask;

    public Task<int> LinkBooksAsync(IEnumerable<Book> books, CancellationToken ct)
    {
        LinkedBooks.AddRange(books);
        return Task.FromResult(0);
    }
}

internal class FakeBookCatalog(params string[] known) : IBookCatalog
{
    public List<Book> Added { get; } = [];

    public Task<IReadOnlySet<string>> GetFileNamesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(known));

    public Task<IReadOnlyList<Book>> AddBooksAsync(IEnumerable<Book> books, CancellationToken ct)
    {
        var list = books.ToList();
        Added.AddRange(list);
        return Task.FromResult<IReadOnlyList<Book>>(list);
    }
}

/// <summary>"Downloads" archives by writing the bytes registered under their name.</summary>
internal class FakeArchiveLibrary : IArchiveLibrary
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public List<string> Covers { get; } = [];
    public List<string> Downloaded { get; } = [];

    public Task<IReadOnlyList<ArchiveFile>> ListArchivesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ArchiveFile>>(Files.Keys.Select(n => new ArchiveFile(n, n)).ToList());

    public async Task DownloadArchiveAsync(ArchiveFile file, string destinationPath, CancellationToken ct)
    {
        Downloaded.Add(file.Name);
        await File.WriteAllBytesAsync(destinationPath, Files[file.Id], ct);
    }

    public Task SaveCoverAsync(string archiveName, Stream jpeg, CancellationToken ct)
    {
        Covers.Add(archiveName);
        return Task.CompletedTask;
    }
}
