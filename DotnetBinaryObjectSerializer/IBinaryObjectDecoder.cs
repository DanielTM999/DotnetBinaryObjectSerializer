namespace DotnetBinaryObjectSerializer
{
    public interface IBinaryObjectDecoder
    {
        IBinaryObjectNode ReadAsTree(byte[] bytes);
        IBinaryObjectNode ReadAsTree(FileInfo file);
        IBinaryObjectNode ReadAsTree(Stream stream);

        T ReadAsObject<T>(byte[] bytes);
        T ReadAsObject<T>(FileInfo file);
        T ReadAsObject<T>(Stream stream);

        C ReadAsCollection<C, T>(byte[] bytes) where C : ICollection<T>, new();
        C ReadAsCollection<C, T>(FileInfo file) where C : ICollection<T>, new();
        C ReadAsCollection<C, T>(Stream stream) where C : ICollection<T>, new();
    }
}
