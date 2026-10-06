using System.IO.Compression;

namespace ShortBox.Services;

internal sealed class ZipReader : IZipReader
{
    public Task<Stream?> GetInfoStreamAsync(string fileName) => WithArchive(fileName, async archive =>
    {
        var infoEntry = archive.Entries.FirstOrDefault(e => string.Equals(e.Name, ArchiveReader.ComicInfoFileName, StringComparison.InvariantCultureIgnoreCase));
        return await this.CopyEntryAsync(infoEntry);
    });

    public Task<int> GetPageCountAsync(string fileName) => WithArchive(fileName, archive =>
        Task.FromResult(archive.Entries.InReadingOrder(e => e.FullName).Count()));

    public Task<Stream?> OpenCoverAsync(string fileName) => OpenPageAsync(fileName, 0);

    public Task<Stream?> OpenPageAsync(string fileName, int pageIndex) => WithArchive(fileName, async archive =>
    {
        var entry = archive.Entries
               .InReadingOrder(e => e.FullName)
               .Skip(pageIndex)
               .First();
        return await CopyEntryAsync(entry);
    });

    private async Task<T> WithArchive<T>(string fileName, Func<ZipArchive, Task<T>> processAsync)
    {
        using var file = File.OpenRead(fileName);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        return await processAsync(archive).ConfigureAwait(false);
    }

    private async Task<Stream?> CopyEntryAsync(ZipArchiveEntry? entry)
    {
        if (entry is not null)
        {
            using var entryStream = entry.Open();
            var memoryStream = new MemoryStream();
            await entryStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
        return default;
    }
}
