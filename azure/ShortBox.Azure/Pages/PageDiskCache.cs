namespace ShortBox.Azure;

/// <summary>
/// Pages on disk as <c>{root}/{bookId}/{index:D4}.img</c>. The extension is fixed because image decoders sniff the bytes.
/// Pages are written to a temporary file and moved into place, so a partial download is never served.
/// </summary>
public sealed class PageDiskCache(string root, long maxBytes)
{
    public string PathFor(int bookId, int pageIndex) => Path.Combine(root, bookId.ToString(), $"{pageIndex:D4}{PageExtension}");

    public bool TryGet(int bookId, int pageIndex, out string path)
    {
        path = this.PathFor(bookId, pageIndex);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            // Last write time doubles as last use, which is what trimming goes by.
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return true;
    }

    public async Task<string> StoreAsync(int bookId, int pageIndex, Stream content, CancellationToken cancellationToken)
    {
        var path = this.PathFor(bookId, pageIndex);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Guid.NewGuid():N}{TempExtension}";
        try
        {
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            TryDelete(temp);
        }

        if (Interlocked.Increment(ref _writes) % TrimEvery == 1)
        {
            this.Trim();
        }

        return path;
    }

    /// <summary>Deletes the least recently used pages until the cache fits, plus any abandoned temporary files.</summary>
    public void Trim()
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        try
        {
            var staleTemp = DateTime.UtcNow - TimeSpan.FromHours(1);
            var pages = new List<FileInfo>();
            foreach (var file in new DirectoryInfo(root).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (file.Extension == TempExtension)
                {
                    if (file.LastWriteTimeUtc < staleTemp)
                    {
                        TryDelete(file.FullName);
                    }
                }
                else if (file.Extension == PageExtension)
                {
                    pages.Add(file);
                }
            }

            var total = pages.Sum(f => f.Length);
            foreach (var file in pages.OrderBy(f => f.LastWriteTimeUtc))
            {
                if (total <= maxBytes)
                {
                    break;
                }

                total -= file.Length;
                TryDelete(file.FullName);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private const string PageExtension = ".img";
    private const string TempExtension = ".tmp";
    private const int TrimEvery = 20;
    private int _writes;
}
