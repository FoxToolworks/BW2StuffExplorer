using System.Buffers.Binary;

namespace StuffCore;

public enum Bw2BmpCompression : uint
{
    Rgb = 0
}

public enum Bw2BmpRowOrder
{
    BottomUp,
    TopDown
}

public sealed record Bw2BmpInfo(
    int Width,
    int Height,
    Bw2BmpRowOrder RowOrder,
    ushort PixelDepth,
    Bw2BmpCompression Compression,
    uint DibHeaderSize,
    uint StoredFileSize,
    uint StoredImageSize,
    uint PixelDataOffset,
    long RowStride,
    long PixelDataLength,
    long PixelDataEndOffset);

public static class Bw2BmpReader
{
    public const int FileHeaderSize = 14;
    public const int BitmapInfoHeaderSize = 40;
    public const int MinimumSize = FileHeaderSize + BitmapInfoHeaderSize;

    private const ushort Signature = 0x4D42;

    public static bool TryRead(
        StuffArchive archive,
        StuffEntry entry,
        out Bw2BmpInfo? info,
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
            error = $"The BMP data could not be read: {exception.Message}";
            return false;
        }
    }

    public static bool TryRead(Stream stream, out Bw2BmpInfo? info, out string error)
    {
        ArgumentNullException.ThrowIfNull(stream);
        info = null;

        if (!stream.CanRead || !stream.CanSeek)
        {
            error = "The BMP reader requires a readable, seekable stream.";
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
            error = $"The BMP stream length could not be read: {exception.Message}";
            return false;
        }

        if (fileLength > uint.MaxValue)
        {
            error = "The BMP exceeds the 32-bit file-size range of its header.";
            return false;
        }

        Span<byte> header = stackalloc byte[MinimumSize];
        if (!TryReadExactly(stream, header))
        {
            error = "The file is shorter than the 14-byte BMP file header and 40-byte BITMAPINFOHEADER.";
            return false;
        }

        if (ReadUInt16(header, 0) != Signature)
        {
            error = "The BMP signature is not BM.";
            return false;
        }

        var storedFileSize = ReadUInt32(header, 2);
        if (storedFileSize != fileLength)
        {
            error = $"The BMP header stores file size {storedFileSize}, but the source contains {fileLength} bytes.";
            return false;
        }

        if (ReadUInt16(header, 6) != 0 || ReadUInt16(header, 8) != 0)
        {
            error = "The BMP file header uses non-zero reserved fields.";
            return false;
        }

        var pixelDataOffset = ReadUInt32(header, 10);
        var dibHeaderSize = ReadUInt32(header, 14);
        if (dibHeaderSize != BitmapInfoHeaderSize)
        {
            error = $"The BW2 BMP reader supports the 40-byte BITMAPINFOHEADER, not a {dibHeaderSize}-byte DIB header.";
            return false;
        }

        if (pixelDataOffset != MinimumSize)
        {
            error = $"The BW2 BMP corpus stores pixel data at byte 54 without a color table, not byte {pixelDataOffset}.";
            return false;
        }

        var storedWidth = ReadInt32(header, 18);
        var storedHeight = ReadInt32(header, 22);
        if (storedWidth <= 0 || storedHeight == 0 || storedHeight == int.MinValue)
        {
            error = "The BMP declares an unsupported zero, negative width, or invalid height.";
            return false;
        }

        if (ReadUInt16(header, 26) != 1)
        {
            error = "The BMP DIB header must declare exactly one color plane.";
            return false;
        }

        var pixelDepth = ReadUInt16(header, 28);
        if (pixelDepth is not 24 and not 32)
        {
            error = $"The BW2 BMP reader supports 24-bit and 32-bit pixels, not {pixelDepth}-bit pixels.";
            return false;
        }

        var compressionValue = ReadUInt32(header, 30);
        if (compressionValue != (uint)Bw2BmpCompression.Rgb)
        {
            error = $"The BW2 BMP reader supports uncompressed BI_RGB data, not compression value {compressionValue}.";
            return false;
        }

        var storedImageSize = ReadUInt32(header, 34);
        var colorsUsed = ReadUInt32(header, 46);
        if (colorsUsed != 0)
        {
            error = $"The BW2 BMP reader does not support a declared color table ({colorsUsed} entries).";
            return false;
        }

        var height = Math.Abs(storedHeight);
        long rowStride;
        long pixelDataLength;
        long pixelDataEndOffset;
        try
        {
            rowStride = checked(((long)storedWidth * pixelDepth + 31L) / 32L * 4L);
            pixelDataLength = checked(rowStride * height);
            pixelDataEndOffset = checked((long)pixelDataOffset + pixelDataLength);
        }
        catch (OverflowException)
        {
            error = "The BMP dimensions overflow the supported pixel-payload range.";
            return false;
        }

        if (storedImageSize != 0 && storedImageSize != pixelDataLength)
        {
            error = $"The BMP stores image size {storedImageSize}, but its padded rows require {pixelDataLength} bytes.";
            return false;
        }

        if (pixelDataEndOffset > fileLength)
        {
            error = "The BMP pixel payload extends beyond the end of the file.";
            return false;
        }

        info = new Bw2BmpInfo(
            storedWidth,
            height,
            storedHeight < 0 ? Bw2BmpRowOrder.TopDown : Bw2BmpRowOrder.BottomUp,
            pixelDepth,
            Bw2BmpCompression.Rgb,
            dibHeaderSize,
            storedFileSize,
            storedImageSize,
            pixelDataOffset,
            rowStride,
            pixelDataLength,
            pixelDataEndOffset);
        error = string.Empty;
        return true;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, sizeof(ushort)));

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, sizeof(uint)));

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, sizeof(int)));

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
