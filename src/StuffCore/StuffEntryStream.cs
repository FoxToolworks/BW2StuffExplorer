namespace StuffCore;

internal sealed class StuffEntryStream : Stream
{
    private readonly FileStream _archiveStream;
    private readonly long _start;
    private readonly long _length;
    private long _position;

    public StuffEntryStream(string archivePath, long start, long length)
    {
        _archiveStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _start = start;
        _length = length;
        _archiveStream.Position = start;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_archiveStream.SafeFileHandle.IsClosed, this);
        var remaining = _length - _position;
        if (remaining <= 0)
            return 0;

        var read = _archiveStream.Read(buffer, offset, (int)Math.Min(count, remaining));
        _position += read;
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var remaining = _length - _position;
        if (remaining <= 0)
            return 0;

        var read = _archiveStream.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]);
        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (target < 0 || target > _length)
            throw new IOException("Attempted to seek outside the archive entry.");

        _position = target;
        _archiveStream.Position = _start + target;
        return _position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _archiveStream.Dispose();
        base.Dispose(disposing);
    }
}
