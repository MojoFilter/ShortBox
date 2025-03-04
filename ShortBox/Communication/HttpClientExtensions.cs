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
        catch (Exception)
        {
            //Debug.WriteLine(ex.Message);
            return getDefault();
        }

    }
}
