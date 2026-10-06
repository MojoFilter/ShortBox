using ShortBox.Azure.Zoom;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class ImageDimensionsTests
{
    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(bytes, 0);
        BigEndian(bytes, 16, width);
        BigEndian(bytes, 20, height);
        return bytes;
    }

    private static byte[] Gif(int width, int height)
    {
        var bytes = new byte[13];
        "GIF89a"u8.CopyTo(bytes);
        bytes[6] = (byte)width;
        bytes[7] = (byte)(width >> 8);
        bytes[8] = (byte)height;
        bytes[9] = (byte)(height >> 8);
        return bytes;
    }

    /// <summary>SOI, an APP0 segment and a COM segment to skip, then a baseline SOF0.</summary>
    private static byte[] Jpeg(int width, int height, byte sofMarker = 0xC0)
    {
        var bytes = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        bytes.AddRange(new byte[14]);
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0x00, 0x06, 1, 2, 3, 4 });
        bytes.AddRange(new byte[] { 0xFF, sofMarker, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 3 });
        bytes.AddRange(new byte[20]);
        return [.. bytes];
    }

    private static byte[] WebPHeader(string chunk)
    {
        var bytes = new byte[40];
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        System.Text.Encoding.ASCII.GetBytes(chunk).CopyTo(bytes, 12);
        return bytes;
    }

    private static void BigEndian(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    private static (int, int)? Read(byte[] bytes) => ImageDimensions.TryRead(new MemoryStream(bytes));

    [TestMethod]
    public void ReadsPng() => Assert.AreEqual((3975, 3056), Read(Png(3975, 3056)));

    [TestMethod]
    public void ReadsGif() => Assert.AreEqual((640, 480), Read(Gif(640, 480)));

    [TestMethod]
    public void ReadsJpegPastEarlierSegments() => Assert.AreEqual((1988, 3056), Read(Jpeg(1988, 3056)));

    [TestMethod]
    public void ReadsProgressiveJpeg() => Assert.AreEqual((1988, 3056), Read(Jpeg(1988, 3056, sofMarker: 0xC2)));

    [TestMethod]
    public void ReadsLossyWebP()
    {
        var bytes = WebPHeader("VP8 ");
        bytes[23] = 0x9D;
        bytes[24] = 0x01;
        bytes[25] = 0x2A;
        bytes[26] = 0xC4;
        bytes[27] = 0x07; // 1988
        bytes[28] = 0xF0;
        bytes[29] = 0x0B; // 3056
        Assert.AreEqual((1988, 3056), Read(bytes));
    }

    [TestMethod]
    public void ReadsLosslessWebP()
    {
        var bytes = WebPHeader("VP8L");
        bytes[20] = 0x2F;
        uint bits = (uint)(1988 - 1) | ((uint)(3056 - 1) << 14);
        bytes[21] = (byte)bits;
        bytes[22] = (byte)(bits >> 8);
        bytes[23] = (byte)(bits >> 16);
        bytes[24] = (byte)(bits >> 24);
        Assert.AreEqual((1988, 3056), Read(bytes));
    }

    [TestMethod]
    public void ReadsExtendedWebP()
    {
        var bytes = WebPHeader("VP8X");
        var width = 1988 - 1;
        var height = 3056 - 1;
        bytes[24] = (byte)width;
        bytes[25] = (byte)(width >> 8);
        bytes[27] = (byte)height;
        bytes[28] = (byte)(height >> 8);
        Assert.AreEqual((1988, 3056), Read(bytes));
    }

    [TestMethod]
    public void AspectRatioIsWidthOverHeight()
    {
        Assert.AreEqual(3975.0 / 3056, ImageDimensions.TryReadAspectRatio(new MemoryStream(Png(3975, 3056))));
    }

    [TestMethod]
    public void UnknownFormatsAndShortInputGiveNull()
    {
        Assert.IsNull(Read([]));
        Assert.IsNull(Read([1, 2, 3]));
        Assert.IsNull(Read("not an image at all, just text"u8.ToArray()));
        Assert.IsNull(Read(Png(100, 100)[..20]));
        Assert.IsNull(ImageDimensions.TryReadAspectRatio(new MemoryStream([])));
    }

    [TestMethod]
    public void AJpegCutOffBeforeItsFrameHeaderGivesNull()
    {
        Assert.IsNull(Read(Jpeg(10, 10)[..30]));
    }

    [TestMethod]
    public void ZeroSizedImagesGiveNull()
    {
        Assert.IsNull(Read(Png(0, 100)));
        Assert.IsNull(Read(Gif(100, 0)));
    }
}
