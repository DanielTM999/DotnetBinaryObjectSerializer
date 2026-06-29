using System.Collections.Generic;

namespace DotnetBinaryObjectSerializer
{
    public interface IBinaryObjectEncoder
    {
        byte[] EncodeToByteArray<T>(T obj);
        IList<byte[]> EncodeToByteArrayList<T>(ICollection<T> objects);
    }
}
