using DotnetBinaryObjectSerializer.Enums;

namespace DotnetBinaryObjectSerializer.Annotations
{
    
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class IgnoreElement : Attribute
    {
        public SerializationType[] Value { get; }
         
        public IgnoreElement()
        {
            Value =
            [
                SerializationType.ENCODE,
                SerializationType.DECODE
            ];
        }
    
        public IgnoreElement(params SerializationType[] value)
        {
            Value = value;
        }
    }
}
