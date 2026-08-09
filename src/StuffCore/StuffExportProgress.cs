namespace StuffCore;

public sealed record StuffExportProgress(
    int CompletedEntries,
    int TotalEntries,
    long CompletedBytes,
    long TotalBytes,
    string CurrentPath)
{
    public double Percentage => TotalBytes > 0
        ? Math.Min(100, CompletedBytes * 100d / TotalBytes)
        : TotalEntries == 0 ? 100 : Math.Min(100, CompletedEntries * 100d / TotalEntries);
}
