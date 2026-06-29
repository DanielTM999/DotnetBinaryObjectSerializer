namespace DotnetBinaryObjectSerializer.Annotations
{
    
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ElementRef : Attribute
    {
         public string Value { get; }
         
         public ElementRef(string value) {
            Value = value;
        }
    }
}
