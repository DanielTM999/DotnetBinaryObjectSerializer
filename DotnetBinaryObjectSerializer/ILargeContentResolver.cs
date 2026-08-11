namespace DotnetBinaryObjectSerializer;

public interface ILargeContentResolver
{
    LargeContentDestination Resolve(LargeContentContext context);
}
