using System.Collections.Generic;

namespace DotnetBinaryObjectSerializer
{
    public interface IBinaryObjectEncoder
    {
        byte[] EncodeToByteArray<T>(T obj);
        Stream EncodeToStream<T>(T obj);
        void Encode<T>(T obj, Stream destination);
        IList<byte[]> EncodeToByteArrayList<T>(ICollection<T> objects);
    }
}
