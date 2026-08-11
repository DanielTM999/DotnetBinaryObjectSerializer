namespace DotnetBinaryObjectSerializer
{
    /// <summary>Controls tree decoding behavior.</summary>
    public sealed record DecodeOptions(ILargeContentResolver? LargeContentResolver = null, bool DeserializeOnDemand = false)
    {
        public static readonly DecodeOptions Default = new();

        /// <summary>
        /// Keeps a terminal byte body connected to its input stream until it is
        /// read through <see cref="IBinaryObjectNode.OpenStream"/>, materialized,
        /// or disposed. This only affects ReadAsTree.
        /// </summary>
        public DecodeOptions WithDeserializeOnDemand(bool value) => this with { DeserializeOnDemand = value };
        public DecodeOptions WithLargeContentResolver(ILargeContentResolver? value) => this with { LargeContentResolver = value };
    }
}
