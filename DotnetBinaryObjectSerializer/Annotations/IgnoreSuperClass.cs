using DotnetBinaryObjectSerializer.Enums;

namespace DotnetBinaryObjectSerializer.Annotations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class IgnoreSuperClassAttribute : Attribute
    {
        public SerializationType[] Value { get; }
        
        public IgnoreSuperClassAttribute()
        {
            Value =
            [
                SerializationType.ENCODE,
                SerializationType.DECODE
            ];
        }
    
        public IgnoreSuperClassAttribute(params SerializationType[] value)
        {
            Value = value;
        }
        
    }
}
