namespace ShortBox.Services;

public interface IArchiveStore
{
    Task<Stream> GetArchiveAsync(string archiveName, CancellationToken cancellationToken);
}
