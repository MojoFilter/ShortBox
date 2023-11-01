using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ShortBox.Services;

public record ComicInfo(string Series, string? Number, int? PageCount);

public interface IComicInfoReader
{
    Task<ComicInfo> ReadAsync(Stream infoStream, CancellationToken cancellationToken);
}

internal class ComicInfoReader : IComicInfoReader 
{
    public async Task<ComicInfo> ReadAsync(Stream infoStream, CancellationToken cancellationToken)
    {
        var info = await XDocument.LoadAsync(infoStream, LoadOptions.None, cancellationToken);
        return new(
            SanitizeSeries((string?)info.ValueOfDescendant("Series") ?? string.Empty),
            (string?)info.ValueOfDescendant("Number"),
            (int?)info.ValueOfDescendant("PageCount"));
    }

    private static string SanitizeSeries(string series)
    {
        if (Regex.Match(series, @"(?<title>) \(.+\)") is var match
            && match.Success)
        {
            return match.Groups["title"].Value;
        }
        return series;
    }
}

internal static class FolderBookStoreExtensions
{
    public static XElement? ValueOfDescendant(this XDocument doc, XName name) => doc.Descendants(name).FirstOrDefault();
}
