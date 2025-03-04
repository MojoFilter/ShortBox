namespace ShortBoxAzureClientTesting;

public static class StreamExtensions 
{
    public static async Task<string> ToBase64Async(this Stream stream)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
        return Convert.ToBase64String(memoryStream.ToArray());
    }
}
