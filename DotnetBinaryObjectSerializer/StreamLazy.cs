namespace DotnetBinaryObjectSerializer
{
    /// <summary>In-memory stream content. Use <see cref="Wrap"/> to avoid a copy.</summary>
    public sealed class StreamLazy : StreamContent
    {
        private StreamLazy(byte[] bytes, int offset, int length) : base(length,
            () => new MemoryStream(bytes, offset, length, writable: false, publiclyVisible: true)) { }

        public static StreamLazy Of(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            return new StreamLazy((byte[])bytes.Clone(), 0, bytes.Length);
        }

        public static StreamLazy Wrap(byte[] bytes) => Wrap(bytes, 0, bytes?.Length ?? 0);
        public static StreamLazy Wrap(byte[] bytes, int offset, int length)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            if (offset > bytes.Length - length) throw new ArgumentOutOfRangeException(nameof(length));
            return new StreamLazy(bytes, offset, length);
        }
    }
}
