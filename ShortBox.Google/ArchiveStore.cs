namespace ShortBox.Google;

internal class ArchiveStore(IDriveBusiness driveBusiness) : IArchiveStore
{
    public Task<Stream> GetArchiveAsync(string archiveName, CancellationToken cancellationToken) =>
        _driveBusiness.DownloadArchiveAsync(archiveName, cancellationToken);

    private readonly IDriveBusiness _driveBusiness = driveBusiness;
}
