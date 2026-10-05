using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ShortBox.Services;

namespace ShortBox.ComicVine;

/// <summary>
/// Weekly releases from Comic Vine (https://comicvine.gamespot.com/api/). Issues can be filtered by store date but
/// don't carry a publisher, so the volumes of the week's issues are looked up to keep only the configured publisher.
/// </summary>
internal class ComicVineReleaseSource(HttpClient client, IOptions<ComicVineOptions> options) : IReleaseSource
{
    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var issues = await this.GetIssuesAsync(from, to, ct).ConfigureAwait(false);
        var publishers = await this.GetPublishersAsync(issues.Select(i => i.Volume!.Id).Distinct().ToList(), ct).ConfigureAwait(false);

        return issues
            .Where(i => publishers.TryGetValue(i.Volume!.Id, out var publisher)
                        && publisher.Contains(_options.Publisher, StringComparison.OrdinalIgnoreCase))
            .Select(ToRelease)
            .OfType<ReleaseInfo>()
            .DistinctBy(r => r.ExternalId)
            .OrderBy(r => r.StoreDate)
            .ThenBy(r => r.Series, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<CvIssue>> GetIssuesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        // Sorted by id: many issues share a store date, and offset paging over tied sort keys repeats and skips items.
        var filter = $"store_date:{from:yyyy-MM-dd}|{to:yyyy-MM-dd}";
        var issues = new List<CvIssue>();
        for (var offset = 0; ; offset += PageSize)
        {
            var page = await this.GetPageAsync<CvIssue>(
                "issues/",
                $"filter={filter}&sort=id:asc&limit={PageSize}&offset={offset}"
                + "&field_list=id,name,issue_number,store_date,image,volume,deck",
                ct).ConfigureAwait(false);
            issues.AddRange(page.Results.Where(i => i.Volume is not null));
            if (offset + PageSize >= page.NumberOfTotalResults || page.Results.Count == 0)
            {
                return issues;
            }
        }
    }

    private async Task<Dictionary<int, string>> GetPublishersAsync(IReadOnlyList<int> volumeIds, CancellationToken ct)
    {
        var publishers = new Dictionary<int, string>();
        foreach (var batch in volumeIds.Chunk(PageSize))
        {
            var page = await this.GetPageAsync<CvVolume>(
                "volumes/",
                $"filter=id:{string.Join('|', batch)}&limit={PageSize}&field_list=id,publisher",
                ct).ConfigureAwait(false);
            foreach (var volume in page.Results.Where(v => v.Publisher?.Name is not null))
            {
                publishers[volume.Id] = volume.Publisher!.Name!;
            }
        }
        return publishers;
    }

    private async Task<CvPage<T>> GetPageAsync<T>(string resource, string query, CancellationToken ct)
    {
        if (_requested && _options.RequestDelay > TimeSpan.Zero)
        {
            await Task.Delay(_options.RequestDelay, ct).ConfigureAwait(false);
        }
        _requested = true;

        var url = $"{resource}?api_key={Uri.EscapeDataString(_options.ApiKey)}&format=json&{query}";
        using var response = await _client.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<CvPage<T>>(JsonOptions, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Comic Vine returned an empty response");
        if (page.StatusCode != 1)
        {
            throw new InvalidOperationException($"Comic Vine error {page.StatusCode}: {page.Error}");
        }
        return page;
    }

    private static ReleaseInfo? ToRelease(CvIssue issue)
    {
        if (!DateOnly.TryParse(issue.StoreDate, out var storeDate))
        {
            return null;
        }
        var series = issue.Volume!.Name ?? string.Empty;
        var number = issue.IssueNumber?.Trim() ?? string.Empty;
        var title = string.IsNullOrWhiteSpace(issue.Name) ? $"{series} #{number}" : $"{series} #{number}: {issue.Name}";
        var cover = Uri.TryCreate(issue.Image?.MediumUrl, UriKind.Absolute, out var uri) ? uri : null;
        return new(issue.Id, series, number, title, storeDate, cover, issue.Deck ?? string.Empty);
    }

    private const int PageSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private bool _requested;
    private readonly HttpClient _client = client;
    private readonly ComicVineOptions _options = options.Value;

    private record CvPage<T>(string? Error, int StatusCode, int NumberOfTotalResults, List<T> Results);

    private record CvIssue(int Id, string? Name, string? IssueNumber, string? StoreDate, CvImage? Image, CvVolume? Volume, string? Deck);

    private record CvVolume(int Id, string? Name, CvPublisher? Publisher);

    private record CvPublisher(string? Name);

    private record CvImage(string? MediumUrl);
}
