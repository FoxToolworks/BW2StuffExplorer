using System.Buffers.Binary;

namespace StuffCore;

public enum Bw2DdsNonOpaqueAlphaStatus
{
    Unknown,
    NotApplicable,
    No,
    Yes
}

public static class Bw2DdsAlphaAnalyzer
{
    private enum BlockFormat
    {
        Bc1,
        Bc2,
        Bc3
    }

    public static Bw2DdsNonOpaqueAlphaStatus Analyze(
        StuffArchive archive,
        StuffEntry entry,
        Bw2DdsInfo info)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(info);

        try
        {
            using var stream = archive.OpenEntry(entry);
            return Analyze(stream, info);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Bw2DdsNonOpaqueAlphaStatus.Unknown;
        }
    }

    public static Bw2DdsNonOpaqueAlphaStatus Analyze(Stream stream, Bw2DdsInfo info)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(info);

        if (info.AlphaCapability == Bw2DdsAlphaCapability.No)
            return Bw2DdsNonOpaqueAlphaStatus.NotApplicable;
        if (info.AlphaCapability != Bw2DdsAlphaCapability.Yes)
            return Bw2DdsNonOpaqueAlphaStatus.Unknown;
        if (!TryGetBlockFormat(info, out var format))
            return Bw2DdsNonOpaqueAlphaStatus.Unknown;

        try
        {
            if (!stream.CanSeek)
                return Bw2DdsNonOpaqueAlphaStatus.Unknown;

            var headerSize = info.HeaderKind == Bw2DdsHeaderKind.Dx10Extended
                ? Bw2DdsReader.Dx10HeaderSize
                : Bw2DdsReader.LegacyHeaderSize;

            if (!HasValidSubresourceLayout(info, out var surfaceCount, out var isVolume))
                return Bw2DdsNonOpaqueAlphaStatus.Unknown;
            var payloadSize = CalculatePayloadSize(info, format, surfaceCount, isVolume);
            if (stream.Length < headerSize || (ulong)(stream.Length - headerSize) < payloadSize)
                return Bw2DdsNonOpaqueAlphaStatus.Unknown;

            stream.Seek(headerSize, SeekOrigin.Begin);

            if (isVolume)
                return AnalyzeVolume(stream, info, format);

            for (uint surface = 0; surface < surfaceCount; surface++)
            {
                var result = AnalyzeMipChain(stream, info.Width, info.Height, info.MipLevelCount, format);
                if (result != Bw2DdsNonOpaqueAlphaStatus.No)
                    return result;
            }

            return Bw2DdsNonOpaqueAlphaStatus.No;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or OverflowException)
        {
            return Bw2DdsNonOpaqueAlphaStatus.Unknown;
        }
    }

    private static Bw2DdsNonOpaqueAlphaStatus AnalyzeVolume(
        Stream stream,
        Bw2DdsInfo info,
        BlockFormat format)
    {
        var width = info.Width;
        var height = info.Height;
        var depth = info.Depth;

        for (uint mip = 0; mip < info.MipLevelCount; mip++)
        {
            for (uint slice = 0; slice < depth; slice++)
            {
                var result = AnalyzePlane(stream, width, height, format);
                if (result != Bw2DdsNonOpaqueAlphaStatus.No)
                    return result;
            }

            width = Math.Max(1u, width / 2);
            height = Math.Max(1u, height / 2);
            depth = Math.Max(1u, depth / 2);
        }

        return Bw2DdsNonOpaqueAlphaStatus.No;
    }

    private static Bw2DdsNonOpaqueAlphaStatus AnalyzeMipChain(
        Stream stream,
        uint initialWidth,
        uint initialHeight,
        uint mipLevelCount,
        BlockFormat format)
    {
        var width = initialWidth;
        var height = initialHeight;

        for (uint mip = 0; mip < mipLevelCount; mip++)
        {
            var result = AnalyzePlane(stream, width, height, format);
            if (result != Bw2DdsNonOpaqueAlphaStatus.No)
                return result;

            width = Math.Max(1u, width / 2);
            height = Math.Max(1u, height / 2);
        }

        return Bw2DdsNonOpaqueAlphaStatus.No;
    }

    private static Bw2DdsNonOpaqueAlphaStatus AnalyzePlane(
        Stream stream,
        uint width,
        uint height,
        BlockFormat format)
    {
        var blocksWide = checked((width + 3u) / 4u);
        var blocksHigh = checked((height + 3u) / 4u);
        var blockSize = format == BlockFormat.Bc1 ? 8 : 16;
        Span<byte> block = stackalloc byte[16];

        for (uint blockY = 0; blockY < blocksHigh; blockY++)
        {
            var validHeight = (int)Math.Min(4u, height - blockY * 4u);
            for (uint blockX = 0; blockX < blocksWide; blockX++)
            {
                var validWidth = (int)Math.Min(4u, width - blockX * 4u);
                var currentBlock = block[..blockSize];
                if (!TryReadExactly(stream, currentBlock))
                    return Bw2DdsNonOpaqueAlphaStatus.Unknown;
                if (ContainsNonOpaqueAlpha(currentBlock, validWidth, validHeight, format))
                    return Bw2DdsNonOpaqueAlphaStatus.Yes;
            }
        }

        return Bw2DdsNonOpaqueAlphaStatus.No;
    }

    private static bool ContainsNonOpaqueAlpha(
        ReadOnlySpan<byte> block,
        int validWidth,
        int validHeight,
        BlockFormat format) => format switch
    {
        BlockFormat.Bc1 => Bc1ContainsNonOpaqueAlpha(block, validWidth, validHeight),
        BlockFormat.Bc2 => Bc2ContainsNonOpaqueAlpha(block, validWidth, validHeight),
        BlockFormat.Bc3 => Bc3ContainsNonOpaqueAlpha(block, validWidth, validHeight),
        _ => false
    };

    private static bool Bc1ContainsNonOpaqueAlpha(
        ReadOnlySpan<byte> block,
        int validWidth,
        int validHeight)
    {
        var color0 = BinaryPrimitives.ReadUInt16LittleEndian(block);
        var color1 = BinaryPrimitives.ReadUInt16LittleEndian(block[2..]);
        if (color0 > color1)
            return false;

        var selectors = BinaryPrimitives.ReadUInt32LittleEndian(block[4..]);
        for (var y = 0; y < validHeight; y++)
        {
            for (var x = 0; x < validWidth; x++)
            {
                var pixel = y * 4 + x;
                if (((selectors >> (pixel * 2)) & 0x03u) == 3u)
                    return true;
            }
        }

        return false;
    }

    private static bool Bc2ContainsNonOpaqueAlpha(
        ReadOnlySpan<byte> block,
        int validWidth,
        int validHeight)
    {
        var alpha = BinaryPrimitives.ReadUInt64LittleEndian(block);
        for (var y = 0; y < validHeight; y++)
        {
            for (var x = 0; x < validWidth; x++)
            {
                var pixel = y * 4 + x;
                if (((alpha >> (pixel * 4)) & 0x0Fu) != 0x0Fu)
                    return true;
            }
        }

        return false;
    }

    private static bool Bc3ContainsNonOpaqueAlpha(
        ReadOnlySpan<byte> block,
        int validWidth,
        int validHeight)
    {
        Span<byte> palette = stackalloc byte[8];
        palette[0] = block[0];
        palette[1] = block[1];

        if (palette[0] > palette[1])
        {
            for (var index = 2; index < 8; index++)
            {
                palette[index] = (byte)(
                    ((8 - index) * palette[0] + (index - 1) * palette[1]) / 7);
            }
        }
        else
        {
            for (var index = 2; index < 6; index++)
            {
                palette[index] = (byte)(
                    ((6 - index) * palette[0] + (index - 1) * palette[1]) / 5);
            }

            palette[6] = 0;
            palette[7] = 255;
        }

        var selectors = BinaryPrimitives.ReadUInt64LittleEndian(block) >> 16;
        for (var y = 0; y < validHeight; y++)
        {
            for (var x = 0; x < validWidth; x++)
            {
                var pixel = y * 4 + x;
                var paletteIndex = (int)((selectors >> (pixel * 3)) & 0x07u);
                if (palette[paletteIndex] < 255)
                    return true;
            }
        }

        return false;
    }

    private static bool HasValidSubresourceLayout(
        Bw2DdsInfo info,
        out uint surfaceCount,
        out bool isVolume)
    {
        surfaceCount = 0;
        isVolume = info.TextureKind == Bw2DdsTextureKind.Texture3D;

        if (info.TextureKind == Bw2DdsTextureKind.Texture1D && info.Height != 1)
            return false;

        var largestDimension = Math.Max(info.Width, info.Height);
        if (isVolume)
        {
            if (info.Depth == 0 || info.ArraySize is > 1)
                return false;
            largestDimension = Math.Max(largestDimension, info.Depth);
        }

        var maximumMipLevels = 1u;
        while (largestDimension > 1)
        {
            largestDimension /= 2;
            maximumMipLevels++;
        }

        if (info.MipLevelCount == 0 || info.MipLevelCount > maximumMipLevels)
            return false;

        surfaceCount = info.TextureKind switch
        {
            Bw2DdsTextureKind.Texture1D or Bw2DdsTextureKind.Texture2D => 1,
            Bw2DdsTextureKind.TextureArray => info.ArraySize ?? 0,
            Bw2DdsTextureKind.Cubemap when info.HeaderKind == Bw2DdsHeaderKind.Legacy =>
                (uint)(info.DeclaredCubemapFaceCount ?? 0),
            Bw2DdsTextureKind.Cubemap => 6,
            Bw2DdsTextureKind.CubemapArray => checked((info.ArraySize ?? 0) * 6u),
            Bw2DdsTextureKind.Texture3D => 1,
            _ => 0
        };

        return surfaceCount > 0;
    }

    private static ulong CalculatePayloadSize(
        Bw2DdsInfo info,
        BlockFormat format,
        uint surfaceCount,
        bool isVolume)
    {
        var width = info.Width;
        var height = info.Height;
        var depth = isVolume ? info.Depth : 1u;
        ulong mipChainSize = 0;

        for (uint mip = 0; mip < info.MipLevelCount; mip++)
        {
            var blocksWide = ((ulong)width + 3) / 4;
            var blocksHigh = ((ulong)height + 3) / 4;
            var blockSize = format == BlockFormat.Bc1 ? 8u : 16u;
            var planeSize = checked(blocksWide * blocksHigh * blockSize);
            mipChainSize = checked(mipChainSize + planeSize * depth);

            width = Math.Max(1u, width / 2);
            height = Math.Max(1u, height / 2);
            if (isVolume)
                depth = Math.Max(1u, depth / 2);
        }

        return isVolume ? mipChainSize : checked(mipChainSize * surfaceCount);
    }

    private static bool TryGetBlockFormat(Bw2DdsInfo info, out BlockFormat format)
    {
        if (info.HeaderKind == Bw2DdsHeaderKind.Dx10Extended)
        {
            format = info.DxgiFormatValue switch
            {
                70 or 71 or 72 => BlockFormat.Bc1,
                73 or 74 or 75 => BlockFormat.Bc2,
                76 or 77 or 78 => BlockFormat.Bc3,
                _ => default
            };
            return info.DxgiFormatValue is >= 70 and <= 78;
        }

        format = info.FourCc switch
        {
            "DXT1" => BlockFormat.Bc1,
            "DXT2" or "DXT3" => BlockFormat.Bc2,
            "DXT4" or "DXT5" => BlockFormat.Bc3,
            _ => default
        };
        return info.FourCc is "DXT1" or "DXT2" or "DXT3" or "DXT4" or "DXT5";
    }

    private static bool TryReadExactly(Stream stream, Span<byte> destination)
    {
        var totalRead = 0;
        while (totalRead < destination.Length)
        {
            var read = stream.Read(destination[totalRead..]);
            if (read == 0)
                return false;
            totalRead += read;
        }

        return true;
    }
}
