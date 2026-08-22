using StuffCore;

namespace StuffExplorer;

internal static class AssetDisplayNames
{
    public static string GetCategory(Bw2AssetCategory category) => category switch
    {
        Bw2AssetCategory.TexturesAndImages => MainWindow.S("AssetCategoryTexturesAndImages"),
        Bw2AssetCategory.ThreeDModels => MainWindow.S("AssetCategoryThreeDModels"),
        Bw2AssetCategory.Video => MainWindow.S("AssetCategoryVideo"),
        Bw2AssetCategory.CameraData => MainWindow.S("AssetCategoryCameraData"),
        Bw2AssetCategory.CreatureData => MainWindow.S("AssetCategoryCreatureData"),
        Bw2AssetCategory.AnimationAndDialogue => MainWindow.S("AssetCategoryAnimationAndDialogue"),
        _ => MainWindow.S("AssetCategoryUnknownData")
    };

    public static string GetFileType(Bw2AssetClassification classification)
    {
        var description = classification.FileType switch
        {
            Bw2FileType.Texture => MainWindow.S("FileTypeTexture"),
            Bw2FileType.TargaImage => MainWindow.S("FileTypeTargaImage"),
            Bw2FileType.BitmapImage => MainWindow.S("FileTypeBitmapImage"),
            Bw2FileType.Rgb555SkyTexture => MainWindow.S("FileTypeRgb555SkyTexture"),
            Bw2FileType.ThreeDModel => MainWindow.S("FileTypeThreeDModel"),
            Bw2FileType.StaticModel => MainWindow.S("FileTypeStaticModel"),
            Bw2FileType.SkinnedModel => MainWindow.S("FileTypeSkinnedModel"),
            Bw2FileType.UnknownModelData => MainWindow.S("FileTypeUnknownModelData"),
            Bw2FileType.BinkVideo => MainWindow.S("FileTypeBinkVideo"),
            Bw2FileType.CameraPathData => MainWindow.S("FileTypeCameraPathData"),
            Bw2FileType.CameraExclusionZone => MainWindow.S("FileTypeCameraExclusionZone"),
            Bw2FileType.CreatureModelAndMorphData => MainWindow.S("FileTypeCreatureModelAndMorphData"),
            Bw2FileType.CreatureHairAndFurData => MainWindow.S("FileTypeCreatureHairAndFurData"),
            Bw2FileType.CreatureSupportData => MainWindow.S("FileTypeCreatureSupportData"),
            Bw2FileType.AdvisorAnimationBank => MainWindow.S("FileTypeAdvisorAnimationBank"),
            Bw2FileType.DialogueAnnotationData => MainWindow.S("FileTypeDialogueAnnotationData"),
            _ => MainWindow.S("FileTypeUnknownData")
        };

        return string.IsNullOrEmpty(classification.Format)
            ? description
            : $"{description} ({classification.Format})";
    }

    public static string GetTextureRole(Bw2TextureRole role) => role switch
    {
        Bw2TextureRole.DiffuseMap => MainWindow.S("TextureRoleDiffuseMap"),
        Bw2TextureRole.LightMap => MainWindow.S("TextureRoleLightMap"),
        Bw2TextureRole.GrowthMap => MainWindow.S("TextureRoleGrowthMap"),
        Bw2TextureRole.SpecularMap => MainWindow.S("TextureRoleSpecularMap"),
        Bw2TextureRole.AnimatedMap => MainWindow.S("TextureRoleAnimatedMap"),
        Bw2TextureRole.NormalMap => MainWindow.S("TextureRoleNormalMap"),
        Bw2TextureRole.BakedTexture => MainWindow.S("TextureRoleBakedTexture"),
        Bw2TextureRole.ScaleBiasMap => MainWindow.S("TextureRoleScaleBiasMap"),
        Bw2TextureRole.FurStrandTexture => MainWindow.S("TextureRoleFurStrandTexture"),
        _ => MainWindow.S("TextureRoleUnclassified")
    };
}
