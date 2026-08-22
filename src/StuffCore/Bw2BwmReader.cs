using System.Buffers.Binary;
using System.Text;

namespace StuffCore;

public sealed record Bw2BwmTextureReference(
    uint MaterialIndex,
    Bw2TextureRole Role,
    string Path);

public sealed record Bw2BwmModelInfo(
    uint Version,
    uint ModelType,
    uint MaterialCount,
    IReadOnlyList<Bw2BwmTextureReference> TextureReferences);

public static class Bw2BwmReader
{
    public const int HeaderSize = 184;
    public const int MaterialSize = 448;
    public const uint Magic = 0x2B00B1E5;
    public const string Signature = "LiOnHeAdMODEL";

    private static readonly (int Offset, Bw2TextureRole Role)[] TextureSlots =
    [
        (0, Bw2TextureRole.DiffuseMap),
        (64, Bw2TextureRole.LightMap),
        (128, Bw2TextureRole.GrowthMap),
        (192, Bw2TextureRole.SpecularMap),
        (256, Bw2TextureRole.AnimatedMap),
        (320, Bw2TextureRole.NormalMap)
    ];

    public static bool TryRead(StuffArchive archive, StuffEntry entry, out Bw2BwmModelInfo? model)
        => TryRead(archive, entry, out model, out _);

    public static bool TryRead(
        StuffArchive archive,
        StuffEntry entry,
        out Bw2BwmModelInfo? model,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(entry);
        try
        {
            using var stream = archive.OpenEntry(entry);
            return TryRead(stream, entry.Length, entry.Extension, out model, out error);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or StuffArchiveException)
        {
            model = null;
            error = $"The BWM data could not be read: {exception.Message}";
            return false;
        }
    }

    public static bool TryRead(
        Stream stream,
        long length,
        string extension,
        out Bw2BwmModelInfo? model,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(extension);
        model = null;

        if (!string.Equals(extension, "BWM", StringComparison.OrdinalIgnoreCase))
        {
            error = "The asset is not a BWM file.";
            return false;
        }

        if (length < HeaderSize)
        {
            error = $"The file is shorter than the {HeaderSize}-byte BWM header.";
            return false;
        }

        try
        {
            var header = new byte[HeaderSize];
            if (!TryReadExactly(stream, header))
            {
                error = "The BWM header is truncated.";
                return false;
            }

            if (!string.Equals(ReadFixedAscii(header, 0, 40), Signature, StringComparison.Ordinal))
            {
                error = $"The expected '{Signature}' signature is missing.";
                return false;
            }

            var storedPayloadSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(40, 4));
            var magic = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(44, 4));
            var version = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(48, 4));
            var materialCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(124, 4));
            var modelType = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(176, 4));

            if (storedPayloadSize != length - 44)
            {
                error = $"The stored payload size ({storedPayloadSize:N0}) does not match the file size ({length - 44:N0}).";
                return false;
            }

            if (magic != Magic)
            {
                error = $"The BWM magic value is 0x{magic:X8}; expected 0x{Magic:X8}.";
                return false;
            }

            if (version != 5 && version != 6)
            {
                error = $"BWM version {version} is not supported; expected version 5 or 6.";
                return false;
            }

            if ((ulong)HeaderSize + ((ulong)materialCount * MaterialSize) > (ulong)length)
            {
                error = "The declared material table extends beyond the file.";
                return false;
            }

            var references = new List<Bw2BwmTextureReference>();
            var material = new byte[MaterialSize];
            for (uint materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                if (!TryReadExactly(stream, material))
                {
                    error = $"Material {materialIndex} is truncated.";
                    return false;
                }

                foreach (var slot in TextureSlots)
                {
                    var path = ReadFixedAscii(material, slot.Offset, 64).Trim();
                    if (!string.IsNullOrWhiteSpace(path))
                        references.Add(new Bw2BwmTextureReference(materialIndex, slot.Role, path));
                }
            }

            model = new Bw2BwmModelInfo(version, modelType, materialCount, references);
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            error = $"The BWM data could not be read: {exception.Message}";
            return false;
        }
    }

    private static bool TryReadExactly(Stream stream, byte[] buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0)
                return false;
            totalRead += read;
        }

        return true;
    }

    private static string ReadFixedAscii(byte[] bytes, int offset, int length)
    {
        var end = offset;
        var limit = offset + length;
        while (end < limit && bytes[end] != 0)
            end++;

        return end == offset ? string.Empty : Encoding.ASCII.GetString(bytes, offset, end - offset);
    }
}
