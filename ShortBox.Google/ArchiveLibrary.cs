namespace ShortBox.Google;

internal class ArchiveLibrary(IDriveBusiness driveBusiness) : IArchiveLibrary
{
    public Task<IReadOnlyList<ArchiveFile>> ListArchivesAsync(CancellationToken ct) =>
        _driveBusiness.ListArchivesAsync(ct);

    public Task DownloadArchiveAsync(ArchiveFile file, string destinationPath, CancellationToken ct) =>
        _driveBusiness.DownloadArchiveToFileAsync(file.Id, destinationPath, ct);

    public Task SaveCoverAsync(string archiveName, Stream jpeg, CancellationToken ct) =>
        _driveBusiness.SaveCoverAsync(archiveName, jpeg, ct);

    private readonly IDriveBusiness _driveBusiness = driveBusiness;
}
