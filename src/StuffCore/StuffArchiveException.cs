namespace StuffCore;

public sealed class StuffArchiveException : Exception
{
    public StuffArchiveException(string message) : base(message) { }

    public StuffArchiveException(string message, Exception innerException) : base(message, innerException) { }
}
