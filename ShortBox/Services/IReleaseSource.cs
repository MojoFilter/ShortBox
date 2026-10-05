namespace ShortBox.Services;

/// <param name="ExternalId">Becomes the pull list key, so it must be unique across release sources.</param>
public record ReleaseInfo(
    int ExternalId,
    string Series,
    string Number,
    string Title,
    DateOnly StoreDate,
    Uri? CoverUri = null,
    string Description = "");

public interface IReleaseSource
{
    /// <summary>Releases with a store date in [from, to] inclusive.</summary>
    Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(DateOnly from, DateOnly to, CancellationToken ct);
}
