using StuffCore;

namespace StuffExplorer;

internal sealed class AssetEntryViewModel
{
    public AssetEntryViewModel(StuffEntry entry, Bw2AssetClassification classification)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        Classification = classification ?? throw new ArgumentNullException(nameof(classification));
        TypeDisplay = AssetDisplayNames.GetFileType(Classification);
        CategoryDisplay = AssetDisplayNames.GetCategory(Classification.Category);
    }

    public StuffEntry Entry { get; }
    public Bw2AssetClassification Classification { get; }
    public string TypeDisplay { get; }
    public string CategoryDisplay { get; }

    public string Path => Entry.Path;
    public string Name => Entry.Name;
    public string DirectoryPath => Entry.DirectoryPath;
    public string Extension => Entry.Extension;
    public uint Offset => Entry.Offset;
    public uint Length => Entry.Length;
    public uint ModifiedTimestamp => Entry.ModifiedTimestamp;
    public DateTime ModifiedLocalTime => Entry.ModifiedLocalTime;
    public DateTimeOffset ModifiedUtc => Entry.ModifiedUtc;
}
