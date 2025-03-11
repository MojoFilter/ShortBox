namespace ShortBoxAzureClientTesting;

public static class StreamExtensions 
{
    public static async Task<string> ToBase64Async(this Stream stream, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        return Convert.ToBase64String(memoryStream.ToArray());
    }

    public static async Task<string> ReadBase64ImageDataAsync(this Stream stream, CancellationToken cancellationToken)
    {
        var data = await stream.ToBase64Async(cancellationToken).ConfigureAwait(false);
        return $"data:image/jpeg;base64,{data}";
    }
}
