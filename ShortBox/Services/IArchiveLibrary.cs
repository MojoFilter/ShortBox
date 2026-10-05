namespace ShortBox.Services;

public record ArchiveFile(string Id, string Name);

/// <summary>The remote folder (Google Drive) the comic archives are dropped into.</summary>
public interface IArchiveLibrary
{
    Task<IReadOnlyList<ArchiveFile>> ListArchivesAsync(CancellationToken ct);
    Task DownloadArchiveAsync(ArchiveFile file, string destinationPath, CancellationToken ct);
    Task SaveCoverAsync(string archiveName, Stream jpeg, CancellationToken ct);
}
