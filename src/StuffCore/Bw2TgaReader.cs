using System.Buffers.Binary;
using System.Text;

namespace StuffCore;

public enum Bw2TgaImageType : byte
{
    UncompressedTrueColor = 2,
    UncompressedGrayscale = 3,
    RunLengthEncodedTrueColor = 10
}

public enum Bw2TgaVerticalOrigin
{
    Bottom,
    Top
}

public enum Bw2TgaHorizontalOrder
{
    LeftToRight,
    RightToLeft
}

public enum Bw2TgaInterleaving
{
    None,
    TwoWay,
    FourWay
}

public sealed record Bw2TgaInfo(
    ushort Width,
    ushort Height,
    Bw2TgaImageType ImageType,
    byte PixelDepth,
    byte AttributeBits,
    Bw2TgaVerticalOrigin VerticalOrigin,
    Bw2TgaHorizontalOrder HorizontalOrder,
    Bw2TgaInterleaving Interleaving,
    byte ImageIdLength,
    bool HasColorMap,
    ushort ColorMapFirstEntry,
    ushort ColorMapEntryCount,
    byte ColorMapEntryDepth,
    long PixelDataOffset,
    long PixelDataEndOffset,
    bool HasTga20Footer,
    bool HasExtensionArea,
    ushort? ExtensionAreaSize,
    ushort? ExtensionAreaStoredSize,
    bool UsesBw2ExtensionSizeCompatibility,
    bool HasDeveloperArea)
{
    public bool IsRunLengthEncoded => ImageType == Bw2TgaImageType.RunLengthEncodedTrueColor;
}

public static class Bw2TgaReader
{
    public const int HeaderSize = 18;
    public const int FooterSize = 26;

    private const byte NoColorMap = 0;
    private const byte ColorMapIncluded = 1;
    private const ushort Tga20ExtensionAreaSize = 495;
    private static readonly byte[] FooterSignature = Encoding.ASCII.GetBytes("TRUEVISION-XFILE.\0");

    public static bool TryRead(
        StuffArchive archive,
        StuffEntry entry,
        out Bw2TgaInfo? info,
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
            error = $"The TGA data could not be read: {exception.Message}";
            return false;
        }
    }

    public static bool TryRead(Stream stream, out Bw2TgaInfo? info, out string error)
    {
        ArgumentNullException.ThrowIfNull(stream);
        info = null;

        if (!stream.CanRead || !stream.CanSeek)
        {
            error = "The TGA reader requires a readable, seekable stream.";
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
            error = $"The TGA stream length could not be read: {exception.Message}";
            return false;
        }

        Span<byte> header = stackalloc byte[HeaderSize];
        if (!TryReadExactly(stream, header))
        {
            error = "The file is shorter than the 18-byte TGA header.";
            return false;
        }

        var imageIdLength = header[0];
        var colorMapType = header[1];
        if (colorMapType is not NoColorMap and not ColorMapIncluded)
        {
            error = $"The TGA header declares unsupported color-map type {colorMapType}.";
            return false;
        }

        if (!TryGetImageType(header[2], out var imageType))
        {
            error = $"The TGA image type {header[2]} is not supported by the BW2 reader.";
            return false;
        }

        var colorMapFirstEntry = ReadUInt16(header, 3);
        var colorMapEntryCount = ReadUInt16(header, 5);
        var colorMapEntryDepth = header[7];
        var width = ReadUInt16(header, 12);
        var height = ReadUInt16(header, 14);
        var pixelDepth = header[16];
        var descriptor = header[17];
        var attributeBits = (byte)(descriptor & 0x0F);
        var interleavingValue = (byte)(descriptor >> 6);

        if (width == 0 || height == 0)
        {
            error = "The TGA header declares a zero width or height.";
            return false;
        }

        if (!TryGetBytesPerPixel(imageType, pixelDepth, out var bytesPerPixel, out error))
            return false;

        if (attributeBits > 8 || attributeBits > pixelDepth)
        {
            error = "The TGA descriptor declares an invalid attribute-bit count.";
            return false;
        }

        if (!TryGetInterleaving(interleavingValue, out var interleaving))
        {
            error = "The TGA descriptor uses the reserved interleaving value 3.";
            return false;
        }

        long colorMapBytes;
        try
        {
            colorMapBytes = colorMapType == ColorMapIncluded
                ? checked((long)colorMapEntryCount * ((colorMapEntryDepth + 7L) / 8L))
                : 0;
        }
        catch (OverflowException)
        {
            error = "The TGA color-map byte count overflows the supported range.";
            return false;
        }

        if (colorMapType == ColorMapIncluded
            && (colorMapEntryCount == 0 || colorMapEntryDepth == 0))
        {
            error = "The TGA header declares a color map without entries or entry depth.";
            return false;
        }

        if (colorMapType == ColorMapIncluded
            && colorMapEntryDepth is not 15 and not 16 and not 24 and not 32)
        {
            error = $"The TGA color map declares unsupported {colorMapEntryDepth}-bit entries.";
            return false;
        }

        long pixelDataOffset;
        try
        {
            pixelDataOffset = checked(HeaderSize + imageIdLength + colorMapBytes);
        }
        catch (OverflowException)
        {
            error = "The TGA image-data offset overflows the supported range.";
            return false;
        }

        if (pixelDataOffset > fileLength)
        {
            error = "The TGA image ID or color map extends beyond the end of the file.";
            return false;
        }

        if (!TryReadFooter(
                stream,
                fileLength,
                out var hasFooter,
                out var extensionOffset,
                out var developerOffset,
                out error))
        {
            return false;
        }

        var footerOffset = hasFooter ? fileLength - FooterSize : fileLength;
        var payloadLimit = footerOffset;
        if (!TryApplyTrailingAreaOffset(extensionOffset, pixelDataOffset, footerOffset, ref payloadLimit, "extension", out error)
            || !TryApplyTrailingAreaOffset(developerOffset, pixelDataOffset, footerOffset, ref payloadLimit, "developer", out error))
        {
            return false;
        }

        ushort? extensionAreaSize = null;
        ushort? extensionAreaStoredSize = null;
        var usesBw2ExtensionSizeCompatibility = false;
        if (extensionOffset != 0)
        {
            if (!TryReadExtensionAreaSize(
                    stream,
                    extensionOffset,
                    developerOffset,
                    footerOffset,
                    out var size,
                    out var storedSize,
                    out usesBw2ExtensionSizeCompatibility,
                    out error))
            {
                return false;
            }

            extensionAreaSize = size;
            extensionAreaStoredSize = storedSize;
        }

        if (developerOffset != 0
            && !TryValidateDeveloperArea(stream, developerOffset, footerOffset, out error))
        {
            return false;
        }

        if (!TryValidateImagePayload(
                stream,
                pixelDataOffset,
                payloadLimit,
                width,
                height,
                bytesPerPixel,
                imageType == Bw2TgaImageType.RunLengthEncodedTrueColor,
                out var pixelDataEndOffset,
                out error))
        {
            return false;
        }

        info = new Bw2TgaInfo(
            width,
            height,
            imageType,
            pixelDepth,
            attributeBits,
            (descriptor & 0x20) != 0 ? Bw2TgaVerticalOrigin.Top : Bw2TgaVerticalOrigin.Bottom,
            (descriptor & 0x10) != 0 ? Bw2TgaHorizontalOrder.RightToLeft : Bw2TgaHorizontalOrder.LeftToRight,
            interleaving,
            imageIdLength,
            colorMapType == ColorMapIncluded,
            colorMapFirstEntry,
            colorMapEntryCount,
            colorMapEntryDepth,
            pixelDataOffset,
            pixelDataEndOffset,
            hasFooter,
            extensionOffset != 0,
            extensionAreaSize,
            extensionAreaStoredSize,
            usesBw2ExtensionSizeCompatibility,
            developerOffset != 0);
        error = string.Empty;
        return true;
    }

    private static bool TryGetImageType(byte value, out Bw2TgaImageType imageType)
    {
        imageType = value switch
        {
            (byte)Bw2TgaImageType.UncompressedTrueColor => Bw2TgaImageType.UncompressedTrueColor,
            (byte)Bw2TgaImageType.UncompressedGrayscale => Bw2TgaImageType.UncompressedGrayscale,
            (byte)Bw2TgaImageType.RunLengthEncodedTrueColor => Bw2TgaImageType.RunLengthEncodedTrueColor,
            _ => default
        };
        return value is (byte)Bw2TgaImageType.UncompressedTrueColor
            or (byte)Bw2TgaImageType.UncompressedGrayscale
            or (byte)Bw2TgaImageType.RunLengthEncodedTrueColor;
    }

    private static bool TryGetBytesPerPixel(
        Bw2TgaImageType imageType,
        byte pixelDepth,
        out int bytesPerPixel,
        out string error)
    {
        bytesPerPixel = 0;
        if (imageType == Bw2TgaImageType.UncompressedGrayscale)
        {
            if (pixelDepth != 8)
            {
                error = $"The BW2 TGA reader supports 8-bit grayscale data, not {pixelDepth}-bit grayscale.";
                return false;
            }

            bytesPerPixel = 1;
            error = string.Empty;
            return true;
        }

        if (pixelDepth is not 24 and not 32)
        {
            error = $"The BW2 TGA reader supports 24-bit and 32-bit true-color data, not {pixelDepth}-bit data.";
            return false;
        }

        bytesPerPixel = pixelDepth / 8;
        error = string.Empty;
        return true;
    }

    private static bool TryGetInterleaving(byte value, out Bw2TgaInterleaving interleaving)
    {
        interleaving = value switch
        {
            0 => Bw2TgaInterleaving.None,
            1 => Bw2TgaInterleaving.TwoWay,
            2 => Bw2TgaInterleaving.FourWay,
            _ => default
        };
        return value <= 2;
    }

    private static bool TryReadFooter(
        Stream stream,
        long fileLength,
        out bool hasFooter,
        out uint extensionOffset,
        out uint developerOffset,
        out string error)
    {
        hasFooter = false;
        extensionOffset = 0;
        developerOffset = 0;
        error = string.Empty;

        if (fileLength < FooterSize)
            return true;

        stream.Position = fileLength - FooterSize;
        Span<byte> footer = stackalloc byte[FooterSize];
        if (!TryReadExactly(stream, footer))
        {
            error = "The final TGA footer bytes could not be read.";
            return false;
        }

        if (!footer[8..].SequenceEqual(FooterSignature))
            return true;

        hasFooter = true;
        extensionOffset = ReadUInt32(footer, 0);
        developerOffset = ReadUInt32(footer, 4);
        return true;
    }

    private static bool TryApplyTrailingAreaOffset(
        uint offset,
        long pixelDataOffset,
        long footerOffset,
        ref long payloadLimit,
        string areaName,
        out string error)
    {
        if (offset == 0)
        {
            error = string.Empty;
            return true;
        }

        if (offset < pixelDataOffset || offset >= footerOffset)
        {
            error = $"The TGA {areaName}-area offset lies outside the valid trailing-data range.";
            return false;
        }

        payloadLimit = Math.Min(payloadLimit, offset);
        error = string.Empty;
        return true;
    }

    private static bool TryReadExtensionAreaSize(
        Stream stream,
        uint extensionOffset,
        uint developerOffset,
        long footerOffset,
        out ushort size,
        out ushort storedSize,
        out bool usesBw2Compatibility,
        out string error)
    {
        size = 0;
        storedSize = 0;
        usesBw2Compatibility = false;
        if ((long)extensionOffset + sizeof(ushort) > footerOffset)
        {
            error = "The TGA extension area is truncated before its size field.";
            return false;
        }

        stream.Position = extensionOffset;
        Span<byte> bytes = stackalloc byte[2];
        if (!TryReadExactly(stream, bytes))
        {
            error = "The TGA extension-area size could not be read.";
            return false;
        }

        size = ReadUInt16(bytes, 0);
        if (size != Tga20ExtensionAreaSize && size != Tga20ExtensionAreaSize - 1)
        {
            error = $"The TGA 2.0 extension area declares size {size}, not 495 bytes.";
            return false;
        }

        var trailingBoundary = developerOffset > extensionOffset
            ? Math.Min((long)developerOffset, footerOffset)
            : footerOffset;
        if ((long)extensionOffset + Tga20ExtensionAreaSize > trailingBoundary)
        {
            error = size == Tga20ExtensionAreaSize - 1
                ? "The TGA extension area declares the BW2-compatible size 494, but does not store the complete 495-byte area."
                : "The TGA extension area extends beyond the next TGA 2.0 trailing area or footer.";
            return false;
        }

        storedSize = Tga20ExtensionAreaSize;
        usesBw2Compatibility = size == Tga20ExtensionAreaSize - 1;
        error = string.Empty;
        return true;
    }

    private static bool TryValidateDeveloperArea(
        Stream stream,
        uint developerOffset,
        long footerOffset,
        out string error)
    {
        if ((long)developerOffset + sizeof(ushort) > footerOffset)
        {
            error = "The TGA developer area is truncated before its tag count.";
            return false;
        }

        stream.Position = developerOffset;
        Span<byte> bytes = stackalloc byte[2];
        if (!TryReadExactly(stream, bytes))
        {
            error = "The TGA developer-area tag count could not be read.";
            return false;
        }

        var tagCount = ReadUInt16(bytes, 0);
        long directoryEnd;
        try
        {
            directoryEnd = checked((long)developerOffset + 2L + tagCount * 10L);
        }
        catch (OverflowException)
        {
            error = "The TGA developer directory size overflows the supported range.";
            return false;
        }

        if (directoryEnd > footerOffset)
        {
            error = "The TGA developer directory extends beyond the TGA 2.0 footer.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateImagePayload(
        Stream stream,
        long pixelDataOffset,
        long payloadLimit,
        ushort width,
        ushort height,
        int bytesPerPixel,
        bool isRunLengthEncoded,
        out long pixelDataEndOffset,
        out string error)
    {
        pixelDataEndOffset = pixelDataOffset;
        long pixelCount;
        try
        {
            pixelCount = checked((long)width * height);
        }
        catch (OverflowException)
        {
            error = "The TGA pixel count overflows the supported range.";
            return false;
        }

        if (!isRunLengthEncoded)
        {
            long payloadSize;
            try
            {
                payloadSize = checked(pixelCount * bytesPerPixel);
                pixelDataEndOffset = checked(pixelDataOffset + payloadSize);
            }
            catch (OverflowException)
            {
                error = "The TGA pixel payload size overflows the supported range.";
                return false;
            }

            if (pixelDataEndOffset > payloadLimit)
            {
                error = "The TGA pixel payload is truncated.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        stream.Position = pixelDataOffset;
        long decodedPixels = 0;
        while (decodedPixels < pixelCount)
        {
            if (stream.Position >= payloadLimit)
            {
                error = "The TGA RLE stream ends before all declared pixels are decoded.";
                return false;
            }

            var packetHeader = stream.ReadByte();
            if (packetHeader < 0)
            {
                error = "The TGA RLE packet header is truncated.";
                return false;
            }

            var packetPixels = (packetHeader & 0x7F) + 1;
            if (decodedPixels + packetPixels > pixelCount)
            {
                error = "A TGA RLE packet decodes beyond the declared image pixel count.";
                return false;
            }

            if ((decodedPixels % width) + packetPixels > width)
            {
                error = "A TGA RLE packet crosses a scanline boundary.";
                return false;
            }

            var packetBytes = (packetHeader & 0x80) != 0
                ? bytesPerPixel
                : checked(packetPixels * bytesPerPixel);
            if (stream.Position + packetBytes > payloadLimit)
            {
                error = "The TGA RLE packet payload is truncated.";
                return false;
            }

            stream.Position += packetBytes;
            decodedPixels += packetPixels;
        }

        pixelDataEndOffset = stream.Position;
        error = string.Empty;
        return true;
    }

    private static bool TryReadExactly(Stream stream, Span<byte> destination)
    {
        var totalRead = 0;
        while (totalRead < destination.Length)
        {
            int read;
            try
            {
                read = stream.Read(destination[totalRead..]);
            }
            catch (IOException)
            {
                return false;
            }

            if (read == 0)
                return false;
            totalRead += read;
        }

        return true;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, sizeof(ushort)));

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, sizeof(uint)));
}
