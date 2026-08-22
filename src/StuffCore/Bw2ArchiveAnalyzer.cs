namespace StuffCore;

public enum Bw2ReferenceResolutionStatus
{
    ResolvedExactPath,
    ResolvedUniqueFileName,
    Missing,
    Ambiguous
}

public sealed record Bw2TextureRelationship(
    StuffEntry ModelEntry,
    uint MaterialIndex,
    Bw2TextureRole Role,
    string ReferencePath,
    Bw2ReferenceResolutionStatus ResolutionStatus,
    StuffEntry? TextureEntry,
    IReadOnlyList<StuffEntry> Candidates);

public sealed class Bw2ArchiveAnalysis
{
    internal Bw2ArchiveAnalysis(
        IReadOnlyDictionary<StuffEntry, Bw2AssetClassification> classifications,
        IReadOnlyDictionary<StuffEntry, Bw2BwmModelInfo> bwmModels,
        IReadOnlyList<Bw2TextureRelationship> relationships)
    {
        Classifications = classifications;
        BwmModels = bwmModels;
        Relationships = relationships;
    }

    public IReadOnlyDictionary<StuffEntry, Bw2AssetClassification> Classifications { get; }
    public IReadOnlyDictionary<StuffEntry, Bw2BwmModelInfo> BwmModels { get; }
    public IReadOnlyList<Bw2TextureRelationship> Relationships { get; }

    public Bw2AssetClassification GetClassification(StuffEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Classifications.TryGetValue(entry, out var classification)
            ? classification
            : Bw2AssetClassifier.Classify(entry);
    }

    public IReadOnlyList<Bw2TextureRelationship> GetRelationshipsFromModel(StuffEntry modelEntry)
    {
        ArgumentNullException.ThrowIfNull(modelEntry);
        return Relationships
            .Where(relationship => relationship.ModelEntry == modelEntry)
            .ToArray();
    }

    public IReadOnlyList<Bw2TextureRelationship> GetRelationshipsForTexture(StuffEntry textureEntry)
    {
        ArgumentNullException.ThrowIfNull(textureEntry);
        return Relationships
            .Where(relationship => relationship.TextureEntry == textureEntry
                || (relationship.ResolutionStatus == Bw2ReferenceResolutionStatus.Ambiguous
                    && relationship.Candidates.Contains(textureEntry)))
            .ToArray();
    }
}

public static class Bw2ArchiveAnalyzer
{
    public static Bw2ArchiveAnalysis Analyze(StuffArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var classifications = new Dictionary<StuffEntry, Bw2AssetClassification>();
        foreach (var entry in archive.Entries)
            classifications[entry] = Bw2AssetClassifier.Classify(entry);
        var bwmModels = new Dictionary<StuffEntry, Bw2BwmModelInfo>();
        var relationships = new List<Bw2TextureRelationship>();
        var rolesByTexture = new Dictionary<StuffEntry, HashSet<Bw2TextureRole>>();

        var ddsEntries = archive.Entries
            .Where(entry => string.Equals(entry.Extension, "DDS", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var pathIndex = BuildIndex(ddsEntries, entry => NormalizeReference(entry.Path));
        var fileNameIndex = BuildIndex(ddsEntries, entry => entry.Name.ToLowerInvariant());

        foreach (var entry in archive.Entries.Where(
                     entry => string.Equals(entry.Extension, "BWM", StringComparison.OrdinalIgnoreCase)))
        {
            if (!Bw2BwmReader.TryRead(archive, entry, out var model) || model is null)
            {
                classifications[entry] = new Bw2AssetClassification(
                    "BWM",
                    Bw2AssetCategory.ThreeDModels,
                    Bw2FileType.UnknownModelData);
                continue;
            }

            bwmModels[entry] = model;
            classifications[entry] = new Bw2AssetClassification(
                "BWM",
                Bw2AssetCategory.ThreeDModels,
                model.ModelType switch
                {
                    2 => Bw2FileType.StaticModel,
                    3 => Bw2FileType.SkinnedModel,
                    _ => Bw2FileType.UnknownModelData
                });

            foreach (var reference in model.TextureReferences)
            {
                var relationship = ResolveRelationship(
                    entry,
                    reference,
                    pathIndex,
                    fileNameIndex);
                relationships.Add(relationship);

                if (relationship.TextureEntry is not { } textureEntry)
                    continue;

                if (!rolesByTexture.TryGetValue(textureEntry, out var roles))
                {
                    roles = [];
                    rolesByTexture.Add(textureEntry, roles);
                }
                roles.Add(reference.Role);
            }
        }

        foreach (var textureEntry in ddsEntries)
        {
            var context = GetContext(textureEntry);
            Bw2TextureRole[] roles;

            if (rolesByTexture.TryGetValue(textureEntry, out var roleSet))
            {
                roles = roleSet.OrderBy(role => role).ToArray();
                if (context == Bw2AssetContext.None)
                    context = Bw2AssetContext.Model;
            }
            else if (TryGetFamilyRole(textureEntry, context, out var familyRole))
            {
                roles = [familyRole];
            }
            else
            {
                roles = [];
            }

            classifications[textureEntry] = new Bw2AssetClassification(
                "DDS",
                Bw2AssetCategory.TexturesAndImages,
                Bw2FileType.Texture,
                context,
                roles);
        }

        return new Bw2ArchiveAnalysis(classifications, bwmModels, relationships);
    }

    private static Dictionary<string, List<StuffEntry>> BuildIndex(
        IEnumerable<StuffEntry> entries,
        Func<StuffEntry, string> keySelector)
    {
        var index = new Dictionary<string, List<StuffEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var key = keySelector(entry);
            if (!index.TryGetValue(key, out var matches))
            {
                matches = [];
                index.Add(key, matches);
            }
            matches.Add(entry);
        }

        return index;
    }

    private static Bw2TextureRelationship ResolveRelationship(
        StuffEntry modelEntry,
        Bw2BwmTextureReference reference,
        IReadOnlyDictionary<string, List<StuffEntry>> pathIndex,
        IReadOnlyDictionary<string, List<StuffEntry>> fileNameIndex)
    {
        var normalized = NormalizeReference(reference.Path);
        if (string.IsNullOrEmpty(normalized))
            return Missing(modelEntry, reference);

        var pathCandidates = normalized.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
            ? new[] { normalized }
            : new[] { normalized, $"data/{normalized}" };

        foreach (var candidatePath in pathCandidates)
        {
            if (!pathIndex.TryGetValue(candidatePath, out var exactMatches))
                continue;

            return exactMatches.Count == 1
                ? Resolved(modelEntry, reference, Bw2ReferenceResolutionStatus.ResolvedExactPath, exactMatches[0])
                : Ambiguous(modelEntry, reference, exactMatches);
        }

        var lastSeparator = normalized.LastIndexOf('/');
        var fileName = lastSeparator < 0 ? normalized : normalized[(lastSeparator + 1)..];
        if (!string.IsNullOrEmpty(fileName) && fileNameIndex.TryGetValue(fileName, out var fileNameMatches))
        {
            return fileNameMatches.Count == 1
                ? Resolved(modelEntry, reference, Bw2ReferenceResolutionStatus.ResolvedUniqueFileName, fileNameMatches[0])
                : Ambiguous(modelEntry, reference, fileNameMatches);
        }

        return Missing(modelEntry, reference);
    }

    private static Bw2TextureRelationship Resolved(
        StuffEntry modelEntry,
        Bw2BwmTextureReference reference,
        Bw2ReferenceResolutionStatus status,
        StuffEntry textureEntry) => new(
            modelEntry,
            reference.MaterialIndex,
            reference.Role,
            reference.Path,
            status,
            textureEntry,
            new[] { textureEntry });

    private static Bw2TextureRelationship Missing(
        StuffEntry modelEntry,
        Bw2BwmTextureReference reference) => new(
            modelEntry,
            reference.MaterialIndex,
            reference.Role,
            reference.Path,
            Bw2ReferenceResolutionStatus.Missing,
            null,
            Array.Empty<StuffEntry>());

    private static Bw2TextureRelationship Ambiguous(
        StuffEntry modelEntry,
        Bw2BwmTextureReference reference,
        IReadOnlyList<StuffEntry> candidates) => new(
            modelEntry,
            reference.MaterialIndex,
            reference.Role,
            reference.Path,
            Bw2ReferenceResolutionStatus.Ambiguous,
            null,
            candidates.ToArray());

    private static string NormalizeReference(string reference)
    {
        var normalized = reference.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];
        return normalized.TrimStart('/').ToLowerInvariant();
    }

    private static Bw2AssetContext GetContext(StuffEntry entry)
    {
        var path = NormalizeReference(entry.Path);
        if (path.StartsWith("data/landscape/", StringComparison.OrdinalIgnoreCase))
            return Bw2AssetContext.Landscape;
        if (path.StartsWith("data/ctr/", StringComparison.OrdinalIgnoreCase))
            return Bw2AssetContext.Creature;
        return Bw2AssetContext.None;
    }

    private static bool TryGetFamilyRole(
        StuffEntry entry,
        Bw2AssetContext context,
        out Bw2TextureRole role)
    {
        var name = entry.Name;
        if (context == Bw2AssetContext.Landscape)
        {
            if (name.EndsWith("_dif.dds", StringComparison.OrdinalIgnoreCase))
            {
                role = Bw2TextureRole.DiffuseMap;
                return true;
            }
            if (name.EndsWith("_baked.dds", StringComparison.OrdinalIgnoreCase))
            {
                role = Bw2TextureRole.BakedTexture;
                return true;
            }
            if (name.EndsWith("_nrm.dds", StringComparison.OrdinalIgnoreCase))
            {
                role = Bw2TextureRole.NormalMap;
                return true;
            }
        }
        else if (context == Bw2AssetContext.Creature)
        {
            if (name.EndsWith("_d.dds", StringComparison.OrdinalIgnoreCase))
            {
                role = Bw2TextureRole.DiffuseMap;
                return true;
            }
            if (name.EndsWith("_b.dds", StringComparison.OrdinalIgnoreCase))
            {
                role = Bw2TextureRole.NormalMap;
                return true;
            }
            if (name.EndsWith("_s.dds", StringComparison.OrdinalIgnoreCase))
            {
                role = Bw2TextureRole.SpecularMap;
                return true;
            }
            if (name.EndsWith("scalebias.dds", StringComparison.OrdinalIgnoreCase))
            {
                role = Bw2TextureRole.ScaleBiasMap;
                return true;
            }
            if (name.Equals("t_strand.dds", StringComparison.OrdinalIgnoreCase))
            {
                role = Bw2TextureRole.FurStrandTexture;
                return true;
            }
        }

        role = default;
        return false;
    }

}
