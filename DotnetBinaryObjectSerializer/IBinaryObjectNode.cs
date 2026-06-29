using System.Collections.Generic;
using DotnetBinaryObjectSerializer.Enums;

namespace DotnetBinaryObjectSerializer
{
    public interface IBinaryObjectNode
    {
        ObjectType ObjectType { get; }
        string Name { get; }

        IList<IBinaryObjectNode> Children { get; }

        IBinaryObjectNode GetChild(string key);
        IBinaryObjectNode GetChild(int index);

        string AsString();
        long? AsLong();
        int? AsInt();
        bool? AsBoolean();
        byte[] AsBytes();
        float AsFloat();
        double AsDouble();

        T AsObject<T>();
        C AsCollection<C, T>() where C : ICollection<T>, new();

        IDictionary<string, object> AsMap();
        IDictionary<string, byte[]> AsByteMap();
        IDictionary<string, IBinaryObjectNode> AsBinaryObjectNodeMap();
    }
}
