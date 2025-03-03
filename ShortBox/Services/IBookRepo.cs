
namespace ShortBox.Services;

public interface IBookRepo
{
    Task<string> GetFileNameAsync(BookId bookId, CancellationToken ct);
}
