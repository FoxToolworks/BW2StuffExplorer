using System.Buffers.Binary;
using System.Text;
using StuffCore;

var tempRoot = Path.Combine(Path.GetTempPath(), $"stuffcore-test-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempRoot);

try
{
    TestValidArchive(tempRoot);
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
