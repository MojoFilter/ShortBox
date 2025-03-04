namespace ShortBox.Google;

public class BookCoverFileBusiness(
    IOptions<GoogleOptions> options,
    IBookRepo repo) : IBookCoverFileBusiness
{
    public async Task<Stream> GetCoverStreamAsync(BookId bookId, CancellationToken ct)
    {
        var fileName = await _repo.GetFileNameAsync(bookId, ct);
        var credentials = GoogleCredential.FromJson(_opt.CredentialsJson)
            .CreateScoped(DriveService.Scope.Drive)
            .CreateWithUser(_opt.CredentialsUser);

        var service = new DriveService(new()
        {
            HttpClientInitializer = credentials,
            ApplicationName = "ShortBox"
        });

        try
        {
            var fileId = await FindFileIdAsync(service, $"{fileName}.jpg", ct).ConfigureAwait(false);
            return await DownloadFileAsync(service, fileId, ct).ConfigureAwait(false);
        } 
        catch (Exception)
        {
            throw;
        }
    }

    private async Task<string> FindFileIdAsync(DriveService service, string fileName, CancellationToken ct)
    {
        var request = service.Files.List();
        request.Q = $"'{_opt.CoversFolderId}' in parents and name = '{fileName}'";
        request.Fields = "files(id)";
        var response = await request.ExecuteAsync(ct);
        return response.Files.FirstOrDefault()?.Id ?? throw new FileNotFoundException($"{fileName} not found");
    }

    private async Task<Stream> DownloadFileAsync(DriveService service, string fileId, CancellationToken ct)
    {
        var request = service.Files.Get(fileId);
        //var resp = await request.ExecuteAsync(ct).ConfigureAwait(false);
        //var stream = await request.ExecuteAsStreamAsync(ct).ConfigureAwait(false);
        //using var file = File.OpenWrite("cover.jpg");
        //await stream.CopyToAsync(file, ct).ConfigureAwait(false);
        var stream = new MemoryStream();
        await request.DownloadAsync(stream, ct).ConfigureAwait(false);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private readonly IBookRepo _repo = repo;
    private readonly GoogleOptions _opt = options.Value;
}
