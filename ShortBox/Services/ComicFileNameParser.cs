using System.Text.RegularExpressions;

namespace ShortBox.Services;

public interface IComicFileNameParser
{
    ComicFileNameParserResult Parse(string fileName);
}

public record ComicFileNameParserResult(string Series, string? Number)
{
    public static ComicFileNameParserResult Empty { get; } = new(string.Empty, default);
}

internal class ComicFileNameParser : IComicFileNameParser
{
    public ComicFileNameParserResult Parse(string fileName)
    {
        if (Regex.Match(fileName, @"(?<series>[^(\.)]+[^0-9]) (?<number>[0-9]*)") is Match match)
        {
            return new(match.Groups["series"].Value, match.Groups["number"]?.Value);
        }
        return ComicFileNameParserResult.Empty;
    }
}
