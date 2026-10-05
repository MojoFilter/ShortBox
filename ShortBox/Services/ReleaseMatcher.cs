using System.Globalization;
using System.Text.RegularExpressions;

namespace ShortBox.Services;

/// <summary>Decides whether a library book is the issue a pull list entry is waiting for.</summary>
public static partial class ReleaseMatcher
{
    public static bool IsMatch(PullListEntry entry, Book book) =>
        book.Series is not null
        && book.Number is not null
        && NormalizeSeries(entry.Series) is { Length: > 0 } series
        && series == NormalizeSeries(book.Series)
        && NormalizeNumber(entry.Number) == NormalizeNumber(book.Number);

    public static string NormalizeSeries(string series)
    {
        var withoutYear = TrailingYear().Replace(series, string.Empty);
        var lettersAndDigits = NonAlphanumeric().Replace(withoutYear.ToLowerInvariant(), string.Empty);
        return lettersAndDigits.StartsWith("the", StringComparison.Ordinal) ? lettersAndDigits[3..] : lettersAndDigits;
    }

    public static string NormalizeNumber(string number)
    {
        var trimmed = number.Trim().TrimStart('#');
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value.ToString(CultureInfo.InvariantCulture)
            : trimmed.ToLowerInvariant();
    }

    [GeneratedRegex(@"\s*\((19|20)\d{2}\)\s*$")]
    private static partial Regex TrailingYear();

    [GeneratedRegex(@"[^a-z0-9]")]
    private static partial Regex NonAlphanumeric();
}
