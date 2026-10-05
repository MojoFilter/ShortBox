using Google.Apis.Download;
using Google.Apis.Upload;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace ShortBox.Google;

internal interface IDriveBusiness
{
    Task<Stream> DownloadArchiveAsync(string fileName, CancellationToken ct);
    Task<Stream> DownloadCoverAsync(string fileName, CancellationToken ct);
    Task<IReadOnlyList<ArchiveFile>> ListArchivesAsync(CancellationToken ct);
    Task DownloadArchiveToFileAsync(string fileId, string destinationPath, CancellationToken ct);
    Task SaveCoverAsync(string archiveName, Stream jpeg, CancellationToken ct);
}

internal class DriveBusiness(
    IOptions<GoogleOptions> options,
    IDriveServiceFactory driveServiceFactory) : IDriveBusiness
{
    public async Task<IReadOnlyList<ArchiveFile>> ListArchivesAsync(CancellationToken ct)
    {
        var service = _driveServiceFactory.GetDriveService();
        var files = new List<ArchiveFile>();
        string? pageToken = null;
        do
        {
            var request = service.Files.List();
            request.Q = $"'{_opt.ArchivesFolderId}' in parents and trashed = false and mimeType != 'application/vnd.google-apps.folder'";
            request.Fields = "nextPageToken, files(id, name)";
            request.PageSize = 1000;
            request.PageToken = pageToken;
            var response = await request.ExecuteAsync(ct).ConfigureAwait(false);
            files.AddRange(response.Files.Select(f => new ArchiveFile(f.Id, f.Name)));
            pageToken = response.NextPageToken;
        } while (pageToken is not null);
        return files;
    }

    public async Task DownloadArchiveToFileAsync(string fileId, string destinationPath, CancellationToken ct)
    {
        var service = _driveServiceFactory.GetDriveService();
        using var file = File.Create(destinationPath);
        var progress = await service.Files.Get(fileId).DownloadAsync(file, ct).ConfigureAwait(false);
        if (progress.Status == DownloadStatus.Failed)
        {
            throw new IOException($"Download of {fileId} failed", progress.Exception);
        }
    }

    public async Task SaveCoverAsync(string archiveName, Stream jpeg, CancellationToken ct)
    {
        var service = _driveServiceFactory.GetDriveService();
        var coverName = $"{archiveName}.jpg";
        var existingId = await TryFindFileIdAsync(service, coverName, _opt.CoversFolderId, ct).ConfigureAwait(false);
        IUploadProgress progress;
        if (existingId is null)
        {
            var metadata = new DriveFile { Name = coverName, Parents = [_opt.CoversFolderId] };
            progress = await service.Files.Create(metadata, jpeg, "image/jpeg").UploadAsync(ct).ConfigureAwait(false);
        }
        else
        {
            progress = await service.Files.Update(new DriveFile(), existingId, jpeg, "image/jpeg").UploadAsync(ct).ConfigureAwait(false);
        }
        if (progress.Status == UploadStatus.Failed)
        {
            throw new IOException($"Upload of {coverName} failed", progress.Exception);
        }
    }

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

    private async Task<string> FindFileIdAsync(DriveService service, string fileName, string folderId, CancellationToken ct) =>
        await TryFindFileIdAsync(service, fileName, folderId, ct).ConfigureAwait(false)
            ?? throw new FileNotFoundException($"{fileName} not found");

    private async Task<string?> TryFindFileIdAsync(DriveService service, string fileName, string folderId, CancellationToken ct)
    {
        var request = service.Files.List();
        request.Q = $"'{folderId}' in parents and name = '{this.EscapeFileName(fileName)}' and trashed = false";
        request.Fields = "files(id)";
        var response = await request.ExecuteAsync(ct).ConfigureAwait(false);
        return response.Files.FirstOrDefault()?.Id;
    }

    private async Task<Stream> DownloadFileAsync(DriveService service, string fileId, CancellationToken ct)
    {
        var request = service.Files.Get(fileId);
        var stream = new MemoryStream();
        await request.DownloadAsync(stream, ct).ConfigureAwait(false);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private string EscapeFileName(string fileName) => fileName.Replace("'", "\\'");

    private readonly GoogleOptions _opt = options.Value;
    private readonly IDriveServiceFactory _driveServiceFactory = driveServiceFactory;
}
