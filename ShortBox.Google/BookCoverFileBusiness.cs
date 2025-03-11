namespace ShortBox.Google;

internal class BookCoverFileBusiness(
    IBookRepo repo,
    IDriveBusiness driveBusiness) 
    : IBookCoverFileBusiness
{
    public async Task<Stream> GetCoverStreamAsync(BookId bookId, CancellationToken ct)
    {
        var fileName = await _repo.GetFileNameAsync(bookId, ct);
        return await _driveBusiness.DownloadCoverAsync(fileName, ct);
    }

    private readonly IBookRepo _repo = repo;
    private readonly IDriveBusiness _driveBusiness = driveBusiness;
}
