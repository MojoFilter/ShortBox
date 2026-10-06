namespace ShortBox.Communication;

public static class HttpClientExtensions
{
    public static async Task<IEnumerable<T>> GetSomeAsync<T>(this HttpClient client, string uri, CancellationToken cancellationToken)
    {
        IEnumerable<T> getDefault() => Enumerable.Empty<T>();
        try
        {
            var result = await client.GetFromJsonAsync<IEnumerable<T>>(uri, cancellationToken);
            return result ?? getDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GET {uri} failed: {ex.Message}");
            return getDefault();
        }

    }
}
