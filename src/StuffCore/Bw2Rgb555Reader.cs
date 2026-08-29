using System.Buffers.Binary;

namespace StuffCore;

public enum Bw2Rgb555PixelFormat
{
    X1R5G5B5LittleEndian
}

public sealed record Bw2Rgb555Info(
    int Width,
    int Height,
    ushort PixelDepth,
    Bw2Rgb555PixelFormat PixelFormat,
    uint StoredHeaderValue,
    long PixelDataOffset,
    long PixelDataLength,
    int PixelCount,
    int SetHighBitPixelCount);

public static class Bw2Rgb555Reader
{
    public const int HeaderSize = 16;
    public const int Width = 256;
    public const int Height = 256;
    public const ushort PixelDepth = 16;
    public const int BytesPerPixel = 2;
    public const int PixelCount = Width * Height;
    public const int PixelDataSize = PixelCount * BytesPerPixel;
    public const int FileSize = HeaderSize + PixelDataSize;

    private const uint LeadingHeaderValue = 0;

    public static bool TryRead(
        StuffArchive archive,
        StuffEntry entry,
        out Bw2Rgb555Info? info,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            using var stream = archive.OpenEntry(entry);
            return TryRead(stream, out info, out error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            info = null;
            error = $"The .555 data could not be read: {exception.Message}";
            return false;
        }
    }

    public static bool TryRead(Stream stream, out Bw2Rgb555Info? info, out string error)
    {
        ArgumentNullException.ThrowIfNull(stream);
        info = null;

        if (!stream.CanRead || !stream.CanSeek)
        {
            error = "The .555 reader requires a readable, seekable stream.";
            return false;
        }

        long fileLength;
        try
        {
            fileLength = stream.Length;
            stream.Position = 0;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            error = $"The .555 stream length could not be read: {exception.Message}";
            return false;
        }

        if (fileLength != FileSize)
        {
            error = $"The BW2 .555 layout requires exactly {FileSize} bytes, but the source contains {fileLength} bytes.";
            return false;
        }

        Span<byte> header = stackalloc byte[HeaderSize];
        if (!TryReadExactly(stream, header))
        {
            error = "The file is shorter than the 16-byte BW2 .555 header.";
            return false;
        }

        var leadingValue = ReadUInt32(header, 0);
        if (leadingValue != LeadingHeaderValue)
        {
            error = $"The BW2 .555 corpus stores zero in the first header field, not 0x{leadingValue:X8}.";
            return false;
        }

        var width = ReadUInt32(header, 4);
        var height = ReadUInt32(header, 8);
        if (width != Width || height != Height)
        {
            error = $"The confirmed BW2 .555 layout is 256 x 256 pixels, not {width} x {height}.";
            return false;
        }

        var storedHeaderValue = ReadUInt32(header, 12);
        var pixelData = new byte[PixelDataSize];
        if (!TryReadExactly(stream, pixelData))
        {
            error = "The .555 pixel payload is truncated.";
            return false;
        }

        var setHighBitPixelCount = 0;
        for (var offset = 0; offset < pixelData.Length; offset += BytesPerPixel)
        {
            var pixel = BinaryPrimitives.ReadUInt16LittleEndian(pixelData.AsSpan(offset, BytesPerPixel));
            if ((pixel & 0x8000) != 0)
                setHighBitPixelCount++;
        }

        if (setHighBitPixelCount != 0)
        {
            error = $"The confirmed BW2 X1R5G5B5 layout keeps bit 15 clear, but {setHighBitPixelCount} pixels set it.";
            return false;
        }

        info = new Bw2Rgb555Info(
            Width,
            Height,
            PixelDepth,
            Bw2Rgb555PixelFormat.X1R5G5B5LittleEndian,
            storedHeaderValue,
            HeaderSize,
            PixelDataSize,
            PixelCount,
            setHighBitPixelCount);
        error = string.Empty;
        return true;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, sizeof(uint)));

    private static bool TryReadExactly(Stream stream, Span<byte> buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer[totalRead..]);
            if (read == 0)
                return false;
            totalRead += read;
        }

        return true;
    }
}
