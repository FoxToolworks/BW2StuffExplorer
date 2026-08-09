namespace StuffCore;

public sealed record StuffEntry(
    string Path,
    uint Offset,
    uint Length,
    uint ModifiedTimestamp)
{
    public string Name => System.IO.Path.GetFileName(Path.Replace('\\', '/'));

    public string DirectoryPath
    {
        get
        {
            var normalized = Path.Replace('\\', '/');
            var separator = normalized.LastIndexOf('/');
            return separator < 0 ? string.Empty : normalized[..separator];
        }
    }

    public string Extension => System.IO.Path.GetExtension(Name).TrimStart('.').ToUpperInvariant();

    public DateTime ModifiedLocalTime => DateTimeOffset
        .FromUnixTimeSeconds(ModifiedTimestamp)
        .LocalDateTime;

    public DateTimeOffset ModifiedUtc => DateTimeOffset.FromUnixTimeSeconds(ModifiedTimestamp);
}
