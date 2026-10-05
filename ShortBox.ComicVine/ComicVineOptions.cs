namespace ShortBox.ComicVine;

public class ComicVineOptions
{
    public string BaseAddress { get; set; } = "https://comicvine.gamespot.com/api/";
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Only releases whose volume's publisher name contains this text (case-insensitive) are kept.</summary>
    public string Publisher { get; set; } = "Marvel";

    /// <summary>Pause between requests; Comic Vine blocks clients that hammer it.</summary>
    public TimeSpan RequestDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}
