namespace ShortBox;

public static class ShortBoxExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this Task<IEnumerable<T>> task) => (await task).ToList();

    public static async Task<byte[]> ToByteArrayAsync(this Task<Stream?> streamTask, CancellationToken cancellationToken)
    {
        var stream = await streamTask ?? throw new ArgumentNullException("stream");
        using var reader = new BinaryReader(stream);
        return reader.ReadBytes((int)stream.Length);
    }
}
