namespace DotnetBinaryObjectSerializer;

public sealed record LargeContentContext(IReadOnlyList<string> FieldPath, string FieldName, long Length, Type? OwnerType);
