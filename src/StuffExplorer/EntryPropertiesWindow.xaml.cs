using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using StuffCore;

namespace StuffExplorer;

public partial class EntryPropertiesWindow : Window
{
    private const double ExpandedTextureColumnWidth = 185;
    private static Rect? _sessionBounds;
    private static readonly Bw2AssetInspectionService InspectionService = new();
    private bool _additionalMapColumnsExpanded;

    internal EntryPropertiesWindow(
        AssetEntryViewModel entry,
        StuffArchive archive,
        Bw2ArchiveAnalysis analysis)
    {
        InitializeComponent();
        HeaderNameText.Text = entry.Name;
        HeaderTypeText.Text = entry.TypeDisplay;
        GeneralDetailsList.ItemsSource = CreateGeneralRows(entry, archive);

        var context = Bw2AssetInspectionContext.FromArchive(archive, entry.Entry, analysis);
        var inspection = InspectionService.Inspect(context);
        FileDetailsList.ItemsSource = CreateFileDetailRows(inspection);
        ConfigureContents(inspection);
        ConfigureReferences(inspection);
    }

    private static IReadOnlyList<FileDetailRow> CreateGeneralRows(
        AssetEntryViewModel entry,
        StuffArchive archive) =>
        ApplySectionStarts(new[]
        {
            CreateFileDetailRow(FileDetailSection.File, S("PropertyName"), entry.Name),
            CreateFileDetailRow(FileDetailSection.File, S("PropertyFullPath"), entry.Path),
            CreateFileDetailRow(FileDetailSection.File, S("PropertyType"), entry.TypeDisplay),
            CreateFileDetailRow(
                FileDetailSection.File,
                S("PropertyModified"),
                $"{entry.ModifiedLocalTime:G} ({entry.ModifiedUtc:yyyy-MM-dd HH:mm:ss} UTC)"),
            CreateFileDetailRow(
                FileDetailSection.File,
                S("PropertySize"),
                $"{FileSizeConverter.Format(entry.Length)} ({entry.Length:N0} bytes)"),
            CreateFileDetailRow(FileDetailSection.Archive, S("PropertyArchive"), archive.FilePath),
            CreateFileDetailRow(
                FileDetailSection.Archive,
                S("PropertyOffset"),
                $"0x{entry.Offset:X8} ({entry.Offset:N0})")
        });

    private static IReadOnlyList<FileDetailRow> CreateFileDetailRows(Bw2AssetInspection inspection)
    {
        var rows = inspection.Details.Select(CreateFileDetailRow).ToList();
        if (inspection.Status == Bw2AssetInspectionStatus.Invalid)
        {
            var insertionIndex = Math.Min(1, rows.Count);
            rows.Insert(
                insertionIndex,
                CreateFileDetailRow(
                    FileDetailSection.File,
                    S("PropertyDetailHeaderStatus"),
                    S("PropertyDetailInvalid")));
            rows.Insert(
                insertionIndex + 1,
                CreateFileDetailRow(
                    FileDetailSection.File,
                    S("PropertyDetailReason"),
                    inspection.Error ?? S("PropertyDetailUnknown")));
        }
        else if (inspection.Status == Bw2AssetInspectionStatus.Unsupported)
        {
            rows.Add(CreateFileDetailRow(
                FileDetailSection.File,
                S("PropertyDetailStatus"),
                S("PropertyNoSpecializedReader")));
        }

        return ApplySectionStarts(rows);
    }

    private static IReadOnlyList<FileDetailRow> ApplySectionStarts(
        IEnumerable<FileDetailRow> rows)
    {
        var previousSection = -1;
        return rows
            .OrderBy(row => row.SectionOrder)
            .Select(row =>
            {
                var startsSection = row.SectionOrder != previousSection;
                previousSection = row.SectionOrder;
                return row with { StartsSection = startsSection };
            })
            .ToArray();
    }

    private static FileDetailRow CreateFileDetailRow(Bw2AssetDetail detail)
    {
        var section = GetDetailSection(detail.Kind);
        return CreateFileDetailRow(section, GetDetailLabel(detail.Kind), FormatDetailValue(detail));
    }

    private static FileDetailRow CreateFileDetailRow(
        FileDetailSection section,
        string label,
        string value) =>
        new((int)section, GetDetailSectionLabel(section), false, label, value);

    private static FileDetailSection GetDetailSection(Bw2AssetDetailKind kind) => kind switch
    {
        Bw2AssetDetailKind.Dimensions
            or Bw2AssetDetailKind.TextureType
            or Bw2AssetDetailKind.ImageEncoding
            or Bw2AssetDetailKind.ImageOrigin
            or Bw2AssetDetailKind.BmpRowOrder
            or Bw2AssetDetailKind.PixelOrder
            or Bw2AssetDetailKind.Interleaving
            or Bw2AssetDetailKind.MipLevels
            or Bw2AssetDetailKind.ArraySize
            or Bw2AssetDetailKind.CubemapFaces => FileDetailSection.Image,
        Bw2AssetDetailKind.PixelFormat
            or Bw2AssetDetailKind.PixelDepth
            or Bw2AssetDetailKind.BmpCompression
            or Bw2AssetDetailKind.ChannelLayout
            or Bw2AssetDetailKind.Rgb555HighBit
            or Bw2AssetDetailKind.DeclaredAttributeBits
            or Bw2AssetDetailKind.ColorMap
            or Bw2AssetDetailKind.FourCc
            or Bw2AssetDetailKind.DxgiFormat
            or Bw2AssetDetailKind.ColorSpace
            or Bw2AssetDetailKind.AlphaCapable
            or Bw2AssetDetailKind.NonOpaqueAlpha => FileDetailSection.PixelFormat,
        Bw2AssetDetailKind.Pitch
            or Bw2AssetDetailKind.LinearSize
            or Bw2AssetDetailKind.PixelDataSize
            or Bw2AssetDetailKind.StoredHeaderValue
            or Bw2AssetDetailKind.ImageIdLength
            or Bw2AssetDetailKind.PixelDataOffset
            or Bw2AssetDetailKind.TgaFooter
            or Bw2AssetDetailKind.TgaExtensionArea => FileDetailSection.Storage,
        Bw2AssetDetailKind.ModelType
            or Bw2AssetDetailKind.MaterialCount
            or Bw2AssetDetailKind.TextureReferenceCount => FileDetailSection.Model,
        _ => FileDetailSection.File
    };

    private static string GetDetailSectionLabel(FileDetailSection section) => section switch
    {
        FileDetailSection.Image => S("PropertyDetailSectionImage"),
        FileDetailSection.Archive => S("PropertyDetailSectionArchive"),
        FileDetailSection.PixelFormat => S("PropertyDetailSectionPixelFormat"),
        FileDetailSection.Storage => S("PropertyDetailSectionStorage"),
        FileDetailSection.Model => S("PropertyDetailSectionModel"),
        _ => S("PropertyDetailSectionFile")
    };

    private static string GetDetailLabel(Bw2AssetDetailKind kind) => kind switch
    {
        Bw2AssetDetailKind.Format => S("PropertyDetailFormat"),
        Bw2AssetDetailKind.Header => S("PropertyDetailHeader"),
        Bw2AssetDetailKind.Dimensions => S("PropertyDetailDimensions"),
        Bw2AssetDetailKind.TextureType => S("PropertyDetailTextureType"),
        Bw2AssetDetailKind.ImageEncoding => S("PropertyDetailImageEncoding"),
        Bw2AssetDetailKind.PixelDepth => S("PropertyDetailPixelDepth"),
        Bw2AssetDetailKind.DeclaredAttributeBits => S("PropertyDetailDeclaredAttributeBits"),
        Bw2AssetDetailKind.ImageOrigin => S("PropertyDetailImageOrigin"),
        Bw2AssetDetailKind.PixelOrder => S("PropertyDetailPixelOrder"),
        Bw2AssetDetailKind.Interleaving => S("PropertyDetailInterleaving"),
        Bw2AssetDetailKind.ColorMap => S("PropertyDetailColorMap"),
        Bw2AssetDetailKind.ImageIdLength => S("PropertyDetailImageIdLength"),
        Bw2AssetDetailKind.PixelDataOffset => S("PropertyDetailPixelDataOffset"),
        Bw2AssetDetailKind.TgaFooter => S("PropertyDetailTgaFooter"),
        Bw2AssetDetailKind.TgaExtensionArea => S("PropertyDetailTgaExtensionArea"),
        Bw2AssetDetailKind.DibHeader => S("PropertyDetailDibHeader"),
        Bw2AssetDetailKind.BmpCompression => S("PropertyDetailBmpCompression"),
        Bw2AssetDetailKind.BmpRowOrder => S("PropertyDetailBmpRowOrder"),
        Bw2AssetDetailKind.PixelFormat => S("PropertyDetailPixelFormat"),
        Bw2AssetDetailKind.ChannelLayout => S("PropertyDetailChannelLayout"),
        Bw2AssetDetailKind.Rgb555HighBit => S("PropertyDetailRgb555HighBit"),
        Bw2AssetDetailKind.PixelDataSize => S("PropertyDetailPixelDataSize"),
        Bw2AssetDetailKind.StoredHeaderValue => S("PropertyDetailStoredHeaderValue"),
        Bw2AssetDetailKind.FourCc => S("PropertyDetailFourCc"),
        Bw2AssetDetailKind.DxgiFormat => S("PropertyDetailDxgiFormat"),
        Bw2AssetDetailKind.MipLevels => S("PropertyDetailMipLevels"),
        Bw2AssetDetailKind.ColorSpace => S("PropertyDetailColorSpace"),
        Bw2AssetDetailKind.AlphaCapable => S("PropertyDetailAlphaCapable"),
        Bw2AssetDetailKind.NonOpaqueAlpha => S("PropertyDetailNonOpaqueAlpha"),
        Bw2AssetDetailKind.Pitch => S("PropertyDetailPitch"),
        Bw2AssetDetailKind.LinearSize => S("PropertyDetailLinearSize"),
        Bw2AssetDetailKind.ArraySize => S("PropertyDetailArraySize"),
        Bw2AssetDetailKind.CubemapFaces => S("PropertyDetailCubemapFaces"),
        Bw2AssetDetailKind.Signature => S("PropertyDetailSignature"),
        Bw2AssetDetailKind.Magic => S("PropertyDetailMagic"),
        Bw2AssetDetailKind.BwmVersion => S("PropertyDetailBwmVersion"),
        Bw2AssetDetailKind.ModelType => S("PropertyDetailModelType"),
        Bw2AssetDetailKind.MaterialCount => S("PropertyDetailMaterialCount"),
        Bw2AssetDetailKind.TextureReferenceCount => S("PropertyDetailTextureReferenceCount"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatDetailValue(Bw2AssetDetail detail) => detail.Kind switch
    {
        Bw2AssetDetailKind.Format => EmptyAsUnknown((string)detail.Value),
        Bw2AssetDetailKind.Header => FormatHeader((Bw2DdsHeaderKind)detail.Value),
        Bw2AssetDetailKind.Dimensions => FormatDimensions(detail.Value),
        Bw2AssetDetailKind.TextureType => FormatTextureKind((Bw2DdsTextureKind)detail.Value),
        Bw2AssetDetailKind.ImageEncoding => FormatTgaImageType((Bw2TgaImageType)detail.Value),
        Bw2AssetDetailKind.PixelDepth => FormatPixelDepth(detail.Value),
        Bw2AssetDetailKind.DeclaredAttributeBits => string.Format(
            S("PropertyDetailDeclaredAttributeBitsValue"),
            (byte)detail.Value),
        Bw2AssetDetailKind.ImageOrigin => FormatTgaOrigin((Bw2TgaVerticalOrigin)detail.Value),
        Bw2AssetDetailKind.PixelOrder => FormatTgaPixelOrder((Bw2TgaHorizontalOrder)detail.Value),
        Bw2AssetDetailKind.Interleaving => FormatTgaInterleaving((Bw2TgaInterleaving)detail.Value),
        Bw2AssetDetailKind.ColorMap => FormatTgaColorMap((Bw2TgaInfo)detail.Value),
        Bw2AssetDetailKind.ImageIdLength => $"{(byte)detail.Value:N0} bytes",
        Bw2AssetDetailKind.PixelDataOffset => $"{(long)detail.Value:N0} bytes",
        Bw2AssetDetailKind.TgaFooter => (bool)detail.Value
            ? S("PropertyDetailPresent")
            : S("PropertyDetailNotPresent"),
        Bw2AssetDetailKind.TgaExtensionArea => FormatTgaExtensionArea((Bw2TgaInfo)detail.Value),
        Bw2AssetDetailKind.DibHeader => FormatBmpDibHeader((Bw2BmpInfo)detail.Value),
        Bw2AssetDetailKind.BmpCompression => FormatBmpCompression((Bw2BmpCompression)detail.Value),
        Bw2AssetDetailKind.BmpRowOrder => FormatBmpRowOrder((Bw2BmpRowOrder)detail.Value),
        Bw2AssetDetailKind.PixelFormat => FormatPixelFormat(detail.Value),
        Bw2AssetDetailKind.ChannelLayout => FormatRgb555ChannelLayout((Bw2Rgb555PixelFormat)detail.Value),
        Bw2AssetDetailKind.Rgb555HighBit => FormatRgb555HighBit((Bw2Rgb555Info)detail.Value),
        Bw2AssetDetailKind.PixelDataSize => $"{(long)detail.Value:N0} bytes",
        Bw2AssetDetailKind.StoredHeaderValue => string.Format(
            S("PropertyDetailStoredHeaderValueValue"),
            (uint)detail.Value),
        Bw2AssetDetailKind.DxgiFormat => FormatDxgiFormat((Bw2DdsInfo)detail.Value),
        Bw2AssetDetailKind.ColorSpace => FormatColorSpace((Bw2DdsColorSpace)detail.Value),
        Bw2AssetDetailKind.AlphaCapable => FormatAlphaCapability((Bw2DdsAlphaCapability)detail.Value),
        Bw2AssetDetailKind.NonOpaqueAlpha => FormatNonOpaqueAlpha((Bw2DdsNonOpaqueAlphaStatus)detail.Value),
        Bw2AssetDetailKind.Pitch or Bw2AssetDetailKind.LinearSize => $"{(uint)detail.Value:N0} bytes",
        Bw2AssetDetailKind.CubemapFaces => (int)detail.Value == 0
            ? S("PropertyDetailNotDeclared")
            : $"{(int)detail.Value} / 6",
        Bw2AssetDetailKind.Magic => $"0x{(uint)detail.Value:X8}",
        Bw2AssetDetailKind.ModelType => FormatBwmModelType((uint)detail.Value),
        Bw2AssetDetailKind.MipLevels
            or Bw2AssetDetailKind.ArraySize
            or Bw2AssetDetailKind.BwmVersion
            or Bw2AssetDetailKind.MaterialCount => $"{(uint)detail.Value:N0}",
        Bw2AssetDetailKind.TextureReferenceCount => $"{(int)detail.Value:N0}",
        _ => detail.Value.ToString() ?? S("PropertyDetailUnknown")
    };

    private void ConfigureReferences(Bw2AssetInspection inspection)
    {
        IEnumerable<Bw2AssetReference> references = inspection.ReferenceView switch
        {
            Bw2AssetReferenceView.ModelToImage => inspection.References,
            Bw2AssetReferenceView.ImageToModel => inspection.References
                .OrderBy(reference => reference.ModelPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(reference => reference.MaterialIndex)
                .ThenBy(reference => reference.Role),
            _ => Array.Empty<Bw2AssetReference>()
        };

        IReadOnlyList<ReferenceRow> rows = references
            .Select(reference => new ReferenceRow(
                reference.MaterialIndex,
                EmptyAsDash(reference.MaterialName),
                AssetDisplayNames.GetTextureRole(reference.Role),
                reference.ModelPath,
                reference.ReferencePath,
                FormatResolutionStatus(reference.ResolutionStatus),
                FormatResolutionStatusDescription(reference.ResolutionStatus),
                FormatReferenceCandidates(reference)))
            .ToArray();

        var showReverseContext = inspection.ReferenceView == Bw2AssetReferenceView.ImageToModel;
        ReferenceMaterialNameColumn.Visibility = showReverseContext
            ? Visibility.Visible
            : Visibility.Collapsed;
        ReferenceModelEntryColumn.Visibility = showReverseContext
            ? Visibility.Visible
            : Visibility.Collapsed;

        ReferencesGrid.ItemsSource = rows;
        ReferencesGrid.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ReferencesMessage.Text = GetEmptyReferenceMessage(inspection.EmptyReferenceReason);
        ReferencesMessage.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ConfigureContents(Bw2AssetInspection inspection)
    {
        IReadOnlyList<MaterialContentRow> rows = inspection.Contents
            .OfType<Bw2BwmMaterialContent>()
            .OrderBy(material => material.MaterialIndex)
            .Select(material => new MaterialContentRow(
                material.MaterialIndex,
                EmptyAsDash(material.StoredName),
                EmptyAsDash(material.DiffuseMap),
                EmptyAsDash(material.LightMap),
                EmptyAsDash(material.GrowthMap),
                EmptyAsDash(material.SpecularMap),
                EmptyAsDash(material.AdditionalMap),
                EmptyAsDash(material.NormalMap)))
            .ToArray();

        ContentsGrid.ItemsSource = rows;
        ContentsTab.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AdditionalMapsToggleButton_Click(object sender, RoutedEventArgs e) =>
        SetAdditionalMapColumnsExpanded(!_additionalMapColumnsExpanded);

    private void SetAdditionalMapColumnsExpanded(bool expanded)
    {
        _additionalMapColumnsExpanded = expanded;
        var visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        var textureColumns = new[]
        {
            ContentLightMapColumn,
            ContentGrowthMapColumn,
            ContentSpecularMapColumn,
            ContentAdditionalMapColumn,
            ContentNormalMapColumn
        };

        foreach (var column in textureColumns)
        {
            column.Width = new DataGridLength(
                ExpandedTextureColumnWidth,
                DataGridLengthUnitType.Pixel);
            column.Visibility = visibility;
        }

        ContentDiffuseMapColumn.Width = expanded
            ? new DataGridLength(ExpandedTextureColumnWidth, DataGridLengthUnitType.Pixel)
            : new DataGridLength(1, DataGridLengthUnitType.Star);
        AdditionalMapsToggleButton.Content = S(expanded
            ? "PropertyContentsHideAdditionalMaps"
            : "PropertyContentsShowAdditionalMaps");

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            VisualTreeSearch.FindDescendant<ScrollViewer>(ContentsGrid)?.ScrollToLeftEnd()));
    }

    private static string GetEmptyReferenceMessage(Bw2AssetReferenceEmptyReason reason) => reason switch
    {
        Bw2AssetReferenceEmptyReason.InvalidBwm => S("PropertyReferencesInvalidBwm"),
        Bw2AssetReferenceEmptyReason.NoBwmTextures => S("PropertyReferencesNoBwmTextures"),
        Bw2AssetReferenceEmptyReason.NoImageReferences => S("PropertyReferencesNoImageReferences"),
        _ => S("PropertyReferencesUnavailable")
    };

    private static string FormatBwmModelType(uint modelType) => modelType switch
    {
        2 => $"{S("FileTypeStaticModel")} (2)",
        3 => $"{S("FileTypeSkinnedModel")} (3)",
        _ => $"{S("PropertyDetailUnknown")} ({modelType})"
    };

    private static string FormatResolutionStatus(Bw2ReferenceResolutionStatus status) => status switch
    {
        Bw2ReferenceResolutionStatus.ResolvedExactPath => S("PropertyReferenceResolvedExactPath"),
        Bw2ReferenceResolutionStatus.ResolvedUniqueFileName => S("PropertyReferenceResolvedUniqueFileName"),
        Bw2ReferenceResolutionStatus.Missing => S("PropertyReferenceMissing"),
        Bw2ReferenceResolutionStatus.Ambiguous => S("PropertyReferenceAmbiguous"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatResolutionStatusDescription(Bw2ReferenceResolutionStatus status) => status switch
    {
        Bw2ReferenceResolutionStatus.ResolvedExactPath => S("PropertyReferenceResolvedExactPathDescription"),
        Bw2ReferenceResolutionStatus.ResolvedUniqueFileName => S("PropertyReferenceResolvedUniqueFileNameDescription"),
        Bw2ReferenceResolutionStatus.Missing => S("PropertyReferenceMissingDescription"),
        Bw2ReferenceResolutionStatus.Ambiguous => S("PropertyReferenceAmbiguousDescription"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatReferenceCandidates(Bw2AssetReference reference)
    {
        if (reference.CandidateAssetPaths.Count > 0)
        {
            return string.Join(
                "; ",
                reference.CandidateAssetPaths);
        }

        return "—";
    }

    private static string FormatHeader(Bw2DdsHeaderKind kind) => kind switch
    {
        Bw2DdsHeaderKind.Legacy => S("PropertyDetailLegacyHeader"),
        Bw2DdsHeaderKind.Dx10Extended => S("PropertyDetailDx10Header"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatDimensions(object value) => value switch
    {
        Bw2DdsInfo info when info.TextureKind == Bw2DdsTextureKind.Texture3D && info.Depth > 0 =>
            $"{info.Width:N0} × {info.Height:N0} × {info.Depth:N0}",
        Bw2DdsInfo info => $"{info.Width:N0} × {info.Height:N0}",
        Bw2TgaInfo info => $"{info.Width:N0} × {info.Height:N0}",
        Bw2BmpInfo info => $"{info.Width:N0} × {info.Height:N0}",
        Bw2Rgb555Info info => $"{info.Width:N0} × {info.Height:N0}",
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatPixelFormat(object value) => value switch
    {
        string pixelFormat => EmptyAsUnknown(pixelFormat),
        Bw2Rgb555PixelFormat.X1R5G5B5LittleEndian => S("PropertyDetailRgb555PixelFormat"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatRgb555ChannelLayout(Bw2Rgb555PixelFormat pixelFormat) => pixelFormat switch
    {
        Bw2Rgb555PixelFormat.X1R5G5B5LittleEndian => S("PropertyDetailRgb555ChannelLayoutValue"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatRgb555HighBit(Bw2Rgb555Info info) =>
        string.Format(
            S("PropertyDetailRgb555HighBitValue"),
            info.SetHighBitPixelCount,
            info.PixelCount);

    private static string FormatPixelDepth(object value) => value switch
    {
        byte depth => $"{depth:N0}-bit",
        ushort depth => $"{depth:N0}-bit",
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatBmpDibHeader(Bw2BmpInfo info) =>
        string.Format(S("PropertyDetailBitmapInfoHeader"), info.DibHeaderSize);

    private static string FormatBmpCompression(Bw2BmpCompression compression) => compression switch
    {
        Bw2BmpCompression.Rgb => S("PropertyDetailBmpUncompressedRgb"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatBmpRowOrder(Bw2BmpRowOrder rowOrder) => rowOrder switch
    {
        Bw2BmpRowOrder.BottomUp => S("PropertyDetailBmpBottomUp"),
        Bw2BmpRowOrder.TopDown => S("PropertyDetailBmpTopDown"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatTgaImageType(Bw2TgaImageType imageType) => imageType switch
    {
        Bw2TgaImageType.UncompressedTrueColor => S("PropertyDetailTgaTrueColor"),
        Bw2TgaImageType.UncompressedGrayscale => S("PropertyDetailTgaGrayscale"),
        Bw2TgaImageType.RunLengthEncodedTrueColor => S("PropertyDetailTgaRleTrueColor"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatTgaOrigin(Bw2TgaVerticalOrigin origin) => origin switch
    {
        Bw2TgaVerticalOrigin.Bottom => S("PropertyDetailTgaOriginBottom"),
        Bw2TgaVerticalOrigin.Top => S("PropertyDetailTgaOriginTop"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatTgaPixelOrder(Bw2TgaHorizontalOrder order) => order switch
    {
        Bw2TgaHorizontalOrder.LeftToRight => S("PropertyDetailTgaLeftToRight"),
        Bw2TgaHorizontalOrder.RightToLeft => S("PropertyDetailTgaRightToLeft"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatTgaInterleaving(Bw2TgaInterleaving interleaving) => interleaving switch
    {
        Bw2TgaInterleaving.None => S("PropertyDetailTgaNoInterleaving"),
        Bw2TgaInterleaving.TwoWay => S("PropertyDetailTgaTwoWayInterleaving"),
        Bw2TgaInterleaving.FourWay => S("PropertyDetailTgaFourWayInterleaving"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatTgaColorMap(Bw2TgaInfo info) => info.HasColorMap
        ? string.Format(
            S("PropertyDetailTgaColorMapValue"),
            info.ColorMapEntryCount,
            info.ColorMapEntryDepth,
            info.ColorMapFirstEntry)
        : S("PropertyDetailNotPresent");

    private static string FormatTgaExtensionArea(Bw2TgaInfo info)
    {
        if (!info.HasExtensionArea)
            return S("PropertyDetailNotPresent");

        return info.UsesBw2ExtensionSizeCompatibility
            && info.ExtensionAreaStoredSize is { } storedSize
            && info.ExtensionAreaSize is { } declaredSize
            ? string.Format(
                S("PropertyDetailTgaExtensionAreaBw2Value"),
                storedSize,
                declaredSize)
            : info.ExtensionAreaStoredSize is { } size
                ? string.Format(S("PropertyDetailTgaExtensionAreaValue"), size)
            : S("PropertyDetailPresent");
    }

    private static string FormatDxgiFormat(Bw2DdsInfo info) =>
        info.DxgiFormat is { } format && info.DxgiFormatValue is { } value
            ? $"{format} ({value})"
            : S("PropertyDetailUnknown");

    private static string FormatTextureKind(Bw2DdsTextureKind kind) => kind switch
    {
        Bw2DdsTextureKind.Texture1D => S("PropertyDetailTexture1D"),
        Bw2DdsTextureKind.Texture2D => S("PropertyDetailTexture2D"),
        Bw2DdsTextureKind.Texture3D => S("PropertyDetailTexture3D"),
        Bw2DdsTextureKind.TextureArray => S("PropertyDetailTextureArray"),
        Bw2DdsTextureKind.Cubemap => S("PropertyDetailCubemap"),
        Bw2DdsTextureKind.CubemapArray => S("PropertyDetailCubemapArray"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatColorSpace(Bw2DdsColorSpace colorSpace) => colorSpace switch
    {
        Bw2DdsColorSpace.Linear => "Linear",
        Bw2DdsColorSpace.Srgb => "sRGB",
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatAlphaCapability(Bw2DdsAlphaCapability capability) => capability switch
    {
        Bw2DdsAlphaCapability.Yes => S("PropertyDetailYes"),
        Bw2DdsAlphaCapability.No => S("PropertyDetailNo"),
        _ => S("PropertyDetailUnknown")
    };

    private static string FormatNonOpaqueAlpha(Bw2DdsNonOpaqueAlphaStatus status) => status switch
    {
        Bw2DdsNonOpaqueAlphaStatus.Yes => S("PropertyDetailYes"),
        Bw2DdsNonOpaqueAlphaStatus.No => S("PropertyDetailNo"),
        Bw2DdsNonOpaqueAlphaStatus.NotApplicable => S("PropertyDetailNotApplicable"),
        _ => S("PropertyDetailUnknown")
    };

    private static string EmptyAsUnknown(string value) =>
        string.IsNullOrEmpty(value) ? S("PropertyDetailUnknown") : value;

    private static string EmptyAsDash(string value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string S(string key) => MainWindow.S(key);

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_sessionBounds is { } bounds)
        {
            Left = bounds.Left;
            Top = bounds.Top;
            Width = Math.Max(MinWidth, bounds.Width);
            Height = Math.Max(MinHeight, bounds.Height);
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ResetAllScrollPositions));
    }

    private void PropertiesTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, PropertiesTabs))
            return;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ResetSelectedTabScrollPosition));
    }

    private void ResetAllScrollPositions()
    {
        ResetScrollPosition(GeneralScrollViewer);
        ResetScrollPosition(FileDetailsScrollViewer);
        ResetScrollPosition(VisualTreeSearch.FindDescendant<ScrollViewer>(ContentsGrid));
        ResetScrollPosition(VisualTreeSearch.FindDescendant<ScrollViewer>(ReferencesGrid));
    }

    private void ResetSelectedTabScrollPosition()
    {
        switch (PropertiesTabs.SelectedIndex)
        {
            case 0:
                ResetScrollPosition(GeneralScrollViewer);
                break;
            case 1:
                ResetScrollPosition(FileDetailsScrollViewer);
                break;
            case 2:
                ResetScrollPosition(VisualTreeSearch.FindDescendant<ScrollViewer>(ContentsGrid));
                break;
            case 3:
                ResetScrollPosition(VisualTreeSearch.FindDescendant<ScrollViewer>(ReferencesGrid));
                break;
        }
    }

    private static void ResetScrollPosition(ScrollViewer? scrollViewer)
    {
        if (scrollViewer is null)
            return;

        scrollViewer.ScrollToTop();
        scrollViewer.ScrollToLeftEnd();
    }

    private void Window_Closing(object? sender, CancelEventArgs e) =>
        _sessionBounds = RestoreBounds;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        Close();
        e.Handled = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

internal enum FileDetailSection
{
    File,
    Archive,
    Image,
    PixelFormat,
    Storage,
    Model
}

internal sealed record FileDetailRow(
    int SectionOrder,
    string Section,
    bool StartsSection,
    string Label,
    string Value);
internal sealed record MaterialContentRow(
    uint MaterialIndex,
    string StoredName,
    string DiffuseMap,
    string LightMap,
    string GrowthMap,
    string SpecularMap,
    string AdditionalMap,
    string NormalMap);
internal sealed record ReferenceRow(
    uint MaterialIndex,
    string MaterialName,
    string Role,
    string ModelEntry,
    string StoredReference,
    string Status,
    string StatusDescription,
    string Candidates);
