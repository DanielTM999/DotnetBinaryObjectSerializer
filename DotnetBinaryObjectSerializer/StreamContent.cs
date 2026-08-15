namespace DotnetBinaryObjectSerializer
{
    /// <summary>Content that can be read without requiring it to remain in one byte array.</summary>
    public class StreamContent : IDisposable
    {
        private readonly Func<Stream> _source;
        private readonly Action _cleanup;
        private bool _disposed;

        protected StreamContent(long length, Func<Stream> source, Action? cleanup = null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            Length = length;
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _cleanup = cleanup ?? (() => { });
        }

        public long Length { get; }
        public static StreamContent Of(long length, Func<Stream> source) => new(length, source);
        public static StreamContent From(FileInfo file) => new(file.Length, file.OpenRead);

        public virtual Stream OpenStream()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _source() ?? throw new IOException("StreamContent source returned null");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cleanup();
        }
    }
}
