using DotnetBinaryObjectSerializer.Mapper;
using DotnetBinaryObjectSerializer.Annotations;
using Xunit;

namespace DotnetBinaryObjectSerializer.Tests;

public class StreamFeaturesTest
{
    private readonly BinaryObjectEncoderMapper _encoder = new();
    private readonly BinaryObjectDecoderMapper _decoder = new();

    public sealed class LargeDocument
    {
        [LargeContent]
        public StreamContent Content;
    }

    [Fact]
    public void EncodeToStream_ProducesAReadableProtocolStream()
    {
        using var encoded = _encoder.EncodeToStream(new byte[] { 3, 1, 4, 1, 5 });
        var decoded = _decoder.ReadAsObject<byte[]>(encoded);

        Assert.Equal(new byte[] { 3, 1, 4, 1, 5 }, decoded);
    }

    [Fact]
    public void DeserializeOnDemand_KeepsTerminalBytesUnmaterializedUntilTheyAreRead()
    {
        var encoded = _encoder.EncodeToByteArray(new byte[] { 9, 8, 7 });
        using var tree = _decoder.ReadAsTree(encoded, DecodeOptions.Default.WithDeserializeOnDemand(true));

        using var content = tree.OpenStream();
        var bytes = new byte[3];
        Assert.Equal(3, content.Read(bytes));
        Assert.Equal(new byte[] { 9, 8, 7 }, bytes);
    }

    [Fact]
    public void StreamLazy_ExposesOwnedByteBufferAsStream()
    {
        using var content = StreamLazy.Wrap(new byte[] { 2, 4, 6 });
        using var stream = content.OpenStream();
        var bytes = new byte[3];

        Assert.Equal(3, stream.Read(bytes));
        Assert.Equal(new byte[] { 2, 4, 6 }, bytes);
    }

    [Fact]
    public void LargeContent_RoundTripsAsStreamContent()
    {
        var source = new LargeDocument { Content = StreamLazy.Wrap(new byte[] { 10, 20, 30, 40 }) };
        var encoded = _encoder.EncodeToByteArray(source);
        Assert.Contains((byte)0x13, encoded);

        var decoded = _decoder.ReadAsObject<LargeDocument>(encoded);
        using var stream = decoded.Content.OpenStream();
        var bytes = new byte[4];
        Assert.Equal(4, stream.Read(bytes));
        Assert.Equal(new byte[] { 10, 20, 30, 40 }, bytes);
    }
}
