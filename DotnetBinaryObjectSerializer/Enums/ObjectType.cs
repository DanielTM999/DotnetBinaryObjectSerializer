namespace DotnetBinaryObjectSerializer.Enums
{
    public enum ObjectType
    {
        String = 0x01,
        I8 = 0x02,
        I16 = 0x03,
        I32 = 0x04,
        I64 = 0x05,
        Boolean = 0x06,
        Double = 0x07,
        Float = 0x08,
        Object = 0x09,
        Bytes = 0x10,
        List = 0x11,
        Null = 0x12
    }
}
