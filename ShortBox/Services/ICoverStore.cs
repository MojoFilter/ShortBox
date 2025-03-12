
namespace ShortBox.Services;

public interface ICoverStore
{
    Task<Stream> GetCoverAsync(string bookFileName, CancellationToken ct);
}
