using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using DotnetBinaryObjectSerializer.Enums;
using DotnetBinaryObjectSerializer.Annotations;
using DotnetBinaryObjectSerializer.Exceptions;
using DotnetBinaryObjectSerializer.Extensions;

namespace DotnetBinaryObjectSerializer.Mapper
{
    public class BinaryObjectEncoderMapper : BaseBinaryObjectSerializer, IBinaryObjectEncoder
    {
        private static readonly byte[] RootNameBytes = Encoding.UTF8.GetBytes("root");
        private static readonly byte[] EmptyNameBytes = Array.Empty<byte>();

        public IList<byte[]> EncodeToByteArrayList<T>(ICollection<T> objects)
        {
            var result = new List<byte[]>(objects.Count);
            foreach (var item in objects)
            {
                result.Add(EncodeToByteArray(item));
            }
            return result;
        }

        public byte[] EncodeToByteArray<T>(T obj)
        {
            if (obj == null) throw new EncodeSerializationException("object is null");

            try
            {
                var output = new BinaryOutput(EstimateInitialCapacity(obj));
                output.WriteByte(Constants.ValidatorByte);
                output.WriteByte(Constants.VersionByte);

                var payloadLengthPos = output.ReserveVarLong();
                var payloadStart = output.Position;
                Encode(output, obj, RootNameBytes);
                output.WriteVarLongAt(payloadLengthPos, output.Position - payloadStart);

                return output.ToByteArray();
            }
            catch (EncodeSerializationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new EncodeSerializationException("Failed to encode object", e);
            }
        }

        public Stream EncodeToStream<T>(T obj)
        {
            var path = Path.Combine(Path.GetTempPath(), "binary-object-" + Guid.NewGuid().ToString("N") + ".bin");
            var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read,
                64 * 1024, FileOptions.DeleteOnClose | FileOptions.SequentialScan);
            try { Encode(obj, output); output.Position = 0; return output; }
            catch { output.Dispose(); throw; }
        }

        public void Encode<T>(T obj, Stream destination)
        {
            ArgumentNullException.ThrowIfNull(destination);
            if (obj == null) throw new EncodeSerializationException("object is null");
            try
            {
                var payloadLength = Measure(obj!, RootNameBytes, true);
                var output = new StreamingOutput(destination);
                output.Byte(Constants.ValidatorByte);
                output.Byte(Constants.VersionByte);
                output.VarLong(payloadLength);
                var start = output.Position;
                WriteStreaming(output, obj!, RootNameBytes, true);
                if (output.Position - start != payloadLength)
                    throw new EncodeSerializationException("Object changed while it was being encoded");
            }
            catch (EncodeSerializationException) { throw; }
            catch (Exception e) { throw new EncodeSerializationException("Failed to encode stream", e); }
        }

        private long Measure(object? value, byte[] name, bool allowLarge)
        {
            if (value == null) return HeaderSize(name);
            if (value is StreamContent content)
            {
                if (!allowLarge && value is not StreamLazy) throw new EncodeSerializationException("StreamContent must be [LargeContent]");
                return VariableSize(name, content.Length);
            }
            return value switch
            {
                Enum e => VariableSize(name, Encoding.UTF8.GetByteCount(e.ToString())),
                byte[] b => VariableSize(name, b.LongLength),
                string s => VariableSize(name, Encoding.UTF8.GetByteCount(s)),
                bool or sbyte or byte => FixedSize(name, 1),
                short => FixedSize(name, 2),
                int or uint or float => FixedSize(name, 4),
                long or ulong or double => FixedSize(name, 8),
                char or decimal or BigInteger => VariableSize(name, Encoding.UTF8.GetByteCount(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!)),
                Array a => MeasureArray(a, name),
                IDictionary d => MeasureMap(d, name),
                IEnumerable e => MeasureEnumerable(e, name),
                IBinaryObjectNode n => MeasureNode(n, name),
                _ => MeasureObject(value, name)
            };
        }

        private long MeasureObject(object value, byte[] name)
        {
            long body = 0;
            foreach (var field in ResolveFields(value.GetType(), SerializationType.ENCODE))
                body = checked(body + Measure(field.Field.GetValue(value), field.ElementNameBytes,
                    field.Field.GetCustomAttributes(typeof(LargeContent), false).Length != 0));
            return VariableSize(name, body);
        }
        private long MeasureArray(Array values, byte[] name)
        {
            if (values is byte[] bytes) return VariableSize(name, bytes.LongLength);
            long body = 0; foreach (var value in values) body = checked(body + Measure(value, EmptyNameBytes, false));
            return VariableSize(name, body);
        }
        private long MeasureEnumerable(IEnumerable values, byte[] name)
        { long body = 0; foreach (var value in values) body = checked(body + Measure(value, EmptyNameBytes, false)); return VariableSize(name, body); }
        private long MeasureMap(IDictionary values, byte[] name)
        { long body = 0; var i = 0; foreach (DictionaryEntry e in values) { var key = Encoding.UTF8.GetBytes(e.Key?.ToString() ?? (i++).ToString()); body = checked(body + Measure(e.Value, key, false)); } return VariableSize(name, body); }
        private long MeasureNode(IBinaryObjectNode node, byte[] name)
        {
            if (node.ObjectType == ObjectType.LargeContent) return VariableSize(name, node.AsStreamContent().Length);
            if (node.ObjectType is ObjectType.Object or ObjectType.List) { long body = 0; foreach (var c in node.Children) body = checked(body + MeasureNode(c, Encoding.UTF8.GetBytes(c.Name))); return VariableSize(name, body); }
            return node.ObjectType == ObjectType.Null ? HeaderSize(name) : IsVariable(node.ObjectType) ? VariableSize(name, node.BodyLength) : FixedSize(name, node.BodyLength);
        }

        private void WriteStreaming(StreamingOutput o, object? value, byte[] name, bool allowLarge)
        {
            if (value == null) { Header(o, ObjectType.Null, name); return; }
            if (value is StreamContent content) { if (!allowLarge && value is not StreamLazy) throw new EncodeSerializationException("StreamContent must be [LargeContent]"); Header(o, ObjectType.LargeContent, name); o.VarLong(content.Length); using var s = content.OpenStream(); o.CopyExactly(s, content.Length); return; }
            switch (value)
            {
                case Enum e: WriteString(o, e.ToString(), name); return;
                case byte[] b: Header(o, ObjectType.Bytes, name); o.VarLong(b.LongLength); o.Write(b); return;
                case string s: WriteString(o, s, name); return;
                case bool b: Header(o, ObjectType.Boolean, name); o.Byte(b ? 1 : 0); return;
                case sbyte b: Header(o, ObjectType.I8, name); o.Byte(b); return;
                case byte b: Header(o, ObjectType.I8, name); o.Byte(b); return;
                case short n: Header(o, ObjectType.I16, name); o.Short(n); return;
                case int n: Header(o, ObjectType.I32, name); o.Int(n); return;
                case uint n: Header(o, ObjectType.I32, name); o.Int(unchecked((int)n)); return;
                case long n: Header(o, ObjectType.I64, name); o.Long(n); return;
                case ulong n: Header(o, ObjectType.I64, name); o.Long(unchecked((long)n)); return;
                case float n: Header(o, ObjectType.Float, name); o.Int(BitConverter.SingleToInt32Bits(n)); return;
                case double n: Header(o, ObjectType.Double, name); o.Long(BitConverter.DoubleToInt64Bits(n)); return;
                case char or decimal or BigInteger: WriteString(o, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!, name); return;
                case Array a: WriteArrayStreaming(o, a, name); return;
                case IDictionary d: WriteMapStreaming(o, d, name); return;
                case IEnumerable e: WriteEnumerableStreaming(o, e, name); return;
                case IBinaryObjectNode n: WriteNodeStreaming(o, n, name); return;
                default: WriteObjectStreaming(o, value, name); return;
            }
        }
        private void WriteString(StreamingOutput o, string value, byte[] name) { var b = Encoding.UTF8.GetBytes(value); Header(o, ObjectType.String, name); o.VarLong(b.LongLength); o.Write(b); }
        private void WriteObjectStreaming(StreamingOutput o, object value, byte[] name) { var body = BodyFromTotal(MeasureObject(value, name), name); Header(o, ObjectType.Object, name); o.VarLong(body); foreach (var f in ResolveFields(value.GetType(), SerializationType.ENCODE)) WriteStreaming(o, f.Field.GetValue(value), f.ElementNameBytes, f.Field.GetCustomAttributes(typeof(LargeContent), false).Length != 0); }
        private void WriteArrayStreaming(StreamingOutput o, Array values, byte[] name) { if (values is byte[] b) { Header(o, ObjectType.Bytes, name); o.VarLong(b.LongLength); o.Write(b); return; } var body = BodyFromTotal(MeasureArray(values, name), name); Header(o, ObjectType.List, name); o.VarLong(body); foreach (var v in values) WriteStreaming(o, v, EmptyNameBytes, false); }
        private void WriteEnumerableStreaming(StreamingOutput o, IEnumerable values, byte[] name) { var body = BodyFromTotal(MeasureEnumerable(values, name), name); Header(o, ObjectType.List, name); o.VarLong(body); foreach (var v in values) WriteStreaming(o, v, EmptyNameBytes, false); }
        private void WriteMapStreaming(StreamingOutput o, IDictionary values, byte[] name) { var body = BodyFromTotal(MeasureMap(values, name), name); Header(o, ObjectType.Object, name); o.VarLong(body); var i = 0; foreach (DictionaryEntry e in values) WriteStreaming(o, e.Value, Encoding.UTF8.GetBytes(e.Key?.ToString() ?? (i++).ToString()), false); }
        private void WriteNodeStreaming(StreamingOutput o, IBinaryObjectNode n, byte[] name) { Header(o, n.ObjectType, name); if (n.ObjectType == ObjectType.Null) return; if (n.ObjectType == ObjectType.LargeContent) { var c = n.AsStreamContent(); o.VarLong(c.Length); using var s = c.OpenStream(); o.CopyExactly(s, c.Length); return; } if (n.ObjectType is ObjectType.Object or ObjectType.List) { o.VarLong(BodyFromTotal(MeasureNode(n, name), name)); foreach (var c in n.Children) WriteNodeStreaming(o, c, Encoding.UTF8.GetBytes(c.Name)); return; } var b = n.AsBytes(); if (IsVariable(n.ObjectType)) o.VarLong(b.LongLength); o.Write(b); }
        private static void Header(StreamingOutput o, ObjectType type, byte[] name) { o.Byte(type.Id()); o.VarInt(name.Length); o.Write(name); }
        private static long HeaderSize(byte[] n) => 1L + VarIntLength(n.Length) + n.Length;
        private static long FixedSize(byte[] n, long body) => checked(HeaderSize(n) + body);
        private static long VariableSize(byte[] n, long body) => checked(HeaderSize(n) + VarLongLength(body) + body);
        private static long BodyFromTotal(long total, byte[] n) { var without = total - HeaderSize(n); for (var i = 1; i <= 10; i++) { var body = without - i; if (body >= 0 && VarLongLength(body) == i) return body; } throw new EncodeSerializationException("Invalid calculated body size"); }
        private static bool IsVariable(ObjectType t) => t is ObjectType.String or ObjectType.Object or ObjectType.List or ObjectType.Bytes or ObjectType.LargeContent;
        private static int VarIntLength(int v) { var r=1; while ((v & ~0x7F)!=0) {r++; v=(int)((uint)v>>7);} return r; }
        private static int VarLongLength(long v) { if(v<0) throw new EncodeSerializationException("Negative length"); var r=1; while((v & ~0x7FL)!=0){r++; v=(long)((ulong)v>>7);} return r; }

        private void Encode(BinaryOutput output, object value, byte[] fieldNameBytes)
        {
            if (value == null)
            {
                WriteNull(output, fieldNameBytes);
                return;
            }

            switch (value)
            {
                case Enum e:
                    WriteString(output, e.ToString(), fieldNameBytes);
                    break;
                case bool b:
                    WriteBoolean(output, b, fieldNameBytes);
                    break;
                case sbyte sb:
                    WriteInt8(output, sb, fieldNameBytes);
                    break;
                case byte by:
                    WriteInt8(output, unchecked((sbyte)by), fieldNameBytes);
                    break;
                case byte[] bytes:
                    WriteBytes(output, bytes, fieldNameBytes);
                    break;
                case StreamContent:
                    throw new EncodeSerializationException("StreamContent fields must be marked with [LargeContent]");
                case string s:
                    WriteString(output, s, fieldNameBytes);
                    break;
                case short sh:
                    WriteInt16(output, sh, fieldNameBytes);
                    break;
                case ushort us:
                    WriteInt32(output, us, fieldNameBytes);
                    break;
                case int i:
                    WriteInt32(output, i, fieldNameBytes);
                    break;
                case uint ui:
                    WriteInt64(output, ui, fieldNameBytes);
                    break;
                case long l:
                    WriteInt64(output, l, fieldNameBytes);
                    break;
                case ulong ul:
                    WriteInt64(output, unchecked((long)ul), fieldNameBytes);
                    break;
                case float f:
                    WriteFloat(output, f, fieldNameBytes);
                    break;
                case double d:
                    WriteDouble(output, d, fieldNameBytes);
                    break;
                case char c:
                    WriteString(output, c.ToString(), fieldNameBytes);
                    break;
                case decimal dec:
                    WriteString(output, dec.ToString(System.Globalization.CultureInfo.InvariantCulture), fieldNameBytes);
                    break;
                case BigInteger bi:
                    WriteString(output, bi.ToString(System.Globalization.CultureInfo.InvariantCulture), fieldNameBytes);
                    break;
                case Array array:
                    WriteArray(output, array, fieldNameBytes);
                    break;
                case IDictionary map:
                    WriteMap(output, map, fieldNameBytes);
                    break;
                case IBinaryObjectNode node:
                    WriteBinaryObjectNode(output, node, fieldNameBytes);
                    break;
                case IEnumerable enumerable:
                    WriteList(output, enumerable, fieldNameBytes);
                    break;
                default:
                    WriteObject(output, value, fieldNameBytes);
                    break;
            }
        }

        private void WriteNull(BinaryOutput output, byte[] fieldNameBytes)
        {
            WriteHeader(output, ObjectType.Null, fieldNameBytes);
        }

        private void WriteString(BinaryOutput output, string value, byte[] fieldNameBytes)
        {
            if (value == null)
            {
                WriteNull(output, fieldNameBytes);
                return;
            }

            var valueBytes = Encoding.UTF8.GetBytes(value);
            WriteHeader(output, ObjectType.String, fieldNameBytes);
            output.WriteVarInt(valueBytes.Length);
            output.Write(valueBytes);
        }

        private void WriteInt8(BinaryOutput output, sbyte value, byte[] fieldNameBytes)
        {
            WriteHeader(output, ObjectType.I8, fieldNameBytes);
            output.WriteByte(value);
        }

        private void WriteInt16(BinaryOutput output, short value, byte[] fieldNameBytes)
        {
            WriteHeader(output, ObjectType.I16, fieldNameBytes);
            output.WriteShort(value);
        }

        private void WriteInt32(BinaryOutput output, int value, byte[] fieldNameBytes)
        {
            WriteHeader(output, ObjectType.I32, fieldNameBytes);
            output.WriteInt(value);
        }

        private void WriteInt64(BinaryOutput output, long value, byte[] fieldNameBytes)
        {
            WriteHeader(output, ObjectType.I64, fieldNameBytes);
            output.WriteLong(value);
        }

        private void WriteBoolean(BinaryOutput output, bool value, byte[] fieldNameBytes)
        {
            WriteHeader(output, ObjectType.Boolean, fieldNameBytes);
            output.WriteBoolean(value);
        }

        private void WriteDouble(BinaryOutput output, double value, byte[] fieldNameBytes)
        {
            WriteHeader(output, ObjectType.Double, fieldNameBytes);
            output.WriteDouble(value);
        }

        private void WriteFloat(BinaryOutput output, float value, byte[] fieldNameBytes)
        {
            WriteHeader(output, ObjectType.Float, fieldNameBytes);
            output.WriteFloat(value);
        }

        private void WriteBytes(BinaryOutput output, byte[] value, byte[] fieldNameBytes)
        {
            if (value == null)
            {
                WriteNull(output, fieldNameBytes);
                return;
            }

            WriteHeader(output, ObjectType.Bytes, fieldNameBytes);
            output.WriteVarInt(value.Length);
            output.Write(value);
        }

        private void WriteObject(BinaryOutput output, object obj, byte[] fieldNameBytes)
        {
            WriteHeader(output, ObjectType.Object, fieldNameBytes);
            var payloadLengthPos = output.ReserveVarInt();
            var payloadStart = output.Position;

            var fields = ResolveFields(obj.GetType(), SerializationType.ENCODE);
            foreach (var field in fields)
            {
                var value = field.Field.GetValue(obj);
                if (value == null)
                {
                    WriteNull(output, field.ElementNameBytes);
                    continue;
                }
                if (field.Field.GetCustomAttributes(typeof(LargeContent), false).Length != 0)
                {
                    if (value is not StreamContent content)
                        throw new EncodeSerializationException($"[LargeContent] field '{field.Field.Name}' must be StreamContent");
                    WriteLargeContent(output, content, field.ElementNameBytes);
                    continue;
                }
                Encode(output, value, field.ElementNameBytes);
            }

            output.WriteVarIntAt(payloadLengthPos, output.Position - payloadStart);
        }

        private void WriteLargeContent(BinaryOutput output, StreamContent content, byte[] fieldNameBytes)
        {
            if (content.Length > int.MaxValue)
                throw new EncodeSerializationException("LARGE_CONTENT exceeds the current 2 GB protocol buffer limit");

            WriteHeader(output, ObjectType.LargeContent, fieldNameBytes);
            output.WriteVarInt((int)content.Length);
            using var source = content.OpenStream();
            var buffer = new byte[64 * 1024];
            long remaining = content.Length;
            while (remaining > 0)
            {
                var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read == 0) throw new EncodeSerializationException("LARGE_CONTENT stream ended before its declared length");
                output.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        private void WriteArray(BinaryOutput output, Array array, byte[] fieldNameBytes)
        {
            WriteHeader(output, ObjectType.List, fieldNameBytes);
            var payloadLengthPos = output.ReserveVarInt();
            var payloadStart = output.Position;

            foreach (var element in array)
            {
                Encode(output, element, EmptyNameBytes);
            }

            output.WriteVarIntAt(payloadLengthPos, output.Position - payloadStart);
        }

        private void WriteList(BinaryOutput output, IEnumerable list, byte[] fieldNameBytes)
        {
            WriteHeader(output, ObjectType.List, fieldNameBytes);
            var payloadLengthPos = output.ReserveVarInt();
            var payloadStart = output.Position;

            foreach (var element in list)
            {
                Encode(output, element, EmptyNameBytes);
            }

            output.WriteVarIntAt(payloadLengthPos, output.Position - payloadStart);
        }

        private void WriteMap(BinaryOutput output, IDictionary map, byte[] fieldNameBytes)
        {
            WriteHeader(output, ObjectType.Object, fieldNameBytes);
            var payloadLengthPos = output.ReserveVarInt();
            var payloadStart = output.Position;

            var i = 0;
            foreach (DictionaryEntry entry in map)
            {
                var key = entry.Key?.ToString() ?? i.ToString();
                Encode(output, entry.Value, Encoding.UTF8.GetBytes(key));
                i++;
            }

            output.WriteVarIntAt(payloadLengthPos, output.Position - payloadStart);
        }

        private void WriteBinaryObjectNode(BinaryOutput output, IBinaryObjectNode node, byte[] fieldNameBytes)
        {
            var objectType = node.ObjectType;
            if (objectType == ObjectType.LargeContent)
            {
                WriteLargeContent(output, node.AsStreamContent(), fieldNameBytes);
                return;
            }
            var dataBytes = node.AsBytes() ?? Array.Empty<byte>();

            WriteHeader(output, objectType, fieldNameBytes);

            switch (objectType)
            {
                case ObjectType.String:
                case ObjectType.Object:
                case ObjectType.List:
                case ObjectType.Bytes:
                    output.WriteVarInt(dataBytes.Length);
                    output.Write(dataBytes);
                    break;
                case ObjectType.Null:
                    break;
                case ObjectType.Boolean:
                case ObjectType.I8:
                    WriteFixedNodeBytes(output, dataBytes, 1, objectType);
                    break;
                case ObjectType.I16:
                    WriteFixedNodeBytes(output, dataBytes, 2, objectType);
                    break;
                case ObjectType.I32:
                case ObjectType.Float:
                    WriteFixedNodeBytes(output, dataBytes, 4, objectType);
                    break;
                case ObjectType.I64:
                case ObjectType.Double:
                    WriteFixedNodeBytes(output, dataBytes, 8, objectType);
                    break;
            }
        }

        private void WriteFixedNodeBytes(BinaryOutput output, byte[] dataBytes, int size, ObjectType objectType)
        {
            if (dataBytes.Length != size)
            {
                throw new EncodeSerializationException(
                    $"Invalid byte array length for {objectType}: expected {size}, got {dataBytes.Length}");
            }
            output.Write(dataBytes);
        }

        private void WriteHeader(BinaryOutput output, ObjectType objectType, byte[] fieldNameBytes)
        {
            output.WriteByte(objectType.Id());
            output.WriteVarInt(fieldNameBytes.Length);
            output.Write(fieldNameBytes);
        }

        private static int EstimateInitialCapacity(object obj)
        {
            switch (obj)
            {
                case byte[] bytes:
                    return bytes.Length + 32;
                case string s:
                    return Math.Max(64, s.Length * 3 + 32);
                case ICollection collection:
                    return Math.Max(128, collection.Count * 32);
                default:
                    return 512;
            }
        }

        private sealed class StreamingOutput
        {
            private readonly Stream _destination;
            public StreamingOutput(Stream destination) => _destination = destination;
            public long Position { get; private set; }
            public void Byte(int value) { _destination.WriteByte((byte)value); Position++; }
            public void Short(int value) { Byte(value >> 8); Byte(value); }
            public void Int(int value) { Byte(value >> 24); Byte(value >> 16); Byte(value >> 8); Byte(value); }
            public void Long(long value) { for (var shift = 56; shift >= 0; shift -= 8) Byte((int)(value >> shift)); }
            public void VarInt(int value) { if (value < 0) throw new EncodeSerializationException("Negative varint"); var v=(uint)value; while ((v & ~0x7Fu)!=0) { Byte((int)((v&0x7F)|0x80)); v >>= 7; } Byte((int)v); }
            public void VarLong(long value) { if(value<0) throw new EncodeSerializationException("Negative varlong"); var v=(ulong)value; while((v&~0x7Ful)!=0){Byte((int)((v&0x7F)|0x80));v>>=7;}Byte((int)v); }
            public void Write(byte[] bytes) { _destination.Write(bytes, 0, bytes.Length); Position += bytes.Length; }
            public void CopyExactly(Stream source, long length) { var b=new byte[64*1024]; while(length>0){var r=source.Read(b,0,(int)Math.Min(b.Length,length));if(r==0)throw new EncodeSerializationException("LARGE_CONTENT stream ended early");_destination.Write(b,0,r);Position+=r;length-=r;} }
        }

        private sealed class BinaryOutput
        {
            private byte[] _buffer;
            private int _size;

            public BinaryOutput(int initialCapacity)
            {
                _buffer = new byte[Math.Max(32, initialCapacity)];
            }

            public int Position => _size;

            public void WriteByte(int value)
            {
                EnsureCapacity(_size + 1);
                _buffer[_size++] = (byte)value;
            }

            public void WriteBoolean(bool value) => WriteByte(value ? 1 : 0);

            public void WriteShort(int value)
            {
                EnsureCapacity(_size + 2);
                _buffer[_size++] = (byte)(value >> 8);
                _buffer[_size++] = (byte)value;
            }

            public void WriteInt(int value)
            {
                EnsureCapacity(_size + 4);
                WriteIntAt(_size, value);
                _size += 4;
            }

            public void WriteLong(long value)
            {
                EnsureCapacity(_size + 8);
                WriteLongAt(_size, value);
                _size += 8;
            }

            public void WriteFloat(float value) => WriteInt(BitConverter.SingleToInt32Bits(value));

            public void WriteDouble(double value) => WriteLong(BitConverter.DoubleToInt64Bits(value));

            public void WriteVarInt(int value)
            {
                if (value < 0) throw new EncodeSerializationException("Negative varint value: " + value);
                var v = (uint)value;
                while ((v & ~0x7Fu) != 0)
                {
                    WriteByte((int)((v & 0x7F) | 0x80));
                    v >>= 7;
                }
                WriteByte((int)v);
            }

            public int ReserveVarInt() => Reserve(1);

            public void WriteVarIntAt(int pos, int value)
            {
                if (value < 0) throw new EncodeSerializationException("Negative varint value: " + value);
                var length = VarIntLength(value);
                ReplaceReserved(pos, 1, length);
                WriteVarIntBytesAt(pos, value);
            }

            public int ReserveVarLong() => Reserve(5);

            public void WriteVarLongAt(int pos, long value)
            {
                if (value < 0) throw new EncodeSerializationException("Negative varlong value: " + value);
                var length = VarLongLength(value);
                ReplaceReserved(pos, 5, length);
                WriteVarLongBytesAt(pos, value);
            }

            public void Write(byte[] bytes)
            {
                if (bytes.Length == 0) return;
                EnsureCapacity(_size + bytes.Length);
                Array.Copy(bytes, 0, _buffer, _size, bytes.Length);
                _size += bytes.Length;
            }

            public void Write(byte[] bytes, int offset, int length)
            {
                if (length == 0) return;
                EnsureCapacity(_size + length);
                Array.Copy(bytes, offset, _buffer, _size, length);
                _size += length;
            }

            public byte[] ToByteArray()
            {
                var result = new byte[_size];
                Array.Copy(_buffer, result, _size);
                return result;
            }

            private void WriteIntAt(int pos, int value)
            {
                _buffer[pos] = (byte)(value >> 24);
                _buffer[pos + 1] = (byte)(value >> 16);
                _buffer[pos + 2] = (byte)(value >> 8);
                _buffer[pos + 3] = (byte)value;
            }

            private void WriteLongAt(int pos, long value)
            {
                _buffer[pos] = (byte)(value >> 56);
                _buffer[pos + 1] = (byte)(value >> 48);
                _buffer[pos + 2] = (byte)(value >> 40);
                _buffer[pos + 3] = (byte)(value >> 32);
                _buffer[pos + 4] = (byte)(value >> 24);
                _buffer[pos + 5] = (byte)(value >> 16);
                _buffer[pos + 6] = (byte)(value >> 8);
                _buffer[pos + 7] = (byte)value;
            }

            private int Reserve(int length)
            {
                EnsureCapacity(_size + length);
                var pos = _size;
                _size += length;
                return pos;
            }

            private static int VarIntLength(int value)
            {
                var v = (uint)value;
                var i = 0;
                while ((v & ~0x7Fu) != 0)
                {
                    i++;
                    v >>= 7;
                }
                return i + 1;
            }

            private static int VarLongLength(long value)
            {
                var v = (ulong)value;
                var i = 0;
                while ((v & ~0x7Ful) != 0)
                {
                    i++;
                    v >>= 7;
                }
                return i + 1;
            }

            private void WriteVarIntBytesAt(int pos, int value)
            {
                var v = (uint)value;
                while ((v & ~0x7Fu) != 0)
                {
                    _buffer[pos++] = (byte)((v & 0x7F) | 0x80);
                    v >>= 7;
                }
                _buffer[pos] = (byte)v;
            }

            private void WriteVarLongBytesAt(int pos, long value)
            {
                var v = (ulong)value;
                while ((v & ~0x7Ful) != 0)
                {
                    _buffer[pos++] = (byte)((v & 0x7F) | 0x80);
                    v >>= 7;
                }
                _buffer[pos] = (byte)v;
            }

            private void ReplaceReserved(int pos, int reservedLength, int encodedLength)
            {
                var tailStart = pos + reservedLength;
                var newTailStart = pos + encodedLength;
                var tailLength = _size - tailStart;
                var delta = encodedLength - reservedLength;

                if (delta > 0) EnsureCapacity(_size + delta);

                if (tailLength > 0 && newTailStart != tailStart)
                {
                    Array.Copy(_buffer, tailStart, _buffer, newTailStart, tailLength);
                }
                _size += delta;
            }

            private void EnsureCapacity(int minCapacity)
            {
                if (minCapacity <= _buffer.Length) return;
                var newCapacity = _buffer.Length + (_buffer.Length >> 1);
                if (newCapacity < minCapacity) newCapacity = minCapacity;
                Array.Resize(ref _buffer, newCapacity);
            }
        }
    }
}
