namespace StuffCore;

public enum Bw2AssetCategory
{
    UnknownData,
    TexturesAndImages,
    ThreeDModels,
    Video,
    CameraData,
    CreatureData,
    AnimationAndDialogue
}

public enum Bw2FileType
{
    UnknownData,
    Texture,
    TargaImage,
    BitmapImage,
    Rgb555SkyTexture,
    ThreeDModel,
    StaticModel,
    SkinnedModel,
    UnknownModelData,
    BinkVideo,
    CameraPathData,
    CameraExclusionZone,
    CreatureModelAndMorphData,
    CreatureHairAndFurData,
    CreatureSupportData,
    AdvisorAnimationBank,
    DialogueAnnotationData
}

public enum Bw2TextureRole
{
    DiffuseMap,
    LightMap,
    GrowthMap,
    SpecularMap,
    AnimatedMap,
    NormalMap,
    BakedTexture,
    ScaleBiasMap,
    FurStrandTexture
}

public enum Bw2AssetContext
{
    None,
    Model,
    Landscape,
    Creature
}

public sealed record Bw2AssetClassification(
    string Format,
    Bw2AssetCategory Category,
    Bw2FileType FileType,
    Bw2AssetContext Context,
    IReadOnlyList<Bw2TextureRole> TextureRoles)
{
    public Bw2AssetClassification(
        string format,
        Bw2AssetCategory category,
        Bw2FileType fileType)
        : this(format, category, fileType, Bw2AssetContext.None, Array.Empty<Bw2TextureRole>())
    {
    }

    public Bw2AssetClassification(
        string format,
        Bw2AssetCategory category,
        Bw2FileType fileType,
        IReadOnlyList<Bw2TextureRole> textureRoles)
        : this(format, category, fileType, Bw2AssetContext.None, textureRoles)
    {
    }

    public Bw2AssetClassification(
        string format,
        Bw2AssetCategory category,
        Bw2FileType fileType,
        Bw2AssetContext context)
        : this(format, category, fileType, context, Array.Empty<Bw2TextureRole>())
    {
    }
}

public static class Bw2AssetClassifier
{
    public static Bw2AssetClassification Classify(StuffEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return ClassifyPath(entry.Path);
    }

    public static Bw2AssetClassification ClassifyPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var format = System.IO.Path.GetExtension(path)
            .TrimStart('.')
            .ToUpperInvariant();

        return format switch
        {
            "DDS" => Known(format, Bw2AssetCategory.TexturesAndImages, Bw2FileType.Texture),
            "TGA" => Known(format, Bw2AssetCategory.TexturesAndImages, Bw2FileType.TargaImage),
            "BMP" => Known(format, Bw2AssetCategory.TexturesAndImages, Bw2FileType.BitmapImage),
            "555" => Known(format, Bw2AssetCategory.TexturesAndImages, Bw2FileType.Rgb555SkyTexture),
            "BWM" => Known(format, Bw2AssetCategory.ThreeDModels, Bw2FileType.ThreeDModel),
            "BIK" => Known(format, Bw2AssetCategory.Video, Bw2FileType.BinkVideo),
            "CAM" => Known(format, Bw2AssetCategory.CameraData, Bw2FileType.CameraPathData),
            "EXC" => Known(format, Bw2AssetCategory.CameraData, Bw2FileType.CameraExclusionZone),
            "CSK" => Known(format, Bw2AssetCategory.CreatureData, Bw2FileType.CreatureModelAndMorphData),
            "CHA" => Known(format, Bw2AssetCategory.CreatureData, Bw2FileType.CreatureHairAndFurData),
            "CCS" => Known(format, Bw2AssetCategory.CreatureData, Bw2FileType.CreatureSupportData),
            "ABN" => Known(format, Bw2AssetCategory.AnimationAndDialogue, Bw2FileType.AdvisorAnimationBank),
            "DAN" => Known(format, Bw2AssetCategory.AnimationAndDialogue, Bw2FileType.DialogueAnnotationData),
            _ => new Bw2AssetClassification(format, Bw2AssetCategory.UnknownData, Bw2FileType.UnknownData)
        };
    }

    private static Bw2AssetClassification Known(
        string format,
        Bw2AssetCategory category,
        Bw2FileType fileType) => new(format, category, fileType);
}
