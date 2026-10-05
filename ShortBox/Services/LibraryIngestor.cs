using Microsoft.Extensions.Logging;

namespace ShortBox.Services;

public record IngestResult(IReadOnlyList<string> Added, IReadOnlyList<string> Failed, int Remaining);

public interface ILibraryIngestor
{
    /// <summary>Adds every archive in the remote folder that isn't in the library yet.</summary>
    Task<IngestResult> IngestAsync(CancellationToken ct);
}

internal class LibraryIngestor(
    IArchiveLibrary archives,
    IBookCatalog catalog,
    IPullListStore pullList,
    IBookFactory bookFactory,
    IRarReader rarReader,
    IZipReader zipReader,
    ICoverBusiness coverBusiness,
    ILogger<LibraryIngestor> log) : ILibraryIngestor
{
    /// <summary>Keeps a single run inside a function timeout; leftovers are picked up by the next run.</summary>
    public const int MaxFilesPerRun = 25;

    public async Task<IngestResult> IngestAsync(CancellationToken ct)
    {
        var known = await _catalog.GetFileNamesAsync(ct).ConfigureAwait(false);
        var pending = (await _archives.ListArchivesAsync(ct).ConfigureAwait(false))
            .Where(f => IsArchive(f.Name) && !known.Contains(f.Name))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var added = new List<Book>();
        var failed = new List<string>();
        foreach (var file in pending.Take(MaxFilesPerRun))
        {
            try
            {
                added.AddRange(await this.IngestFileAsync(file, ct).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Failed to ingest {file}", file.Name);
                failed.Add(file.Name);
            }
        }

        if (added.Count > 0)
        {
            var linked = await _pullList.LinkBooksAsync(added, ct).ConfigureAwait(false);
            _log.LogInformation("Ingested {count} books, {linked} matched the pull list", added.Count, linked);
        }
        return new(
            added.Select(b => b.FileName).ToArray(),
            failed,
            Math.Max(0, pending.Count - MaxFilesPerRun));
    }

    private async Task<IReadOnlyList<Book>> IngestFileAsync(ArchiveFile file, CancellationToken ct)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "shortbox-ingest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            var localPath = Path.Combine(workDir, SafeFileName(file.Name));
            await _archives.DownloadArchiveAsync(file, localPath, ct).ConfigureAwait(false);

            IArchiveReader reader = IsRar(file.Name) ? _rarReader : _zipReader;
            var book = await _bookFactory.CreateFromInfoAsync(localPath, reader, ct).ConfigureAwait(false);
            book.FileName = file.Name;
            book.PageCount ??= await reader.GetPageCountAsync(localPath).ConfigureAwait(false);

            await this.UploadCoverAsync(file.Name, localPath, reader, ct).ConfigureAwait(false);
            return await _catalog.AddBooksAsync([book], ct).ConfigureAwait(false);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    private async Task UploadCoverAsync(string archiveName, string localPath, IArchiveReader reader, CancellationToken ct)
    {
        using var cover = await reader.OpenCoverAsync(localPath).ConfigureAwait(false);
        if (cover is null)
        {
            _log.LogWarning("{file} has no cover image", archiveName);
            return;
        }
        using var thumbnail = await _coverBusiness.CreateThumbnailAsync(cover, ct).ConfigureAwait(false);
        await _archives.SaveCoverAsync(archiveName, thumbnail, ct).ConfigureAwait(false);
    }

    private static bool IsRar(string name) => name.EndsWith(".cbr", StringComparison.OrdinalIgnoreCase);

    private static bool IsArchive(string name) =>
        IsRar(name) || name.EndsWith(".cbz", StringComparison.OrdinalIgnoreCase);

    private static string SafeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // temp files; the OS cleans up eventually
        }
    }

    private readonly IArchiveLibrary _archives = archives;
    private readonly IBookCatalog _catalog = catalog;
    private readonly IPullListStore _pullList = pullList;
    private readonly IBookFactory _bookFactory = bookFactory;
    private readonly IRarReader _rarReader = rarReader;
    private readonly IZipReader _zipReader = zipReader;
    private readonly ICoverBusiness _coverBusiness = coverBusiness;
    private readonly ILogger<LibraryIngestor> _log = log;
}
