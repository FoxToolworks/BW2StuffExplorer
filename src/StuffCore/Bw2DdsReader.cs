using System.Buffers.Binary;
using System.Text;

namespace StuffCore;

public enum Bw2DdsHeaderKind
{
    Legacy,
    Dx10Extended
}

public enum Bw2DdsTextureKind
{
    Unknown,
    Texture1D,
    Texture2D,
    Texture3D,
    TextureArray,
    Cubemap,
    CubemapArray
}

public enum Bw2DdsColorSpace
{
    Unknown,
    Linear,
    Srgb
}

public enum Bw2DdsAlphaCapability
{
    Unknown,
    No,
    Yes
}

public enum Bw2DdsDataLayout
{
    Unknown,
    Pitch,
    LinearSize
}

public sealed record Bw2DdsInfo(
    Bw2DdsHeaderKind HeaderKind,
    uint Width,
    uint Height,
    uint Depth,
    uint MipLevelCount,
    Bw2DdsTextureKind TextureKind,
    string PixelFormat,
    string? FourCc,
    string? DxgiFormat,
    uint? DxgiFormatValue,
    uint? ArraySize,
    int? DeclaredCubemapFaceCount,
    Bw2DdsColorSpace ColorSpace,
    Bw2DdsAlphaCapability AlphaCapability,
    Bw2DdsDataLayout DataLayout,
    uint PitchOrLinearSize);

public static class Bw2DdsReader
{
    public const int LegacyHeaderSize = 128;
    public const int Dx10HeaderSize = 148;

    private const uint DdsMagic = 0x20534444;
    private const uint DdsHeaderStructureSize = 124;
    private const uint DdsPixelFormatStructureSize = 32;

    private const uint DdsdPitch = 0x00000008;
    private const uint DdsdLinearSize = 0x00080000;
    private const uint DdpfAlphaPixels = 0x00000001;
    private const uint DdpfAlpha = 0x00000002;
    private const uint DdpfFourCc = 0x00000004;
    private const uint DdpfRgb = 0x00000040;
    private const uint DdpfYuv = 0x00000200;
    private const uint DdpfLuminance = 0x00020000;

    private const uint DdsCaps2Cubemap = 0x00000200;
    private const uint DdsCaps2CubemapPositiveX = 0x00000400;
    private const uint DdsCaps2CubemapNegativeX = 0x00000800;
    private const uint DdsCaps2CubemapPositiveY = 0x00001000;
    private const uint DdsCaps2CubemapNegativeY = 0x00002000;
    private const uint DdsCaps2CubemapPositiveZ = 0x00004000;
    private const uint DdsCaps2CubemapNegativeZ = 0x00008000;
    private const uint DdsCaps2Volume = 0x00200000;

    private const uint D3D10ResourceDimensionTexture1D = 2;
    private const uint D3D10ResourceDimensionTexture2D = 3;
    private const uint D3D10ResourceDimensionTexture3D = 4;
    private const uint D3D10ResourceMiscTextureCube = 0x00000004;

    public static bool TryRead(
        StuffArchive archive,
        StuffEntry entry,
        out Bw2DdsInfo? info,
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
            error = $"The DDS header could not be read: {exception.Message}";
            return false;
        }
    }

    public static bool TryRead(Stream stream, out Bw2DdsInfo? info, out string error)
    {
        ArgumentNullException.ThrowIfNull(stream);
        info = null;

        Span<byte> header = stackalloc byte[LegacyHeaderSize];
        if (!TryReadExactly(stream, header))
        {
            error = "The file is shorter than the 128-byte DDS header.";
            return false;
        }

        if (ReadUInt32(header, 0) != DdsMagic)
        {
            error = "The DDS magic signature is missing.";
            return false;
        }

        if (ReadUInt32(header, 4) != DdsHeaderStructureSize)
        {
            error = "The DDS_HEADER size is not 124 bytes.";
            return false;
        }

        if (ReadUInt32(header, 76) != DdsPixelFormatStructureSize)
        {
            error = "The DDS_PIXELFORMAT size is not 32 bytes.";
            return false;
        }

        var flags = ReadUInt32(header, 8);
        var height = ReadUInt32(header, 12);
        var width = ReadUInt32(header, 16);
        var pitchOrLinearSize = ReadUInt32(header, 20);
        var depth = ReadUInt32(header, 24);
        var rawMipMapCount = ReadUInt32(header, 28);
        var pixelFormatFlags = ReadUInt32(header, 80);
        var fourCcValue = ReadUInt32(header, 84);
        var rgbBitCount = ReadUInt32(header, 88);
        var alphaMask = ReadUInt32(header, 104);
        var caps2 = ReadUInt32(header, 112);

        if (width == 0 || height == 0)
        {
            error = "The DDS header declares a zero width or height.";
            return false;
        }

        var fourCc = (pixelFormatFlags & DdpfFourCc) != 0
            ? DecodeFourCc(fourCcValue)
            : null;
        var hasDx10Header = string.Equals(fourCc, "DX10", StringComparison.Ordinal);

        uint? dxgiFormatValue = null;
        string? dxgiFormat = null;
        uint? arraySize = null;
        uint resourceDimension = 0;
        uint miscFlag = 0;

        if (hasDx10Header)
        {
            Span<byte> dx10Header = stackalloc byte[Dx10HeaderSize - LegacyHeaderSize];
            if (!TryReadExactly(stream, dx10Header))
            {
                error = "The FourCC is DX10, but the 20-byte DDS_HEADER_DXT10 is missing.";
                return false;
            }

            dxgiFormatValue = ReadUInt32(dx10Header, 0);
            resourceDimension = ReadUInt32(dx10Header, 4);
            miscFlag = ReadUInt32(dx10Header, 8);
            arraySize = ReadUInt32(dx10Header, 12);

            if (arraySize == 0)
            {
                error = "The DDS_HEADER_DXT10 declares an array size of zero.";
                return false;
            }

            if (resourceDimension is not D3D10ResourceDimensionTexture1D
                and not D3D10ResourceDimensionTexture2D
                and not D3D10ResourceDimensionTexture3D)
            {
                error = $"The DDS_HEADER_DXT10 declares unsupported resource dimension {resourceDimension}.";
                return false;
            }

            dxgiFormat = GetDxgiFormatName(dxgiFormatValue.Value);
        }

        var headerKind = hasDx10Header ? Bw2DdsHeaderKind.Dx10Extended : Bw2DdsHeaderKind.Legacy;
        var textureKind = hasDx10Header
            ? GetDx10TextureKind(resourceDimension, miscFlag, arraySize!.Value)
            : GetLegacyTextureKind(caps2);
        int? faceCount = !hasDx10Header && (caps2 & DdsCaps2Cubemap) != 0
            ? CountCubemapFaces(caps2)
            : null;
        var pixelFormat = hasDx10Header
            ? GetDxgiPixelFormat(dxgiFormatValue!.Value)
            : GetLegacyPixelFormat(pixelFormatFlags, fourCc, rgbBitCount, alphaMask);
        var colorSpace = hasDx10Header
            ? GetDxgiColorSpace(dxgiFormatValue!.Value)
            : Bw2DdsColorSpace.Unknown;
        var alphaCapability = hasDx10Header
            ? GetDxgiAlphaCapability(dxgiFormatValue!.Value)
            : GetLegacyAlphaCapability(pixelFormatFlags, fourCc, alphaMask);
        var dataLayout = (flags & DdsdLinearSize) != 0
            ? Bw2DdsDataLayout.LinearSize
            : (flags & DdsdPitch) != 0
                ? Bw2DdsDataLayout.Pitch
                : Bw2DdsDataLayout.Unknown;

        info = new Bw2DdsInfo(
            headerKind,
            width,
            height,
            depth,
            rawMipMapCount == 0 ? 1u : rawMipMapCount,
            textureKind,
            pixelFormat,
            fourCc,
            dxgiFormat,
            dxgiFormatValue,
            arraySize,
            faceCount,
            colorSpace,
            alphaCapability,
            dataLayout,
            pitchOrLinearSize);
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

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, sizeof(uint)));

    private static string DecodeFourCc(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        foreach (var character in bytes)
        {
            if (character is < 0x20 or > 0x7E)
                return $"0x{value:X8}";
        }

        return Encoding.ASCII.GetString(bytes);
    }

    private static Bw2DdsTextureKind GetLegacyTextureKind(uint caps2)
    {
        if ((caps2 & DdsCaps2Cubemap) != 0)
            return Bw2DdsTextureKind.Cubemap;
        if ((caps2 & DdsCaps2Volume) != 0)
            return Bw2DdsTextureKind.Texture3D;
        return Bw2DdsTextureKind.Texture2D;
    }

    private static Bw2DdsTextureKind GetDx10TextureKind(uint dimension, uint miscFlag, uint arraySize)
    {
        if (dimension == D3D10ResourceDimensionTexture1D)
            return arraySize > 1 ? Bw2DdsTextureKind.TextureArray : Bw2DdsTextureKind.Texture1D;
        if (dimension == D3D10ResourceDimensionTexture3D)
            return Bw2DdsTextureKind.Texture3D;
        if ((miscFlag & D3D10ResourceMiscTextureCube) != 0)
            return arraySize > 1 ? Bw2DdsTextureKind.CubemapArray : Bw2DdsTextureKind.Cubemap;
        return arraySize > 1 ? Bw2DdsTextureKind.TextureArray : Bw2DdsTextureKind.Texture2D;
    }

    private static int CountCubemapFaces(uint caps2)
    {
        var flags = new[]
        {
            DdsCaps2CubemapPositiveX,
            DdsCaps2CubemapNegativeX,
            DdsCaps2CubemapPositiveY,
            DdsCaps2CubemapNegativeY,
            DdsCaps2CubemapPositiveZ,
            DdsCaps2CubemapNegativeZ
        };
        return flags.Count(flag => (caps2 & flag) != 0);
    }

    private static string GetLegacyPixelFormat(uint flags, string? fourCc, uint bitCount, uint alphaMask)
    {
        if ((flags & DdpfFourCc) != 0)
        {
            return fourCc switch
            {
                "DXT1" => "BC1",
                "DXT2" or "DXT3" => "BC2",
                "DXT4" or "DXT5" => "BC3",
                "ATI1" or "BC4U" or "BC4S" => "BC4",
                "ATI2" or "BC5U" or "BC5S" or "DXN " => "BC5",
                null => "Unknown FourCC format",
                _ => $"FourCC {fourCc}"
            };
        }

        if ((flags & DdpfRgb) != 0)
            return alphaMask != 0 ? $"{bitCount}-bit RGBA" : $"{bitCount}-bit RGB";
        if ((flags & DdpfLuminance) != 0)
            return alphaMask != 0 ? $"{bitCount}-bit luminance + alpha" : $"{bitCount}-bit luminance";
        if ((flags & DdpfAlpha) != 0)
            return $"{bitCount}-bit alpha";
        if ((flags & DdpfYuv) != 0)
            return $"{bitCount}-bit YUV";
        return "Unknown";
    }

    private static Bw2DdsAlphaCapability GetLegacyAlphaCapability(uint flags, string? fourCc, uint alphaMask)
    {
        if ((flags & DdpfFourCc) != 0)
        {
            return fourCc switch
            {
                "DXT1" or "DXT2" or "DXT3" or "DXT4" or "DXT5" => Bw2DdsAlphaCapability.Yes,
                "ATI1" or "BC4U" or "BC4S" or "ATI2" or "BC5U" or "BC5S" or "DXN " => Bw2DdsAlphaCapability.No,
                _ => Bw2DdsAlphaCapability.Unknown
            };
        }

        if ((flags & (DdpfAlphaPixels | DdpfAlpha)) != 0 || alphaMask != 0)
            return Bw2DdsAlphaCapability.Yes;
        if ((flags & (DdpfRgb | DdpfLuminance | DdpfYuv)) != 0)
            return Bw2DdsAlphaCapability.No;
        return Bw2DdsAlphaCapability.Unknown;
    }

    private static string GetDxgiPixelFormat(uint format) => format switch
    {
        70 or 71 or 72 => "BC1",
        73 or 74 or 75 => "BC2",
        76 or 77 or 78 => "BC3",
        79 or 80 or 81 => "BC4",
        82 or 83 or 84 => "BC5",
        94 or 95 or 96 => "BC6H",
        97 or 98 or 99 => "BC7",
        _ => GetDxgiFormatName(format)
    };

    private static string GetDxgiFormatName(uint format) => format switch
    {
        0 => "DXGI_FORMAT_UNKNOWN",
        28 => "DXGI_FORMAT_R8G8B8A8_UNORM",
        29 => "DXGI_FORMAT_R8G8B8A8_UNORM_SRGB",
        70 => "DXGI_FORMAT_BC1_TYPELESS",
        71 => "DXGI_FORMAT_BC1_UNORM",
        72 => "DXGI_FORMAT_BC1_UNORM_SRGB",
        73 => "DXGI_FORMAT_BC2_TYPELESS",
        74 => "DXGI_FORMAT_BC2_UNORM",
        75 => "DXGI_FORMAT_BC2_UNORM_SRGB",
        76 => "DXGI_FORMAT_BC3_TYPELESS",
        77 => "DXGI_FORMAT_BC3_UNORM",
        78 => "DXGI_FORMAT_BC3_UNORM_SRGB",
        79 => "DXGI_FORMAT_BC4_TYPELESS",
        80 => "DXGI_FORMAT_BC4_UNORM",
        81 => "DXGI_FORMAT_BC4_SNORM",
        82 => "DXGI_FORMAT_BC5_TYPELESS",
        83 => "DXGI_FORMAT_BC5_UNORM",
        84 => "DXGI_FORMAT_BC5_SNORM",
        87 => "DXGI_FORMAT_B8G8R8A8_UNORM",
        88 => "DXGI_FORMAT_B8G8R8X8_UNORM",
        90 => "DXGI_FORMAT_B8G8R8A8_TYPELESS",
        91 => "DXGI_FORMAT_B8G8R8A8_UNORM_SRGB",
        92 => "DXGI_FORMAT_B8G8R8X8_TYPELESS",
        93 => "DXGI_FORMAT_B8G8R8X8_UNORM_SRGB",
        94 => "DXGI_FORMAT_BC6H_TYPELESS",
        95 => "DXGI_FORMAT_BC6H_UF16",
        96 => "DXGI_FORMAT_BC6H_SF16",
        97 => "DXGI_FORMAT_BC7_TYPELESS",
        98 => "DXGI_FORMAT_BC7_UNORM",
        99 => "DXGI_FORMAT_BC7_UNORM_SRGB",
        _ => $"Unknown DXGI format ({format})"
    };

    private static Bw2DdsColorSpace GetDxgiColorSpace(uint format)
    {
        if (format is 29 or 72 or 75 or 78 or 91 or 93 or 99)
            return Bw2DdsColorSpace.Srgb;
        if (format is 28 or 71 or 74 or 77 or 87 or 88 or 98)
            return Bw2DdsColorSpace.Linear;
        return Bw2DdsColorSpace.Unknown;
    }

    private static Bw2DdsAlphaCapability GetDxgiAlphaCapability(uint format) => format switch
    {
        28 or 29 or 70 or 71 or 72 or 73 or 74 or 75 or 76 or 77 or 78 or 87 or 90 or 91 or 97 or 98 or 99 => Bw2DdsAlphaCapability.Yes,
        79 or 80 or 81 or 82 or 83 or 84 or 88 or 92 or 93 or 94 or 95 or 96 => Bw2DdsAlphaCapability.No,
        _ => Bw2DdsAlphaCapability.Unknown
    };
}
