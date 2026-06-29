using DotnetBinaryObjectSerializer.Enums;

namespace DotnetBinaryObjectSerializer.Extensions
{
    public static class ObjectTypeExtensions
    {
        public static byte Id(this ObjectType type)
        {
            return (byte)type;
        }
        
        public static ObjectType? FromId(byte id)
        {
            return id switch
            {
                0x01 => ObjectType.String,
                0x02 => ObjectType.I8,
                0x03 => ObjectType.I16,
                0x04 => ObjectType.I32,
                0x05 => ObjectType.I64,
                0x06 => ObjectType.Boolean,
                0x07 => ObjectType.Double,
                0x08 => ObjectType.Float,
                0x09 => ObjectType.Object,
                0x10 => ObjectType.Bytes,
                0x11 => ObjectType.List,
                0x12 => ObjectType.Null,
                _ => null
            };
        }
        
    }
}