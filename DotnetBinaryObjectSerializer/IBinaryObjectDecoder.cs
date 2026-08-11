namespace DotnetBinaryObjectSerializer
{
    public interface IBinaryObjectDecoder
    {
        IBinaryObjectNode ReadAsTree(byte[] bytes);
        IBinaryObjectNode ReadAsTree(FileInfo file);
        IBinaryObjectNode ReadAsTree(Stream stream);
        IBinaryObjectNode ReadAsTree(byte[] bytes, DecodeOptions options);
        IBinaryObjectNode ReadAsTree(FileInfo file, DecodeOptions options);
        IBinaryObjectNode ReadAsTree(Stream stream, DecodeOptions options);
        IBinaryObjectNode ReadAsTreeWithOptions(byte[] bytes, DecodeOptions options);
        IBinaryObjectNode ReadAsTreeWithOptions(FileInfo file, DecodeOptions options);
        IBinaryObjectNode ReadAsTreeWithOptions(Stream stream, DecodeOptions options);

        T ReadAsObject<T>(byte[] bytes);
        T ReadAsObject<T>(FileInfo file);
        T ReadAsObject<T>(Stream stream);
        T ReadAsObject<T>(byte[] bytes, DecodeOptions options);
        T ReadAsObject<T>(FileInfo file, DecodeOptions options);
        T ReadAsObject<T>(Stream stream, DecodeOptions options);

        C ReadAsCollection<C, T>(byte[] bytes) where C : ICollection<T>, new();
        C ReadAsCollection<C, T>(FileInfo file) where C : ICollection<T>, new();
        C ReadAsCollection<C, T>(Stream stream) where C : ICollection<T>, new();
    }
}
