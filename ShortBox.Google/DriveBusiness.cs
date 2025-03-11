namespace ShortBox.Google;

internal interface IDriveBusiness
{
    Task<Stream> DownloadArchiveAsync(string fileName, CancellationToken ct);
    Task<Stream> DownloadCoverAsync(string fileName, CancellationToken ct);
}

internal class DriveBusiness(
    IOptions<GoogleOptions> options,
    IDriveServiceFactory driveServiceFactory) : IDriveBusiness
{
    public Task<Stream> DownloadCoverAsync(string fileName, CancellationToken ct) =>
        this.DownloadFileAsync($"{fileName}.jpg", _opt.CoversFolderId, ct);

    public Task<Stream> DownloadArchiveAsync(string fileName, CancellationToken ct) =>
        this.DownloadFileAsync(fileName, _opt.ArchivesFolderId, ct);

    private async Task<Stream> DownloadFileAsync(string fileName, string folderId, CancellationToken ct)
    {
        var service = _driveServiceFactory.GetDriveService();
        var fileId = await FindFileIdAsync(service, fileName, folderId, ct).ConfigureAwait(false);
        return await DownloadFileAsync(service, fileId, ct).ConfigureAwait(false);
    }

    private async Task<string> FindFileIdAsync(DriveService service, string fileName, string folderId, CancellationToken ct)
    {
        var request = service.Files.List();
        request.Q = $"'{folderId}' in parents and name = '{fileName}'";
        request.Fields = "files(id)";
        var response = await request.ExecuteAsync(ct);
        return response.Files.FirstOrDefault()?.Id ?? throw new FileNotFoundException($"{fileName} not found");
    }

    private async Task<Stream> DownloadFileAsync(DriveService service, string fileId, CancellationToken ct)
    {
        var request = service.Files.Get(fileId);
        var stream = new MemoryStream();
        await request.DownloadAsync(stream, ct).ConfigureAwait(false);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private readonly GoogleOptions _opt = options.Value;
    private readonly IDriveServiceFactory _driveServiceFactory = driveServiceFactory;
}
