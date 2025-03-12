namespace ShortBox.Google;

internal class CoverStore(IDriveBusiness driveBusiness) : ICoverStore
{
    public Task<Stream> GetCoverAsync(string bookFileName, CancellationToken ct) =>
        _driveBusiness.DownloadCoverAsync(bookFileName, ct);

    private readonly IDriveBusiness _driveBusiness = driveBusiness;
}
