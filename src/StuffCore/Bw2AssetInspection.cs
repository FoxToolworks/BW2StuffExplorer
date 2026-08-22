namespace StuffCore;

public interface IBw2AssetSource
{
    string Name { get; }
    string Path { get; }
    string Extension { get; }
    long Length { get; }
    Stream OpenRead();
}

public sealed class StuffArchiveAssetSource : IBw2AssetSource
{
    private readonly StuffArchive _archive;

    public StuffArchiveAssetSource(StuffArchive archive, StuffEntry entry)
    {
        _archive = archive ?? throw new ArgumentNullException(nameof(archive));
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    public StuffEntry Entry { get; }
    public string Name => Entry.Name;
    public string Path => Entry.Path;
    public string Extension => Entry.Extension;
    public long Length => Entry.Length;
    public Stream OpenRead() => _archive.OpenEntry(Entry);
}

public sealed class LooseFileAssetSource : IBw2AssetSource
{
    private readonly FileInfo _file;

    public LooseFileAssetSource(string filePath, string? logicalPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _file = new FileInfo(filePath);
        Path = string.IsNullOrWhiteSpace(logicalPath) ? _file.FullName : logicalPath;
    }

    public string Name => _file.Name;
    public string Path { get; }
    public string Extension => System.IO.Path.GetExtension(_file.Name).TrimStart('.').ToUpperInvariant();
    public long Length => _file.Length;
    public Stream OpenRead() => _file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
}

public enum Bw2AssetDetailKind
{
    Format,
    Header,
    Dimensions,
    TextureType,
    PixelFormat,
    FourCc,
    DxgiFormat,
    MipLevels,
    ColorSpace,
    AlphaCapable,
    NonOpaqueAlpha,
    Pitch,
    LinearSize,
    ArraySize,
    CubemapFaces,
    Signature,
    Magic,
    BwmVersion,
    ModelType,
    MaterialCount,
    TextureReferenceCount
}

public enum Bw2AssetInspectionStatus
{
    Valid,
    Invalid,
    Unsupported
}

public enum Bw2AssetReferenceView
{
    Unavailable,
    ModelToTexture,
    TextureToModel
}

public enum Bw2AssetReferenceEmptyReason
{
    Unavailable,
    InvalidBwm,
    NoBwmTextures,
    NoDdsModels
}

public enum Bw2AssetPreviewKind
{
    Image,
    Model3D,
    Audio,
    Text,
    Binary
}

public sealed record Bw2AssetPreviewDescriptor(
    Bw2AssetPreviewKind Kind,
    string ViewerId);

public sealed record Bw2AssetReference(
    string ModelPath,
    uint MaterialIndex,
    Bw2TextureRole Role,
    string ReferencePath,
    Bw2ReferenceResolutionStatus ResolutionStatus,
    string? ResolvedAssetPath,
    IReadOnlyList<string> CandidateAssetPaths)
{
    internal static Bw2AssetReference FromRelationship(Bw2TextureRelationship relationship) => new(
        relationship.ModelEntry.Path,
        relationship.MaterialIndex,
        relationship.Role,
        relationship.ReferencePath,
        relationship.ResolutionStatus,
        relationship.TextureEntry?.Path,
        relationship.Candidates.Select(candidate => candidate.Path).ToArray());
}

public sealed record Bw2AssetDetail(
    Bw2AssetDetailKind Kind,
    object Value);

public sealed class Bw2AssetInspection
{
    public Bw2AssetInspection(
        string providerId,
        Bw2AssetInspectionStatus status,
        IReadOnlyList<Bw2AssetDetail> details,
        Bw2AssetReferenceView referenceView,
        IReadOnlyList<Bw2AssetReference> references,
        Bw2AssetReferenceEmptyReason emptyReferenceReason,
        string? error = null,
        Bw2AssetPreviewDescriptor? preview = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ProviderId = providerId;
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(references);
        if (status == Bw2AssetInspectionStatus.Invalid)
            ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Status = status;
        Details = details.ToArray();
        ReferenceView = referenceView;
        References = references.ToArray();
        EmptyReferenceReason = emptyReferenceReason;
        Error = error;
        Preview = preview;
    }

    public string ProviderId { get; }
    public Bw2AssetInspectionStatus Status { get; }
    public IReadOnlyList<Bw2AssetDetail> Details { get; }
    public Bw2AssetReferenceView ReferenceView { get; }
    public IReadOnlyList<Bw2AssetReference> References { get; }
    public Bw2AssetReferenceEmptyReason EmptyReferenceReason { get; }
    public string? Error { get; }
    public Bw2AssetPreviewDescriptor? Preview { get; }
    public bool HasPreview => Preview is not null;
}

public sealed class Bw2AssetInspectionContext
{
    public Bw2AssetInspectionContext(
        IBw2AssetSource source,
        Bw2AssetClassification classification,
        Bw2BwmModelInfo? bwmModel = null,
        IReadOnlyList<Bw2AssetReference>? referencesFromAsset = null,
        IReadOnlyList<Bw2AssetReference>? referencesToAsset = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Classification = classification ?? throw new ArgumentNullException(nameof(classification));
        BwmModel = bwmModel;
        ReferencesFromAsset = referencesFromAsset?.ToArray() ?? Array.Empty<Bw2AssetReference>();
        ReferencesToAsset = referencesToAsset?.ToArray() ?? Array.Empty<Bw2AssetReference>();
    }

    public IBw2AssetSource Source { get; }
    public Bw2AssetClassification Classification { get; }
    public Bw2BwmModelInfo? BwmModel { get; }
    public IReadOnlyList<Bw2AssetReference> ReferencesFromAsset { get; }
    public IReadOnlyList<Bw2AssetReference> ReferencesToAsset { get; }

    public static Bw2AssetInspectionContext FromArchive(
        StuffArchive archive,
        StuffEntry entry,
        Bw2ArchiveAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(analysis);

        analysis.BwmModels.TryGetValue(entry, out var bwmModel);
        return new Bw2AssetInspectionContext(
            new StuffArchiveAssetSource(archive, entry),
            analysis.GetClassification(entry),
            bwmModel,
            analysis.GetRelationshipsFromModel(entry)
                .Select(Bw2AssetReference.FromRelationship)
                .ToArray(),
            analysis.GetRelationshipsForTexture(entry)
                .Select(Bw2AssetReference.FromRelationship)
                .ToArray());
    }

    public static Bw2AssetInspectionContext FromSource(IBw2AssetSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new Bw2AssetInspectionContext(
            source,
            Bw2AssetClassifier.ClassifyPath(source.Path));
    }
}

public interface IBw2AssetMetadataProvider
{
    string Id { get; }
    bool CanInspect(Bw2AssetInspectionContext context);
    Bw2AssetInspection Inspect(Bw2AssetInspectionContext context);
}

public sealed class Bw2AssetInspectionService
{
    private readonly IReadOnlyList<IBw2AssetMetadataProvider> _providers;

    public Bw2AssetInspectionService(IEnumerable<IBw2AssetMetadataProvider>? additionalProviders = null)
    {
        var providers = new List<IBw2AssetMetadataProvider>();
        if (additionalProviders is not null)
            providers.AddRange(additionalProviders);
        providers.Add(new Bw2DdsMetadataProvider());
        providers.Add(new Bw2BwmMetadataProvider());
        providers.Add(new Bw2FallbackMetadataProvider());
        _providers = providers;
    }

    public Bw2AssetInspection Inspect(Bw2AssetInspectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _providers.First(provider => provider.CanInspect(context)).Inspect(context);
    }
}

internal sealed class Bw2DdsMetadataProvider : IBw2AssetMetadataProvider
{
    public string Id => "dds";

    public bool CanInspect(Bw2AssetInspectionContext context) =>
        context.Source.Extension.Equals("DDS", StringComparison.OrdinalIgnoreCase);

    public Bw2AssetInspection Inspect(Bw2AssetInspectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Bw2DdsInfo? info;
        string error;
        Bw2DdsNonOpaqueAlphaStatus alphaStatus;
        try
        {
            using var stream = context.Source.OpenRead();
            if (!Bw2DdsReader.TryRead(stream, out info, out error) || info is null)
                return InvalidDds(error, context.ReferencesToAsset);
            alphaStatus = Bw2DdsAlphaAnalyzer.Analyze(stream, info);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return InvalidDds($"The DDS data could not be read: {exception.Message}", context.ReferencesToAsset);
        }

        var details = new List<Bw2AssetDetail>
        {
            new(Bw2AssetDetailKind.Format, "DDS"),
            new(Bw2AssetDetailKind.Header, info.HeaderKind),
            new(Bw2AssetDetailKind.Dimensions, info),
            new(Bw2AssetDetailKind.TextureType, info.TextureKind),
            new(Bw2AssetDetailKind.PixelFormat, info.PixelFormat)
        };

        if (info.FourCc is { } fourCc)
            details.Add(new Bw2AssetDetail(Bw2AssetDetailKind.FourCc, fourCc));
        if (info.DxgiFormat is not null && info.DxgiFormatValue is not null)
            details.Add(new Bw2AssetDetail(Bw2AssetDetailKind.DxgiFormat, info));

        details.Add(new Bw2AssetDetail(Bw2AssetDetailKind.MipLevels, info.MipLevelCount));
        details.Add(new Bw2AssetDetail(Bw2AssetDetailKind.ColorSpace, info.ColorSpace));
        details.Add(new Bw2AssetDetail(Bw2AssetDetailKind.AlphaCapable, info.AlphaCapability));
        details.Add(new Bw2AssetDetail(Bw2AssetDetailKind.NonOpaqueAlpha, alphaStatus));

        if (info.DataLayout != Bw2DdsDataLayout.Unknown)
        {
            details.Add(new Bw2AssetDetail(
                info.DataLayout == Bw2DdsDataLayout.Pitch
                    ? Bw2AssetDetailKind.Pitch
                    : Bw2AssetDetailKind.LinearSize,
                info.PitchOrLinearSize));
        }

        if (info.ArraySize is { } arraySize)
            details.Add(new Bw2AssetDetail(Bw2AssetDetailKind.ArraySize, arraySize));
        if (info.DeclaredCubemapFaceCount is { } faceCount)
            details.Add(new Bw2AssetDetail(Bw2AssetDetailKind.CubemapFaces, faceCount));

        return new Bw2AssetInspection(
            Id,
            Bw2AssetInspectionStatus.Valid,
            details,
            Bw2AssetReferenceView.TextureToModel,
            context.ReferencesToAsset,
            Bw2AssetReferenceEmptyReason.NoDdsModels);
    }

    private Bw2AssetInspection InvalidDds(
        string error,
        IReadOnlyList<Bw2AssetReference> references) => new(
            Id,
            Bw2AssetInspectionStatus.Invalid,
            [
                new Bw2AssetDetail(Bw2AssetDetailKind.Format, "DDS")
            ],
            Bw2AssetReferenceView.TextureToModel,
            references,
            Bw2AssetReferenceEmptyReason.NoDdsModels,
            error);
}

internal sealed class Bw2BwmMetadataProvider : IBw2AssetMetadataProvider
{
    public string Id => "bwm";

    public bool CanInspect(Bw2AssetInspectionContext context) =>
        context.Source.Extension.Equals("BWM", StringComparison.OrdinalIgnoreCase);

    public Bw2AssetInspection Inspect(Bw2AssetInspectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var model = context.BwmModel;
        var error = string.Empty;

        if (model is null)
        {
            try
            {
                using var stream = context.Source.OpenRead();
                Bw2BwmReader.TryRead(
                    stream,
                    context.Source.Length,
                    context.Source.Extension,
                    out model,
                    out error);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                error = $"The BWM data could not be read: {exception.Message}";
            }
        }

        if (model is null)
        {
            return new Bw2AssetInspection(
                Id,
                Bw2AssetInspectionStatus.Invalid,
                [
                    new Bw2AssetDetail(Bw2AssetDetailKind.Format, "BWM")
                ],
                Bw2AssetReferenceView.ModelToTexture,
                Array.Empty<Bw2AssetReference>(),
                Bw2AssetReferenceEmptyReason.InvalidBwm,
                error);
        }

        return new Bw2AssetInspection(
            Id,
            Bw2AssetInspectionStatus.Valid,
            [
                new Bw2AssetDetail(Bw2AssetDetailKind.Format, "BWM"),
                new Bw2AssetDetail(Bw2AssetDetailKind.Signature, Bw2BwmReader.Signature),
                new Bw2AssetDetail(Bw2AssetDetailKind.Magic, Bw2BwmReader.Magic),
                new Bw2AssetDetail(Bw2AssetDetailKind.BwmVersion, model.Version),
                new Bw2AssetDetail(Bw2AssetDetailKind.ModelType, model.ModelType),
                new Bw2AssetDetail(Bw2AssetDetailKind.MaterialCount, model.MaterialCount),
                new Bw2AssetDetail(Bw2AssetDetailKind.TextureReferenceCount, model.TextureReferences.Count)
            ],
            Bw2AssetReferenceView.ModelToTexture,
            context.ReferencesFromAsset,
            Bw2AssetReferenceEmptyReason.NoBwmTextures);
    }
}

internal sealed class Bw2FallbackMetadataProvider : IBw2AssetMetadataProvider
{
    public string Id => "fallback";
    public bool CanInspect(Bw2AssetInspectionContext context) => true;

    public Bw2AssetInspection Inspect(Bw2AssetInspectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Bw2AssetInspection(
            Id,
            Bw2AssetInspectionStatus.Unsupported,
            [
                new Bw2AssetDetail(Bw2AssetDetailKind.Format, context.Classification.Format)
            ],
            Bw2AssetReferenceView.Unavailable,
            Array.Empty<Bw2AssetReference>(),
            Bw2AssetReferenceEmptyReason.Unavailable);
    }
}
