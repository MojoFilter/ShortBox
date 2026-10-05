using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ShortBox.Services;

namespace ShortBox.Metron;

/// <summary>Weekly releases from the Metron comic database (https://metron.cloud).</summary>
internal class MetronReleaseSource(HttpClient client, IOptions<MetronOptions> options) : IReleaseSource
{
    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        // Widened by a day on each side so we don't depend on whether the range filter is inclusive.
        var after = from.AddDays(-1).ToString("yyyy-MM-dd");
        var before = to.AddDays(1).ToString("yyyy-MM-dd");
        string? url = $"issue/?publisher_name={Uri.EscapeDataString(_options.Publisher)}"
                    + $"&store_date_range_after={after}&store_date_range_before={before}";

        var releases = new List<ReleaseInfo>();
        while (url is not null)
        {
            var page = await this.GetPageAsync(url, ct).ConfigureAwait(false);
            releases.AddRange(page.Results.Select(ToRelease).OfType<ReleaseInfo>());
            url = page.Next;
        }
        return releases.Where(r => r.StoreDate >= from && r.StoreDate <= to).ToList();
    }

    private async Task<MetronPage> GetPageAsync(string url, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var response = await _client.GetAsync(url, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxAttempts)
            {
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(30);
                await Task.Delay(wait > MaxWait ? MaxWait : wait, ct).ConfigureAwait(false);
                continue;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MetronPage>(JsonOptions, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Metron returned an empty response");
        }
    }

    private static ReleaseInfo? ToRelease(MetronIssue issue)
    {
        if (!DateOnly.TryParse(issue.StoreDate, out var storeDate))
        {
            return null;
        }
        var series = issue.Series?.Name ?? string.Empty;
        var number = issue.Number ?? string.Empty;
        var title = string.IsNullOrWhiteSpace(issue.Issue) ? $"{series} #{number}" : issue.Issue;
        var cover = Uri.TryCreate(issue.Image, UriKind.Absolute, out var uri) ? uri : null;
        return new(IdOffset + issue.Id, series, number, title, storeDate, cover, issue.Desc ?? string.Empty);
    }

    /// <summary>Keeps Metron ids clear of Comic Vine's (which are used as they are), since both end up as the pull list key.</summary>
    public const int IdOffset = 1_000_000_000;

    private const int MaxAttempts = 3;
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _client = client;
    private readonly MetronOptions _options = options.Value;

    private record MetronPage(string? Next, List<MetronIssue> Results);

    private record MetronIssue(
        int Id,
        MetronSeries? Series,
        string? Number,
        string? Issue,
        string? StoreDate,
        string? Image,
        string? Desc);

    private record MetronSeries(string? Name);
}
