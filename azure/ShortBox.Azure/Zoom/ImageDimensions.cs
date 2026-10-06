namespace ShortBox.Azure.Zoom;

/// <summary>
/// Reads the pixel size of a JPEG, PNG, GIF or WebP from its header without decoding it. The reader needs the shape of a page
/// to bound panning, and decoding a 4000 pixel page just to learn it would be wasteful. Does not apply EXIF orientation.
/// </summary>
public static class ImageDimensions
{
    /// <summary>The size, or null when the format is not recognised or the header is cut short. JPEG needs a seekable stream.</summary>
    public static (int Width, int Height)? TryRead(Stream stream)
    {
        try
        {
            Span<byte> head = stackalloc byte[32];
            var read = ReadFully(stream, head);
            if (read < 4)
            {
                return null;
            }

            head = head[..read];
            if (head[0] == 0x89 && head[1] == 'P' && head[2] == 'N' && head[3] == 'G')
            {
                return read >= 24 ? Valid(BigEndian32(head[16..]), BigEndian32(head[20..])) : null;
            }

            if (head[0] == 'G' && head[1] == 'I' && head[2] == 'F')
            {
                return read >= 10 ? Valid(LittleEndian16(head[6..]), LittleEndian16(head[8..])) : null;
            }

            if (head[0] == 0xFF && head[1] == 0xD8)
            {
                return ReadJpeg(stream);
            }

            if (head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F')
            {
                return ReadWebP(head);
            }

            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Width over height, or null when the size cannot be read. Leaves the stream position unspecified.</summary>
    public static double? TryReadAspectRatio(Stream stream) =>
        TryRead(stream) is var (width, height) ? (double)width / height : null;

    private static (int, int)? ReadJpeg(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return null;
        }

        // Segments follow the 2 byte start marker; the size is in the first start-of-frame segment.
        var position = 2L;
        Span<byte> segment = stackalloc byte[5];
        for (var i = 0; i < 512; i++)
        {
            stream.Position = position;
            if (ReadFully(stream, segment[..4]) < 4 || segment[0] != 0xFF)
            {
                return null;
            }

            var marker = segment[1];
            if (marker == 0xFF)
            {
                position++;
                continue;
            }

            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                position += 2;
                continue;
            }

            if (marker is 0xD9 or 0xDA)
            {
                return null;
            }

            var length = (segment[2] << 8) | segment[3];
            if (length < 2)
            {
                return null;
            }

            if (marker is (>= 0xC0 and <= 0xCF) and not (0xC4 or 0xC8 or 0xCC))
            {
                // Length, precision, then height and width.
                stream.Position = position + 5;
                Span<byte> size = stackalloc byte[4];
                if (ReadFully(stream, size) < 4)
                {
                    return null;
                }

                return Valid((size[2] << 8) | size[3], (size[0] << 8) | size[1]);
            }

            position += 2 + length;
        }

        return null;
    }

    private static (int, int)? ReadWebP(ReadOnlySpan<byte> head)
    {
        if (head.Length < 30 || head[8] != 'W' || head[9] != 'E' || head[10] != 'B' || head[11] != 'P')
        {
            return null;
        }

        switch ((char)head[12], (char)head[13], (char)head[14], (char)head[15])
        {
            case ('V', 'P', '8', ' '):
                return head[23] == 0x9D && head[24] == 0x01 && head[25] == 0x2A
                    ? Valid(LittleEndian16(head[26..]) & 0x3FFF, LittleEndian16(head[28..]) & 0x3FFF)
                    : null;
            case ('V', 'P', '8', 'L'):
                if (head[20] != 0x2F)
                {
                    return null;
                }

                var bits = (uint)(head[21] | (head[22] << 8) | (head[23] << 16)) | ((uint)head[24] << 24);
                return Valid((int)(1 + (bits & 0x3FFF)), (int)(1 + ((bits >> 14) & 0x3FFF)));
            case ('V', 'P', '8', 'X'):
                return Valid(1 + (head[24] | (head[25] << 8) | (head[26] << 16)), 1 + (head[27] | (head[28] << 8) | (head[29] << 16)));
            default:
                return null;
        }
    }

    private static (int, int)? Valid(int width, int height) => width > 0 && height > 0 ? (width, height) : null;

    private static int BigEndian32(ReadOnlySpan<byte> bytes) => (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];

    private static int LittleEndian16(ReadOnlySpan<byte> bytes) => bytes[0] | (bytes[1] << 8);

    private static int ReadFully(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
