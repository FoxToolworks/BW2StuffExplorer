using System.Buffers.Binary;
using System.Text;

namespace StuffCore;

public sealed record Bw2BwmTextureReference(
    uint MaterialIndex,
    string MaterialName,
    Bw2TextureRole Role,
    string Path);

public sealed record Bw2BwmMaterial
{
    public Bw2BwmMaterial(
        uint index,
        string storedName,
        string diffuseMap,
        string lightMap,
        string growthMap,
        string specularMap,
        string additionalMap,
        string normalMap)
    {
        ArgumentNullException.ThrowIfNull(storedName);
        ArgumentNullException.ThrowIfNull(diffuseMap);
        ArgumentNullException.ThrowIfNull(lightMap);
        ArgumentNullException.ThrowIfNull(growthMap);
        ArgumentNullException.ThrowIfNull(specularMap);
        ArgumentNullException.ThrowIfNull(additionalMap);
        ArgumentNullException.ThrowIfNull(normalMap);

        Index = index;
        StoredName = storedName;
        DiffuseMap = diffuseMap;
        LightMap = lightMap;
        GrowthMap = growthMap;
        SpecularMap = specularMap;
        AdditionalMap = additionalMap;
        NormalMap = normalMap;
        TextureReferences = CreateTextureReferences();
    }

    public uint Index { get; }
    public string StoredName { get; }
    public string DiffuseMap { get; }
    public string LightMap { get; }
    public string GrowthMap { get; }
    public string SpecularMap { get; }
    public string AdditionalMap { get; }
    public string NormalMap { get; }
    public IReadOnlyList<Bw2BwmTextureReference> TextureReferences { get; }

    private IReadOnlyList<Bw2BwmTextureReference> CreateTextureReferences()
    {
        var references = new List<Bw2BwmTextureReference>(6);
        AddReference(references, Bw2TextureRole.DiffuseMap, DiffuseMap);
        AddReference(references, Bw2TextureRole.LightMap, LightMap);
        AddReference(references, Bw2TextureRole.GrowthMap, GrowthMap);
        AddReference(references, Bw2TextureRole.SpecularMap, SpecularMap);
        AddReference(references, Bw2TextureRole.AdditionalMap, AdditionalMap);
        AddReference(references, Bw2TextureRole.NormalMap, NormalMap);
        return references.ToArray();
    }

    private void AddReference(
        ICollection<Bw2BwmTextureReference> references,
        Bw2TextureRole role,
        string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            references.Add(new Bw2BwmTextureReference(Index, StoredName, role, path));
    }
}

public sealed record Bw2BwmModelInfo
{
    public Bw2BwmModelInfo(
        uint version,
        uint modelType,
        IReadOnlyList<Bw2BwmMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(materials);
        var materialArray = materials.ToArray();

        Version = version;
        ModelType = modelType;
        Materials = materialArray;
        MaterialCount = checked((uint)materialArray.Length);
        TextureReferences = materialArray
            .SelectMany(material => material.TextureReferences)
            .ToArray();
    }

    public uint Version { get; }
    public uint ModelType { get; }
    public uint MaterialCount { get; }
    public IReadOnlyList<Bw2BwmMaterial> Materials { get; }
    public IReadOnlyList<Bw2BwmTextureReference> TextureReferences { get; }
}

public static class Bw2BwmReader
{
    public const int HeaderSize = 184;
    public const int MaterialSize = 448;
    public const int MaterialNameOffset = 384;
    public const int FixedStringSize = 64;
    public const uint Magic = 0x2B00B1E5;
    public const string Signature = "LiOnHeAdMODEL";

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

            var materials = new List<Bw2BwmMaterial>();
            var materialBytes = new byte[MaterialSize];
            for (uint materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                if (!TryReadExactly(stream, materialBytes))
                {
                    error = $"Material {materialIndex} is truncated.";
                    return false;
                }

                materials.Add(new Bw2BwmMaterial(
                    materialIndex,
                    ReadFixedAscii(materialBytes, MaterialNameOffset, FixedStringSize),
                    ReadTexturePath(materialBytes, 0),
                    ReadTexturePath(materialBytes, 64),
                    ReadTexturePath(materialBytes, 128),
                    ReadTexturePath(materialBytes, 192),
                    ReadTexturePath(materialBytes, 256),
                    ReadTexturePath(materialBytes, 320)));
            }

            model = new Bw2BwmModelInfo(version, modelType, materials);
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

    private static string ReadTexturePath(byte[] material, int offset) =>
        ReadFixedAscii(material, offset, FixedStringSize).Trim();
}
