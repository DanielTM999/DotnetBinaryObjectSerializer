namespace DotnetBinaryObjectSerializer.Annotations
{
    /// <summary>Marks a <see cref="StreamContent"/> field for LARGE_CONTENT transfer.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class LargeContent : Attribute { }
}
