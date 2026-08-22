using System.Buffers.Binary;
using System.Text;
using StuffCore;

var tempRoot = Path.Combine(Path.GetTempPath(), $"stuffcore-test-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempRoot);

try
{
    TestValidArchive(tempRoot);
    TestBw2AssetClassification();
    TestBw2ArchiveAnalysis(tempRoot);
    TestBw2DdsReader();
    TestBw2DdsAlphaAnalyzer();
    TestAssetInspectionProviders(tempRoot);
    TestMalformedArchives(tempRoot);
    TestUnsafeAndDuplicatePaths(tempRoot);
    TestCancellationCleanup(tempRoot);
    TestTruncatedSourceCleanup(tempRoot);

    Console.WriteLine("StuffCore self-test passed.");
    return 0;
}
finally
{
    Directory.Delete(tempRoot, recursive: true);
}

static void TestBw2AssetClassification()
{
    var cases = new[]
    {
        ("data/texture.dds", "DDS", Bw2AssetCategory.TexturesAndImages, Bw2FileType.Texture),
        ("data/image.tga", "TGA", Bw2AssetCategory.TexturesAndImages, Bw2FileType.TargaImage),
        ("data/image.bmp", "BMP", Bw2AssetCategory.TexturesAndImages, Bw2FileType.BitmapImage),
        ("data/sky.555", "555", Bw2AssetCategory.TexturesAndImages, Bw2FileType.Rgb555SkyTexture),
        ("data/model.bwm", "BWM", Bw2AssetCategory.ThreeDModels, Bw2FileType.ThreeDModel),
        ("data/movie.bik", "BIK", Bw2AssetCategory.Video, Bw2FileType.BinkVideo),
        ("data/path.cam", "CAM", Bw2AssetCategory.CameraData, Bw2FileType.CameraPathData),
        ("data/zone.exc", "EXC", Bw2AssetCategory.CameraData, Bw2FileType.CameraExclusionZone),
        ("data/creature.csk", "CSK", Bw2AssetCategory.CreatureData, Bw2FileType.CreatureModelAndMorphData),
        ("data/hair.cha", "CHA", Bw2AssetCategory.CreatureData, Bw2FileType.CreatureHairAndFurData),
        ("data/support.ccs", "CCS", Bw2AssetCategory.CreatureData, Bw2FileType.CreatureSupportData),
        ("data/advisor.abn", "ABN", Bw2AssetCategory.AnimationAndDialogue, Bw2FileType.AdvisorAnimationBank),
        ("data/dialogue.dan", "DAN", Bw2AssetCategory.AnimationAndDialogue, Bw2FileType.DialogueAnnotationData)
    };

    foreach (var testCase in cases)
    {
        var classification = Bw2AssetClassifier.ClassifyPath(testCase.Item1);
        Assert(classification.Format == testCase.Item2, $"{testCase.Item2} format classification");
        Assert(classification.Category == testCase.Item3, $"{testCase.Item2} category classification");
        Assert(classification.FileType == testCase.Item4, $"{testCase.Item2} file-type classification");
    }

    var mixedCase = Bw2AssetClassifier.ClassifyPath("data/MODEL.BwM");
    Assert(mixedCase.Format == "BWM" && mixedCase.FileType == Bw2FileType.ThreeDModel, "case-insensitive format classification");

    var unknown = Bw2AssetClassifier.ClassifyPath("data/value.xyz");
    Assert(unknown.Format == "XYZ", "unknown format preservation");
    Assert(unknown.Category == Bw2AssetCategory.UnknownData, "unknown category fallback");
    Assert(unknown.FileType == Bw2FileType.UnknownData, "unknown file-type fallback");

    var extensionless = Bw2AssetClassifier.ClassifyPath("data/readme");
    Assert(extensionless.Format == string.Empty, "extensionless format");
    Assert(extensionless.Category == Bw2AssetCategory.UnknownData, "extensionless category fallback");
    Assert(extensionless.FileType == Bw2FileType.UnknownData, "extensionless file-type fallback");
}

static void TestBw2ArchiveAnalysis(string tempRoot)
{
    var archivePath = Path.Combine(tempRoot, "classification.stuff");
    var staticModel = CreateBwm(
        version: 6,
        modelType: 2,
        references:
        [
            (Bw2TextureRole.DiffuseMap, "shared.dds"),
            (Bw2TextureRole.LightMap, "data/textures/light.dds"),
            (Bw2TextureRole.GrowthMap, "missing.dds"),
            (Bw2TextureRole.SpecularMap, "shared.dds"),
            (Bw2TextureRole.AnimatedMap, "special_d.dds"),
            (Bw2TextureRole.NormalMap, "norm.dds")
        ]);
    var skinnedModel = CreateBwm(
        version: 5,
        modelType: 3,
        references: [(Bw2TextureRole.DiffuseMap, "unique.dds")]);
    var ambiguousModel = CreateBwm(
        version: 6,
        modelType: 2,
        references: [(Bw2TextureRole.DiffuseMap, "mesh.dds")]);
    var multiMaterialModel = CreateIndexedBwm(
        version: 6,
        modelType: 2,
        materialCount: 2,
        references: [(1u, Bw2TextureRole.DiffuseMap, "unique.dds")]);

    CreateArchiveWithContents(
        archivePath,
        [
            ("data/models/static.bwm", staticModel),
            ("data/models/skinned.bwm", skinnedModel),
            ("data/models/ambiguous.bwm", ambiguousModel),
            ("data/models/multi-material.bwm", multiMaterialModel),
            ("data/models/invalid.bwm", new byte[256]),
            ("data/textures/shared.dds", new byte[] { 1 }),
            ("data/textures/light.dds", new byte[] { 2 }),
            ("data/ctr/test/special_d.dds", new byte[] { 3 }),
            ("data/textures/norm.dds", new byte[] { 4 }),
            ("data/textures/unique.dds", new byte[] { 5 }),
            ("data/textures/a/mesh.dds", new byte[] { 6 }),
            ("data/textures/b/mesh.dds", new byte[] { 7 }),
            ("data/textures/generic.dds", new byte[] { 8 }),
            ("data/landscape/test/ground_dif.dds", new byte[] { 9 }),
            ("data/landscape/test/ground_baked.dds", new byte[] { 10 }),
            ("data/landscape/test/ground_nrm.dds", new byte[] { 11 }),
            ("data/landscape/test/cracks.dds", new byte[] { 12 }),
            ("data/ctr/test/ape_d.dds", new byte[] { 13 }),
            ("data/ctr/test/ape_b.dds", new byte[] { 14 }),
            ("data/ctr/test/ape_s.dds", new byte[] { 15 }),
            ("data/ctr/test/ape_scalebias.dds", new byte[] { 16 }),
            ("data/ctr/test/t_strand.dds", new byte[] { 17 }),
            ("data/ctr/test/normalize.dds", new byte[] { 18 })
        ]);

    var archive = StuffArchive.Open(archivePath);
    var analysis = Bw2ArchiveAnalyzer.Analyze(archive);
    StuffEntry Entry(string path) => archive.Entries.Single(
        entry => entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
    Bw2AssetClassification Classification(string path) => analysis.GetClassification(Entry(path));

    Assert(Classification("data/models/static.bwm").FileType == Bw2FileType.StaticModel, "static BWM detection");
    Assert(Classification("data/models/skinned.bwm").FileType == Bw2FileType.SkinnedModel, "skinned BWM detection");
    Assert(Classification("data/models/invalid.bwm").FileType == Bw2FileType.UnknownModelData, "invalid BWM fallback");
    Assert(analysis.BwmModels[Entry("data/models/skinned.bwm")].Version == 5, "BWM version 5 support");
    Assert(
        analysis.BwmModels[Entry("data/models/static.bwm")].TextureReferences.All(reference => reference.MaterialIndex == 0),
        "BWM material-index preservation");
    Assert(
        analysis.BwmModels[Entry("data/models/multi-material.bwm")].TextureReferences.Single().MaterialIndex == 1,
        "non-zero BWM material-index preservation");
    Assert(
        !Bw2BwmReader.TryRead(archive, Entry("data/models/invalid.bwm"), out _, out var invalidBwmError)
            && !string.IsNullOrWhiteSpace(invalidBwmError),
        "invalid BWM reason preservation");

    Assert(
        archive.Entries
            .Where(entry => entry.Extension.Equals("DDS", StringComparison.OrdinalIgnoreCase))
            .All(entry => analysis.GetClassification(entry).FileType == Bw2FileType.Texture),
        "every DDS keeps the generic texture file type");

    var shared = Classification("data/textures/shared.dds");
    Assert(
        shared.TextureRoles.SequenceEqual([Bw2TextureRole.DiffuseMap, Bw2TextureRole.SpecularMap]),
        "multi-role DDS role preservation");
    Assert(shared.Context == Bw2AssetContext.Model, "BWM-referenced DDS model context");
    Assert(Classification("data/textures/light.dds").TextureRoles.SequenceEqual([Bw2TextureRole.LightMap]), "light-map DDS role");
    Assert(Classification("data/ctr/test/special_d.dds").TextureRoles.SequenceEqual([Bw2TextureRole.AnimatedMap]), "BWM role has priority over family suffix");
    Assert(Classification("data/ctr/test/special_d.dds").Context == Bw2AssetContext.Creature, "BWM-referenced creature DDS context");
    Assert(Classification("data/textures/norm.dds").TextureRoles.SequenceEqual([Bw2TextureRole.NormalMap]), "normal-map DDS role");
    Assert(Classification("data/textures/unique.dds").TextureRoles.SequenceEqual([Bw2TextureRole.DiffuseMap]), "unique filename DDS resolution");
    Assert(Classification("data/textures/a/mesh.dds").TextureRoles.Count == 0, "ambiguous DDS remains unclassified");
    Assert(Classification("data/textures/b/mesh.dds").TextureRoles.Count == 0, "second ambiguous DDS remains unclassified");
    Assert(Classification("data/textures/generic.dds").TextureRoles.Count == 0, "unreferenced DDS remains unclassified");

    Assert(Classification("data/landscape/test/ground_dif.dds").TextureRoles.SequenceEqual([Bw2TextureRole.DiffuseMap]), "landscape diffuse suffix");
    Assert(Classification("data/landscape/test/ground_baked.dds").TextureRoles.SequenceEqual([Bw2TextureRole.BakedTexture]), "landscape baked suffix");
    Assert(Classification("data/landscape/test/ground_nrm.dds").TextureRoles.SequenceEqual([Bw2TextureRole.NormalMap]), "landscape normal suffix");
    Assert(Classification("data/landscape/test/cracks.dds").TextureRoles.Count == 0, "unmatched landscape DDS remains unclassified");
    Assert(Classification("data/landscape/test/cracks.dds").Context == Bw2AssetContext.Landscape, "landscape context preservation");
    Assert(Classification("data/ctr/test/ape_d.dds").TextureRoles.SequenceEqual([Bw2TextureRole.DiffuseMap]), "creature diffuse suffix");
    Assert(Classification("data/ctr/test/ape_b.dds").TextureRoles.SequenceEqual([Bw2TextureRole.NormalMap]), "creature bump/normal suffix");
    Assert(Classification("data/ctr/test/ape_s.dds").TextureRoles.SequenceEqual([Bw2TextureRole.SpecularMap]), "creature specular suffix");
    Assert(Classification("data/ctr/test/ape_scalebias.dds").TextureRoles.SequenceEqual([Bw2TextureRole.ScaleBiasMap]), "creature scale/bias suffix");
    Assert(Classification("data/ctr/test/t_strand.dds").TextureRoles.SequenceEqual([Bw2TextureRole.FurStrandTexture]), "creature strand filename");
    Assert(Classification("data/ctr/test/normalize.dds").TextureRoles.Count == 0, "unproven creature DDS remains unclassified");
    Assert(Classification("data/ctr/test/normalize.dds").Context == Bw2AssetContext.Creature, "creature context preservation");

    Assert(analysis.Relationships.Count == 9, "BWM texture relationship count");
    Assert(analysis.Relationships.Count(item => item.ResolutionStatus == Bw2ReferenceResolutionStatus.ResolvedExactPath) == 1, "exact path resolution");
    Assert(analysis.Relationships.Count(item => item.ResolutionStatus == Bw2ReferenceResolutionStatus.ResolvedUniqueFileName) == 6, "unique filename resolution");
    Assert(analysis.Relationships.Count(item => item.ResolutionStatus == Bw2ReferenceResolutionStatus.Missing) == 1, "missing reference preservation");
    Assert(analysis.Relationships.Count(item => item.ResolutionStatus == Bw2ReferenceResolutionStatus.Ambiguous) == 1, "ambiguous reference preservation");
    Assert(analysis.Relationships.Single(item => item.ResolutionStatus == Bw2ReferenceResolutionStatus.Ambiguous).Candidates.Count == 2, "ambiguous candidates preservation");
    Assert(
        analysis.GetRelationshipsFromModel(Entry("data/models/static.bwm")).Count == 6,
        "forward BWM relationship query");
    Assert(
        analysis.GetRelationshipsForTexture(Entry("data/textures/shared.dds")).Count == 2,
        "reverse DDS relationship query with multiple roles");
    Assert(
        analysis.GetRelationshipsForTexture(Entry("data/textures/a/mesh.dds")).Single().ResolutionStatus ==
        Bw2ReferenceResolutionStatus.Ambiguous,
        "reverse DDS query includes ambiguous candidates");
    Assert(
        analysis.GetRelationshipsForTexture(Entry("data/textures/generic.dds")).Count == 0,
        "reverse DDS query leaves unrelated textures empty");
    Assert(
        analysis.GetRelationshipsFromModel(Entry("data/models/multi-material.bwm")).Single().MaterialIndex == 1,
        "forward relationship query retains material index");

    var inspectionService = new Bw2AssetInspectionService();
    var modelInspection = inspectionService.Inspect(Bw2AssetInspectionContext.FromArchive(
        archive,
        Entry("data/models/static.bwm"),
        analysis));
    Assert(modelInspection.ProviderId == "bwm", "BWM metadata provider selection");
    Assert(modelInspection.ReferenceView == Bw2AssetReferenceView.ModelToTexture, "BWM forward reference view");
    Assert(modelInspection.References.Count == 6, "BWM inspection relationship preservation");

    var textureInspection = inspectionService.Inspect(Bw2AssetInspectionContext.FromArchive(
        archive,
        Entry("data/textures/shared.dds"),
        analysis));
    Assert(textureInspection.ProviderId == "dds", "DDS metadata provider selection in archive context");
    Assert(textureInspection.ReferenceView == Bw2AssetReferenceView.TextureToModel, "DDS reverse reference view");
    Assert(textureInspection.References.Count == 2, "DDS inspection relationship preservation");
}

static void TestBw2DdsReader()
{
    using (var stream = new MemoryStream(CreateLegacyDds(
               fourCc: "DXT5",
               width: 1024,
               height: 512,
               mipLevels: 11,
               linearSize: 262_144)))
    {
        Assert(Bw2DdsReader.TryRead(stream, out var info, out var error), $"legacy DDS parsing: {error}");
        Assert(info is not null, "legacy DDS metadata exists");
        Assert(info!.HeaderKind == Bw2DdsHeaderKind.Legacy, "legacy DDS header kind");
        Assert(info.Width == 1024 && info.Height == 512, "legacy DDS dimensions");
        Assert(info.MipLevelCount == 11, "legacy DDS mip levels");
        Assert(info.PixelFormat == "BC3" && info.FourCc == "DXT5", "legacy DDS compression");
        Assert(info.ColorSpace == Bw2DdsColorSpace.Unknown, "legacy DDS color space remains unknown");
        Assert(info.AlphaCapability == Bw2DdsAlphaCapability.Yes, "DXT5 alpha capability");
        Assert(info.DataLayout == Bw2DdsDataLayout.LinearSize && info.PitchOrLinearSize == 262_144, "legacy DDS linear size");
    }

    using (var stream = new MemoryStream(CreateLegacyDds(
               fourCc: "DXT1",
               width: 256,
               height: 256,
               mipLevels: 0,
               linearSize: 32_768,
               caps2: 0x0000FE00)))
    {
        Assert(Bw2DdsReader.TryRead(stream, out var info, out _), "legacy cubemap parsing");
        Assert(info!.TextureKind == Bw2DdsTextureKind.Cubemap, "legacy cubemap detection");
        Assert(info.DeclaredCubemapFaceCount == 6, "legacy cubemap face flags");
        Assert(info.MipLevelCount == 1, "zero DDS mip count represents the base level");
    }

    using (var stream = new MemoryStream(CreateDx10Dds(
               dxgiFormat: 72,
               width: 128,
               height: 128,
               mipLevels: 8,
               resourceDimension: 3,
               miscFlag: 4,
               arraySize: 1)))
    {
        Assert(Bw2DdsReader.TryRead(stream, out var info, out var error), $"DX10 DDS parsing: {error}");
        Assert(info!.HeaderKind == Bw2DdsHeaderKind.Dx10Extended, "DX10 DDS header kind");
        Assert(info.TextureKind == Bw2DdsTextureKind.Cubemap, "DX10 cubemap detection");
        Assert(info.DxgiFormat == "DXGI_FORMAT_BC1_UNORM_SRGB", "DXGI format name");
        Assert(info.ColorSpace == Bw2DdsColorSpace.Srgb, "explicit DXGI sRGB detection");
        Assert(info.AlphaCapability == Bw2DdsAlphaCapability.Yes, "BC1 alpha capability");
    }

    var invalidMagic = CreateLegacyDds("DXT1", 64, 64, 1, 2_048);
    invalidMagic[0] = 0;
    using (var stream = new MemoryStream(invalidMagic))
        Assert(!Bw2DdsReader.TryRead(stream, out _, out _), "invalid DDS magic rejection");

    using (var stream = new MemoryStream(new byte[127]))
        Assert(!Bw2DdsReader.TryRead(stream, out _, out _), "truncated DDS header rejection");

    var missingDx10Extension = CreateLegacyDds("DX10", 64, 64, 1, 4_096);
    using (var stream = new MemoryStream(missingDx10Extension))
        Assert(!Bw2DdsReader.TryRead(stream, out _, out _), "missing DX10 extension rejection");
}

static void TestBw2DdsAlphaAnalyzer()
{
    var bc1Opaque = CreateBc1Block(color0: 0xFFFF, color1: 0x0000, selectors: uint.MaxValue);
    Assert(
        AnalyzeDdsAlpha(Combine(CreateLegacyDds("DXT1", 4, 4, 1, 8), bc1Opaque)) ==
        Bw2DdsNonOpaqueAlphaStatus.No,
        "BC1 opaque data detection");

    var bc1Transparent = CreateBc1Block(color0: 0x0000, color1: 0xFFFF, selectors: 0x00000003);
    Assert(
        AnalyzeDdsAlpha(Combine(CreateLegacyDds("DXT1", 4, 4, 1, 8), bc1Transparent)) ==
        Bw2DdsNonOpaqueAlphaStatus.Yes,
        "BC1 transparent selector detection");

    var bc1PaddingOnly = CreateBc1Block(color0: 0x0000, color1: 0xFFFF, selectors: 0x0000000C);
    Assert(
        AnalyzeDdsAlpha(Combine(CreateLegacyDds("DXT1", 1, 1, 1, 8), bc1PaddingOnly)) ==
        Bw2DdsNonOpaqueAlphaStatus.No,
        "BC1 padding texels are ignored");

    Assert(
        AnalyzeDdsAlpha(Combine(
            CreateLegacyDds("DXT1", 8, 8, 2, 32),
            bc1Opaque,
            bc1Opaque,
            bc1Opaque,
            bc1Opaque,
            bc1Transparent)) == Bw2DdsNonOpaqueAlphaStatus.Yes,
        "BC1 non-opaque alpha in a lower mip level");

    var bc2Opaque = CreateBc2Block(ulong.MaxValue);
    Assert(
        AnalyzeDdsAlpha(Combine(CreateLegacyDds("DXT3", 4, 4, 1, 16), bc2Opaque)) ==
        Bw2DdsNonOpaqueAlphaStatus.No,
        "BC2 opaque alpha detection");

    var bc2NonOpaque = CreateBc2Block(ulong.MaxValue & ~0x0FUL);
    Assert(
        AnalyzeDdsAlpha(Combine(CreateLegacyDds("DXT3", 4, 4, 1, 16), bc2NonOpaque)) ==
        Bw2DdsNonOpaqueAlphaStatus.Yes,
        "BC2 explicit non-opaque alpha detection");

    var bc3Opaque = CreateBc3Block(alpha0: 255, alpha1: 0, selectors: 0);
    Assert(
        AnalyzeDdsAlpha(Combine(CreateLegacyDds("DXT5", 4, 4, 1, 16), bc3Opaque)) ==
        Bw2DdsNonOpaqueAlphaStatus.No,
        "BC3 opaque alpha detection");

    var bc3NonOpaque = CreateBc3Block(alpha0: 255, alpha1: 0, selectors: 1);
    Assert(
        AnalyzeDdsAlpha(Combine(CreateLegacyDds("DXT5", 4, 4, 1, 16), bc3NonOpaque)) ==
        Bw2DdsNonOpaqueAlphaStatus.Yes,
        "BC3 interpolated alpha selector detection");

    Assert(
        AnalyzeDdsAlpha(Combine(
            CreateDx10Dds(78, 4, 4, 1, 3, 0, 1),
            bc3Opaque)) == Bw2DdsNonOpaqueAlphaStatus.No,
        "DX10 BC3 payload offset and format detection");

    Assert(
        AnalyzeDdsAlpha(Combine(
            CreateLegacyDds("DXT1", 4, 4, 1, 8, caps2: 0x0000FE00),
            bc1Opaque,
            bc1Opaque,
            bc1Opaque,
            bc1Opaque,
            bc1Opaque,
            bc1Transparent)) == Bw2DdsNonOpaqueAlphaStatus.Yes,
        "legacy cubemap face traversal");

    Assert(
        AnalyzeDdsAlpha(Combine(
            CreateDx10Dds(74, 4, 4, 1, 3, 0, 2),
            bc2Opaque,
            bc2NonOpaque)) == Bw2DdsNonOpaqueAlphaStatus.Yes,
        "DX10 texture-array traversal");

    Assert(
        AnalyzeDdsAlpha(Combine(
            CreateLegacyDds("DXT5", 4, 4, 1, 16, caps2: 0x00200000, depth: 2),
            bc3Opaque,
            bc3NonOpaque)) == Bw2DdsNonOpaqueAlphaStatus.Yes,
        "legacy volume-slice traversal");

    Assert(
        AnalyzeDdsAlpha(CreateLegacyDds("ATI2", 4, 4, 1, 16)) ==
        Bw2DdsNonOpaqueAlphaStatus.NotApplicable,
        "no-alpha block format is not applicable");

    Assert(
        AnalyzeDdsAlpha(CreateDx10Dds(98, 4, 4, 1, 3, 0, 1)) ==
        Bw2DdsNonOpaqueAlphaStatus.Unknown,
        "unsupported alpha-capable format remains unknown");

    Assert(
        AnalyzeDdsAlpha(Combine(CreateLegacyDds("DXT1", 4, 4, 1, 8), new byte[7])) ==
        Bw2DdsNonOpaqueAlphaStatus.Unknown,
        "truncated alpha payload remains unknown");
}

static Bw2DdsNonOpaqueAlphaStatus AnalyzeDdsAlpha(byte[] bytes)
{
    using var stream = new MemoryStream(bytes);
    Assert(Bw2DdsReader.TryRead(stream, out var info, out var error), $"alpha test DDS parsing: {error}");
    Assert(info is not null, "alpha test DDS metadata exists");
    return Bw2DdsAlphaAnalyzer.Analyze(stream, info!);
}

static void TestAssetInspectionProviders(string tempRoot)
{
    var service = new Bw2AssetInspectionService();

    var ddsPath = Path.Combine(tempRoot, "loose-texture.dds");
    File.WriteAllBytes(ddsPath, Combine(
        CreateLegacyDds("DXT1", 4, 4, 1, 8),
        CreateBc1Block(color0: 0xFFFF, color1: 0x0000, selectors: uint.MaxValue)));
    var ddsSource = new LooseFileAssetSource(ddsPath, "data/root/loose-texture.dds");
    var ddsInspection = service.Inspect(Bw2AssetInspectionContext.FromSource(ddsSource));
    Assert(ddsInspection.ProviderId == "dds", "loose DDS provider selection");
    Assert(ddsInspection.Status == Bw2AssetInspectionStatus.Valid, "loose DDS valid inspection state");
    Assert(
        ddsInspection.Details.Any(detail =>
            detail.Kind == Bw2AssetDetailKind.NonOpaqueAlpha
            && detail.Value is Bw2DdsNonOpaqueAlphaStatus.No),
        "loose DDS alpha metadata");
    Assert(ddsInspection.References.Count == 0, "loose DDS has optional empty relationship context");
    Assert(!ddsInspection.HasPreview, "DDS preview remains unavailable in stage 3.3");

    var bwmPath = Path.Combine(tempRoot, "loose-model.bwm");
    File.WriteAllBytes(bwmPath, CreateBwm(
        version: 6,
        modelType: 2,
        references: [(Bw2TextureRole.DiffuseMap, "loose-texture.dds")]));
    var bwmInspection = service.Inspect(Bw2AssetInspectionContext.FromSource(
        new LooseFileAssetSource(bwmPath, "data/root/loose-model.bwm")));
    Assert(bwmInspection.ProviderId == "bwm", "loose BWM provider selection");
    Assert(
        bwmInspection.Details.Any(detail =>
            detail.Kind == Bw2AssetDetailKind.TextureReferenceCount
            && detail.Value is int count
            && count == 1),
        "loose BWM metadata parsing");
    Assert(
        bwmInspection.ReferenceView == Bw2AssetReferenceView.ModelToTexture,
        "loose BWM keeps forward reference view without inventing a root relationship");
    Assert(!bwmInspection.HasPreview, "BWM preview remains unavailable in stage 3.3");

    var fallbackPath = Path.Combine(tempRoot, "loose-image.tga");
    File.WriteAllBytes(fallbackPath, [1, 2, 3, 4]);
    var fallbackContext = Bw2AssetInspectionContext.FromSource(
        new LooseFileAssetSource(fallbackPath, "data/root/loose-image.tga"));
    var fallbackInspection = service.Inspect(fallbackContext);
    Assert(fallbackInspection.ProviderId == "fallback", "neutral fallback provider selection");
    Assert(
        fallbackInspection.Status == Bw2AssetInspectionStatus.Unsupported,
        "neutral fallback unsupported inspection state");
    Assert(
        fallbackInspection.ReferenceView == Bw2AssetReferenceView.Unavailable,
        "fallback reference view remains unavailable");

    var extensibleService = new Bw2AssetInspectionService([new TestTgaMetadataProvider()]);
    var extensibleInspection = extensibleService.Inspect(fallbackContext);
    Assert(extensibleInspection.ProviderId == "test-tga", "additional provider registration priority");
    Assert(
        extensibleInspection.Preview is { Kind: Bw2AssetPreviewKind.Image, ViewerId: "test-viewer" },
        "provider preview descriptor propagation");
}

static void TestValidArchive(string tempRoot)
{
    var archivePath = Path.Combine(tempRoot, "sample.stuff");
    var expected = Encoding.ASCII.GetBytes("BW2 Stuff Explorer");
    const uint expectedTimestamp = 1_118_366_858;
    CreateArchive(archivePath, [("data/test/hello.txt", expected, expectedTimestamp, 0u, null)]);

    var archive = StuffArchive.Open(archivePath);
    Assert(archive.Entries.Count == 1, "entry count");
    var entry = archive.Entries[0];
    Assert(entry.Path == "data/test/hello.txt", "path");
    Assert(entry.Offset == 0 && entry.Length == expected.Length, "offset and length");
    Assert(entry.ModifiedTimestamp == expectedTimestamp, "modified timestamp preservation");
    Assert(entry.ModifiedUtc == DateTimeOffset.FromUnixTimeSeconds(expectedTimestamp), "modified timestamp conversion");

    using (var stream = archive.OpenEntry(entry))
    using (var memory = new MemoryStream())
    {
        stream.CopyTo(memory);
        Assert(memory.ToArray().SequenceEqual(expected), "bounded entry stream");
        AssertThrows<IOException>(() => stream.Seek(1, SeekOrigin.End), "bounded entry seek");
    }

    var output = Path.Combine(tempRoot, "output");
    archive.ExtractEntries(archive.Entries, output);
    Assert(File.ReadAllBytes(Path.Combine(output, "data", "test", "hello.txt")).SequenceEqual(expected), "extraction");
    Assert(!Directory.EnumerateFiles(output, "*.bw2stuff-*.tmp", SearchOption.AllDirectories).Any(), "successful extraction temp cleanup");
}

static void TestMalformedArchives(string tempRoot)
{
    var tooSmall = Path.Combine(tempRoot, "too-small.stuff");
    File.WriteAllBytes(tooSmall, [1, 2, 3]);
    AssertThrows<StuffArchiveException>(() => StuffArchive.Open(tooSmall), "too-small archive rejection");

    var misaligned = Path.Combine(tempRoot, "misaligned.stuff");
    using (var stream = File.Create(misaligned))
    {
        stream.WriteByte(0x42);
        WriteFooter(stream, 0);
    }
    AssertThrows<StuffArchiveException>(() => StuffArchive.Open(misaligned), "misaligned TOC rejection");

    var outsideContent = Path.Combine(tempRoot, "outside-content.stuff");
    CreateArchive(outsideContent, [("data/test.bin", [1, 2, 3], 0u, 2u, 5u)]);
    AssertThrows<StuffArchiveException>(() => StuffArchive.Open(outsideContent), "out-of-range entry rejection");

    var emptyPath = Path.Combine(tempRoot, "empty-path.stuff");
    CreateArchive(emptyPath, [(string.Empty, [1], 0u, 0u, null)]);
    AssertThrows<StuffArchiveException>(() => StuffArchive.Open(emptyPath), "empty path rejection");
}

static void TestUnsafeAndDuplicatePaths(string tempRoot)
{
    var unsafePath = Path.Combine(tempRoot, "unsafe-path.stuff");
    CreateArchive(unsafePath, [("../escape.txt", [1], 0u, 0u, null)]);
    var unsafeArchive = StuffArchive.Open(unsafePath);
    var unsafeOutput = Path.Combine(tempRoot, "unsafe-output");
    AssertThrows<StuffArchiveException>(
        () => unsafeArchive.ExtractEntries(unsafeArchive.Entries, unsafeOutput),
        "unsafe export path rejection");
    Assert(!File.Exists(Path.Combine(tempRoot, "escape.txt")), "unsafe path did not escape destination");

    var duplicatePath = Path.Combine(tempRoot, "duplicate-path.stuff");
    CreateArchive(duplicatePath,
    [
        ("data/Test.txt", new byte[] { 1 }, 0u, 0u, null),
        ("data/test.txt", new byte[] { 2 }, 0u, 1u, null)
    ]);
    var duplicateArchive = StuffArchive.Open(duplicatePath);
    AssertThrows<StuffArchiveException>(
        () => duplicateArchive.ExtractEntries(duplicateArchive.Entries, Path.Combine(tempRoot, "duplicate-output")),
        "case-insensitive duplicate destination rejection");
}

static void TestCancellationCleanup(string tempRoot)
{
    var archivePath = Path.Combine(tempRoot, "cancellation.stuff");
    var content = Enumerable.Repeat((byte)0xA5, 3 * 1024 * 1024).ToArray();
    CreateArchive(archivePath, [("data/large.bin", content, 0u, 0u, null)]);
    var archive = StuffArchive.Open(archivePath);
    var destination = Path.Combine(tempRoot, "cancelled.bin");
    var original = Encoding.ASCII.GetBytes("existing file must survive");
    File.WriteAllBytes(destination, original);

    using var cancellation = new CancellationTokenSource();
    var progress = new InlineProgress<StuffExportProgress>(value =>
    {
        if (value.CompletedEntries == 0 && value.CompletedBytes > 0)
            cancellation.Cancel();
    });

    AssertThrows<OperationCanceledException>(
        () => archive.ExtractEntry(archive.Entries[0], destination, overwrite: true, progress, cancellation.Token),
        "mid-file cancellation");
    Assert(File.ReadAllBytes(destination).SequenceEqual(original), "cancelled export preserves existing destination");
    Assert(!Directory.EnumerateFiles(tempRoot, "*.bw2stuff-*.tmp", SearchOption.AllDirectories).Any(), "cancelled export temp cleanup");
}

static void TestTruncatedSourceCleanup(string tempRoot)
{
    var archivePath = Path.Combine(tempRoot, "truncated-source.stuff");
    var content = Enumerable.Repeat((byte)0x5A, 2 * 1024 * 1024).ToArray();
    CreateArchive(archivePath, [("data/truncated.bin", content, 0u, 0u, null)]);
    var archive = StuffArchive.Open(archivePath);
    var destination = Path.Combine(tempRoot, "truncated-destination.bin");
    var original = Encoding.ASCII.GetBytes("keep this destination too");
    File.WriteAllBytes(destination, original);
    File.WriteAllBytes(archivePath, [0]);

    AssertThrows<StuffArchiveException>(
        () => archive.ExtractEntry(archive.Entries[0], destination, overwrite: true),
        "truncated source detection");
    Assert(File.ReadAllBytes(destination).SequenceEqual(original), "truncated source preserves existing destination");
    Assert(!Directory.EnumerateFiles(tempRoot, "*.bw2stuff-*.tmp", SearchOption.AllDirectories).Any(), "truncated source temp cleanup");
}

static void CreateArchive(
    string path,
    IReadOnlyList<(string Path, byte[] Content, uint Timestamp, uint Offset, uint? DeclaredLength)> entries)
{
    using var stream = File.Create(path);
    foreach (var entry in entries)
        stream.Write(entry.Content);

    var contentLength = entries.Sum(entry => entry.Content.Length);
    foreach (var item in entries)
    {
        var entry = new byte[StuffArchive.EntrySize];
        Encoding.Latin1.GetBytes(item.Path).CopyTo(entry, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(256, 4), item.Offset);
        BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(260, 4), item.DeclaredLength ?? (uint)item.Content.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(264, 4), item.Timestamp);
        stream.Write(entry);
    }

    WriteFooter(stream, (uint)contentLength);
}

static void CreateArchiveWithContents(string path, IReadOnlyList<(string Path, byte[] Content)> entries)
{
    uint offset = 0;
    var archiveEntries = new List<(string Path, byte[] Content, uint Timestamp, uint Offset, uint? DeclaredLength)>();
    foreach (var entry in entries)
    {
        archiveEntries.Add((entry.Path, entry.Content, 0u, offset, null));
        offset = checked(offset + (uint)entry.Content.Length);
    }

    CreateArchive(path, archiveEntries);
}

static byte[] CreateBwm(
    uint version,
    uint modelType,
    IReadOnlyList<(Bw2TextureRole Role, string Path)> references)
{
    return CreateIndexedBwm(
        version,
        modelType,
        materialCount: 1,
        references.Select(reference => (0u, reference.Role, reference.Path)).ToArray());
}

static byte[] CreateIndexedBwm(
    uint version,
    uint modelType,
    uint materialCount,
    IReadOnlyList<(uint MaterialIndex, Bw2TextureRole Role, string Path)> references)
{
    var bytes = new byte[checked(Bw2BwmReader.HeaderSize + (int)materialCount * Bw2BwmReader.MaterialSize)];
    WriteFixedAscii(bytes, 0, 40, "LiOnHeAdMODEL");
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40, 4), (uint)bytes.Length - 44);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44, 4), Bw2BwmReader.Magic);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(48, 4), version);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(124, 4), materialCount);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(176, 4), modelType);

    foreach (var reference in references)
    {
        if (reference.MaterialIndex >= materialCount)
            throw new ArgumentOutOfRangeException(nameof(references), "Material index lies outside the material table.");

        var slotOffset = reference.Role switch
        {
            Bw2TextureRole.DiffuseMap => 0,
            Bw2TextureRole.LightMap => 64,
            Bw2TextureRole.GrowthMap => 128,
            Bw2TextureRole.SpecularMap => 192,
            Bw2TextureRole.AnimatedMap => 256,
            Bw2TextureRole.NormalMap => 320,
            _ => throw new ArgumentOutOfRangeException()
        };
        var materialOffset = checked(
            Bw2BwmReader.HeaderSize + (int)reference.MaterialIndex * Bw2BwmReader.MaterialSize);
        WriteFixedAscii(bytes, materialOffset + slotOffset, 64, reference.Path);
    }

    return bytes;
}

static byte[] CreateLegacyDds(
    string fourCc,
    uint width,
    uint height,
    uint mipLevels,
    uint linearSize,
    uint caps2 = 0,
    uint depth = 0)
{
    var bytes = new byte[Bw2DdsReader.LegacyHeaderSize];
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), 0x20534444);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 124);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), 0x000A1007);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12, 4), height);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), width);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20, 4), linearSize);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24, 4), depth);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28, 4), mipLevels);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76, 4), 32);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80, 4), 0x00000004);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(84, 4), EncodeFourCc(fourCc));
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(108, 4), 0x00401008);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(112, 4), caps2);
    return bytes;
}

static byte[] CreateDx10Dds(
    uint dxgiFormat,
    uint width,
    uint height,
    uint mipLevels,
    uint resourceDimension,
    uint miscFlag,
    uint arraySize)
{
    var legacy = CreateLegacyDds("DX10", width, height, mipLevels, 0);
    var bytes = new byte[Bw2DdsReader.Dx10HeaderSize];
    legacy.CopyTo(bytes, 0);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(128, 4), dxgiFormat);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(132, 4), resourceDimension);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(136, 4), miscFlag);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(140, 4), arraySize);
    return bytes;
}

static byte[] CreateBc1Block(ushort color0, ushort color1, uint selectors)
{
    var bytes = new byte[8];
    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0, 2), color0);
    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2, 2), color1);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), selectors);
    return bytes;
}

static byte[] CreateBc2Block(ulong alpha)
{
    var bytes = new byte[16];
    BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0, 8), alpha);
    return bytes;
}

static byte[] CreateBc3Block(byte alpha0, byte alpha1, ulong selectors)
{
    var bytes = new byte[16];
    bytes[0] = alpha0;
    bytes[1] = alpha1;
    for (var index = 0; index < 6; index++)
        bytes[index + 2] = (byte)(selectors >> (index * 8));
    return bytes;
}

static byte[] Combine(params byte[][] segments)
{
    var bytes = new byte[segments.Sum(segment => segment.Length)];
    var offset = 0;
    foreach (var segment in segments)
    {
        segment.CopyTo(bytes, offset);
        offset += segment.Length;
    }

    return bytes;
}

static uint EncodeFourCc(string value)
{
    if (value.Length != 4)
        throw new ArgumentException("FourCC values must contain exactly four ASCII characters.", nameof(value));
    return BinaryPrimitives.ReadUInt32LittleEndian(Encoding.ASCII.GetBytes(value));
}

static void WriteFixedAscii(byte[] bytes, int offset, int length, string value)
{
    var encoded = Encoding.ASCII.GetBytes(value);
    if (encoded.Length >= length)
        throw new ArgumentException($"Value must be shorter than {length} bytes.", nameof(value));
    encoded.AsSpan().CopyTo(bytes.AsSpan(offset, length));
}

static void WriteFooter(Stream stream, uint contentLength)
{
    Span<byte> footer = stackalloc byte[4];
    BinaryPrimitives.WriteUInt32LittleEndian(footer, contentLength);
    stream.Write(footer);
}

static void Assert(bool condition, string description)
{
    if (!condition)
        throw new InvalidOperationException($"Self-test failed: {description}");
}

static void AssertThrows<TException>(Action action, string description) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Self-test failed: {description}");
}

sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}

sealed class TestTgaMetadataProvider : IBw2AssetMetadataProvider
{
    public string Id => "test-tga";

    public bool CanInspect(Bw2AssetInspectionContext context) =>
        context.Source.Extension.Equals("TGA", StringComparison.OrdinalIgnoreCase);

    public Bw2AssetInspection Inspect(Bw2AssetInspectionContext context) => new(
        Id,
        Bw2AssetInspectionStatus.Valid,
        [new Bw2AssetDetail(Bw2AssetDetailKind.Format, "TGA")],
        Bw2AssetReferenceView.Unavailable,
        Array.Empty<Bw2AssetReference>(),
        Bw2AssetReferenceEmptyReason.Unavailable,
        preview: new Bw2AssetPreviewDescriptor(Bw2AssetPreviewKind.Image, "test-viewer"));
}
