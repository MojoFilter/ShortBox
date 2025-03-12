using SixLabors.ImageSharp.PixelFormats;

namespace ShortBox.Services;

public interface ICoverBusiness
{
    Task<Stream> CreateThumbnailAsync(Stream coverStream, CancellationToken ct);
}

internal class CoverBusiness : ICoverBusiness
{
    public async Task<Stream> CreateThumbnailAsync(Stream coverStream, CancellationToken ct)
    {
        using var image = Image.Load<Rgba32>(coverStream);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(0, 250),
            Mode = ResizeMode.Max
        }));
        var thumbnailStream = new MemoryStream();
        await image.SaveAsJpegAsync(thumbnailStream, ct);
        thumbnailStream.Seek(0, SeekOrigin.Begin);
        return thumbnailStream;
    }
}
