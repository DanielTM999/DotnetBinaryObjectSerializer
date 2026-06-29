using DotnetBinaryObjectSerializer.Enums;

namespace DotnetBinaryObjectSerializer.Annotations
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class IgnoreSuperClass : Attribute
    {
        public SerializationType[] Value { get; }
        
        public IgnoreSuperClass()
        {
            Value =
            [
                SerializationType.ENCODE,
                SerializationType.DECODE
            ];
        }
    
        public IgnoreSuperClass(params SerializationType[] value)
        {
            Value = value;
        }
        
    }
}
