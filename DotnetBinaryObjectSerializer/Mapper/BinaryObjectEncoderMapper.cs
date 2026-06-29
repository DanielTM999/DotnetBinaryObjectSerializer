using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using DotnetBinaryObjectSerializer.Enums;
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
                Encode(output, value, field.ElementNameBytes);
            }

            output.WriteVarIntAt(payloadLengthPos, output.Position - payloadStart);
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
