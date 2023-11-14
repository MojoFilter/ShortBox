namespace ShortBox;

public interface IImageBusiness
{
    Task<Stream> LoadImageAsync(Stream sourceStream, int? height, CancellationToken cancellationToken);
}

internal sealed class ImageBusiness : IImageBusiness
{
    public async Task<Stream> LoadImageAsync(Stream sourceStream, int? height, CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(sourceStream, cancellationToken);
        if (height.HasValue)
        {
            image.Mutate(ctx =>
                ctx.Resize(new ResizeOptions()
                {
                    Mode = ResizeMode.Max,
                    Size = new(int.MaxValue, height.Value),
                }));
        }
        var stream = new MemoryStream();
        await image.SaveAsJpegAsync(stream);
        stream.Position = 0;
        return stream;
    }
}
