using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace StuffCore;

public sealed class StuffArchive
{
    public const int EntrySize = 268;
    public const int PathFieldSize = 256;
    private const int CopyBufferSize = 1024 * 1024;

    private StuffArchive(string filePath, long fileLength, uint contentLength, IReadOnlyList<StuffEntry> entries)
    {
        FilePath = filePath;
        FileLength = fileLength;
        ContentLength = contentLength;
        Entries = entries;
        _entrySet = entries.ToHashSet();
    }

    private readonly HashSet<StuffEntry> _entrySet;

    public string FilePath { get; }
    public long FileLength { get; }
    public uint ContentLength { get; }
    public IReadOnlyList<StuffEntry> Entries { get; }

    public static StuffArchive Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        if (stream.Length < sizeof(uint))
            throw new StuffArchiveException("The file is too small to be a STUFF archive.");

        Span<byte> uintBuffer = stackalloc byte[sizeof(uint)];
        stream.Position = stream.Length - sizeof(uint);
        ReadExactly(stream, uintBuffer);
        var contentLength = BinaryPrimitives.ReadUInt32LittleEndian(uintBuffer);

        if (contentLength > stream.Length - sizeof(uint))
            throw new StuffArchiveException("The table-of-contents offset lies outside the archive.");

        var dictionaryByteLength = stream.Length - contentLength - sizeof(uint);
        if (dictionaryByteLength % EntrySize != 0)
            throw new StuffArchiveException("The table of contents is not aligned to 268-byte entries.");

        var entryCount = dictionaryByteLength / EntrySize;
        if (entryCount > int.MaxValue)
            throw new StuffArchiveException("The archive contains too many entries.");

        var entries = new List<StuffEntry>((int)entryCount);
        var entryBuffer = new byte[EntrySize];
        stream.Position = contentLength;

        for (var index = 0; index < entryCount; index++)
        {
            ReadExactly(stream, entryBuffer);
            var entry = ParseEntry(entryBuffer, index);

            var end = (ulong)entry.Offset + entry.Length;
            if (end > contentLength)
                throw new StuffArchiveException($"Entry {index} ('{entry.Path}') points outside the content area.");

            entries.Add(entry);
        }

        return new StuffArchive(fullPath, stream.Length, contentLength, entries);
    }

    public Stream OpenEntry(StuffEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        EnsureEntryBelongsToArchive(entry);
        return new StuffEntryStream(FilePath, entry.Offset, entry.Length);
    }

    public void ExtractEntry(StuffEntry entry, string destinationFile, bool overwrite = false)
    {
        ExtractEntry(entry, destinationFile, overwrite, progress: null, CancellationToken.None);
    }

    public void ExtractEntry(
        StuffEntry entry,
        string destinationFile,
        bool overwrite,
        IProgress<StuffExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFile);
        EnsureEntryBelongsToArchive(entry);

        var fullDestination = Path.GetFullPath(destinationFile);
        ExtractPreparedEntry(
            entry,
            fullDestination,
            overwrite,
            completedEntries: 0,
            totalEntries: 1,
            completedBytes: 0,
            totalBytes: entry.Length,
            progress,
            cancellationToken);
    }

    public int ExtractEntries(IEnumerable<StuffEntry> entries, string destinationDirectory, bool overwrite = false)
    {
        return ExtractEntries(entries, destinationDirectory, overwrite, progress: null, CancellationToken.None);
    }

    public int ExtractEntries(
        IEnumerable<StuffEntry> entries,
        string destinationDirectory,
        bool overwrite,
        IProgress<StuffExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var root = Path.GetFullPath(destinationDirectory);
        var plan = CreateExtractionPlan(entries, root);
        Directory.CreateDirectory(root);
        var totalBytes = plan.Sum(item => (long)item.Entry.Length);
        var count = 0;
        long completedBytes = 0;

        foreach (var item in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExtractPreparedEntry(
                item.Entry,
                item.Destination,
                overwrite,
                count,
                plan.Count,
                completedBytes,
                totalBytes,
                progress,
                cancellationToken);
            count++;
            completedBytes += item.Entry.Length;
        }

        return count;
    }

    public int CountExistingDestinations(IEnumerable<StuffEntry> entries, string destinationDirectory)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var root = Path.GetFullPath(destinationDirectory);
        return CreateExtractionPlan(entries, root).Count(item => File.Exists(item.Destination));
    }

    private List<ExtractionPlanItem> CreateExtractionPlan(IEnumerable<StuffEntry> entries, string root)
    {
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plan = new List<ExtractionPlanItem>();

        foreach (var entry in entries)
        {
            EnsureEntryBelongsToArchive(entry);
            var relativePath = GetSafeRelativePath(entry.Path);
            var destination = Path.GetFullPath(Path.Combine(root, relativePath));

            if (!destination.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new StuffArchiveException($"Unsafe archive path: '{entry.Path}'.");
            if (!destinations.Add(destination))
                throw new StuffArchiveException($"Multiple archive entries map to the same destination: '{entry.Path}'.");

            plan.Add(new ExtractionPlanItem(entry, destination));
        }

        return plan;
    }

    private void ExtractPreparedEntry(
        StuffEntry entry,
        string destination,
        bool overwrite,
        int completedEntries,
        int totalEntries,
        long completedBytes,
        long totalBytes,
        IProgress<StuffExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(destination);
        var temporaryFile = Path.Combine(
            directory ?? Path.GetDirectoryName(Path.GetFullPath(destination)) ?? Directory.GetCurrentDirectory(),
            $".{Path.GetFileName(destination)}.bw2stuff-{Guid.NewGuid():N}.tmp");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            if (!overwrite && File.Exists(destination))
                throw new IOException($"The destination file already exists: '{destination}'.");

            progress?.Report(new StuffExportProgress(
                completedEntries,
                totalEntries,
                completedBytes,
                totalBytes,
                entry.Path));

            using (var source = OpenEntry(entry))
            using (var target = new FileStream(temporaryFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
                try
                {
                    long entryBytes = 0;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var read = source.Read(buffer, 0, buffer.Length);
                        if (read == 0)
                            break;

                        target.Write(buffer, 0, read);
                        entryBytes += read;
                        progress?.Report(new StuffExportProgress(
                            completedEntries,
                            totalEntries,
                            completedBytes + entryBytes,
                            totalBytes,
                            entry.Path));
                    }

                    if (entryBytes != entry.Length)
                        throw new EndOfStreamException(
                            $"The archive ended while reading '{entry.Path}' (expected {entry.Length} bytes, read {entryBytes}).");
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryFile, destination, overwrite);
            progress?.Report(new StuffExportProgress(
                completedEntries + 1,
                totalEntries,
                completedBytes + entry.Length,
                totalBytes,
                entry.Path));
        }
        catch (OperationCanceledException)
        {
            var cleanupError = TryDeleteTemporaryFile(temporaryFile);
            if (cleanupError is not null)
                throw new StuffArchiveException(
                    $"Export was cancelled, but the incomplete temporary file could not be removed: '{temporaryFile}'.",
                    cleanupError);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var cleanupError = TryDeleteTemporaryFile(temporaryFile);
            if (cleanupError is not null)
                throw new StuffArchiveException(
                    $"Could not export '{entry.Path}' to '{destination}', and the incomplete temporary file could not be removed: '{temporaryFile}'.",
                    new AggregateException(exception, cleanupError));
            throw new StuffArchiveException($"Could not export '{entry.Path}' to '{destination}': {exception.Message}", exception);
        }
    }

    private static Exception? TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception;
        }
    }

    private static StuffEntry ParseEntry(ReadOnlySpan<byte> bytes, long index)
    {
        var pathBytes = bytes[..PathFieldSize];
        var terminator = pathBytes.IndexOf((byte)0);
        if (terminator >= 0)
            pathBytes = pathBytes[..terminator];

        var path = Encoding.Latin1.GetString(pathBytes).Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(path))
            throw new StuffArchiveException($"Entry {index} has an empty path.");

        var offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(256, 4));
        var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(260, 4));
        var modifiedTimestamp = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(264, 4));
        return new StuffEntry(path, offset, length, modifiedTimestamp);
    }

    private static string GetSafeRelativePath(string archivePath)
    {
        var normalized = archivePath.Replace('\\', '/').TrimStart('/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 || parts.Any(IsUnsafePathPart))
            throw new StuffArchiveException($"Unsafe archive path: '{archivePath}'.");

        return Path.Combine(parts);
    }

    private static bool IsUnsafePathPart(string part)
    {
        if (part is "." or ".." || part.EndsWith(' ') || part.EndsWith('.'))
            return true;
        if (part.Any(character => character < 32 || "<>:\"|?*".Contains(character)))
            return true;

        var deviceName = part.Split('.')[0];
        return deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (deviceName.Length == 4
                && (deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && deviceName[3] is >= '1' and <= '9');
    }

    private void EnsureEntryBelongsToArchive(StuffEntry entry)
    {
        if (!_entrySet.Contains(entry))
            throw new ArgumentException("The entry does not belong to this archive.", nameof(entry));
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read == 0)
                throw new EndOfStreamException("Unexpected end of STUFF archive.");
            total += read;
        }
    }

    private sealed record ExtractionPlanItem(StuffEntry Entry, string Destination);
}
