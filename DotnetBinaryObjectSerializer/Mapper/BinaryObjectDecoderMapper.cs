using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;
using DotnetBinaryObjectSerializer.Enums;
using DotnetBinaryObjectSerializer.Exceptions;
using DotnetBinaryObjectSerializer.Extensions;
using DotnetBinaryObjectSerializer.Mapper.Models;


namespace DotnetBinaryObjectSerializer.Mapper
{
    public class BinaryObjectDecoderMapper : BinaryObjectEncoderMapper, IBinaryObjectDecoder
    {
        public IBinaryObjectNode ReadAsTree(byte[] bytes)
        {
            if (bytes == null) throw new DecodeSerializationException("bytes is null");

            try
            {
                var root = NewNode();
                var input = new BinaryInput(bytes);

                ValidateValidatorByte(input);
                ValidateVersion(input);

                var payloadSize = input.ReadPayloadLength();
                if (payloadSize < 0 || payloadSize > int.MaxValue)
                    throw new DecodeSerializationException("Invalid payload size: " + payloadSize);

                var payloadEnd = input.Position + (int)payloadSize;
                if (payloadEnd > bytes.Length)
                    throw new DecodeSerializationException(
                        $"Protocol corrupted: payload incomplete, expected {payloadSize} bytes");

                ReadNode(input, root, payloadEnd);

                if (input.Position < payloadEnd)
                    throw new DecodeSerializationException(
                        $"Invalid serialization: extra bytes remaining after root node ({payloadEnd - input.Position} bytes)");

                return root;
            }
            catch (DecodeSerializationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new DecodeSerializationException("Failed to read bytes", e);
            }
        }

        public IBinaryObjectNode ReadAsTree(FileInfo file)
        {
            try
            {
                return ReadAsTree(File.ReadAllBytes(file.FullName));
            }
            catch (IOException e)
            {
                throw new DecodeSerializationException("Failed to read file", e);
            }
        }

        public IBinaryObjectNode ReadAsTree(Stream stream)
        {
            try
            {
                return ReadAsTree(ReadFrame(stream));
            }
            catch (IOException e)
            {
                throw new DecodeSerializationException("Failed to read stream", e);
            }
        }

        public T ReadAsObject<T>(byte[] bytes) => (T)ConvertNode(typeof(T), ReadAsTree(bytes));
        public T ReadAsObject<T>(FileInfo file) => (T)ConvertNode(typeof(T), ReadAsTree(file));
        public T ReadAsObject<T>(Stream stream) => (T)ConvertNode(typeof(T), ReadAsTree(stream));

        public C ReadAsCollection<C, T>(byte[] bytes) where C : ICollection<T>, new()
            => (C)ConvertNode(typeof(C), ReadAsTree(bytes));

        public C ReadAsCollection<C, T>(FileInfo file) where C : ICollection<T>, new()
            => (C)ConvertNode(typeof(C), ReadAsTree(file));

        public C ReadAsCollection<C, T>(Stream stream) where C : ICollection<T>, new()
            => (C)ConvertNode(typeof(C), ReadAsTree(stream));

        private DefaultBinaryObjectNode NewNode() => new DefaultBinaryObjectNode(ConvertNode);

        private void ReadNode(BinaryInput input, DefaultBinaryObjectNode node, int payloadLimit)
        {
            ReadNodeMetadata(input, node);
            ReadNodeBody(input, node, payloadLimit);
        }

        private void ReadNodeMetadata(BinaryInput input, DefaultBinaryObjectNode node)
        {
            var typeByte = input.ReadByte();
            var objectType = ObjectTypeExtensions.FromId(typeByte);
            if (objectType == null)
                throw new DecodeSerializationException(
                    $"Unknown object type: byte=0x{typeByte:X2} ({typeByte})");

            node.SetObjectType(objectType.Value);

            var nameSize = input.ReadLength();
            if (nameSize < 0) throw new DecodeSerializationException("Invalid name size: " + nameSize);
            node.SetName(input.ReadString(nameSize));
        }

        private void ReadNodeBody(BinaryInput input, DefaultBinaryObjectNode node, int payloadLimit)
        {
            var bodySize = GetBodySize(input, node.ObjectType);
            if (bodySize < 0) throw new SerializationException("Invalid body size: " + bodySize);

            var bodyStart = input.Position;
            var bodyEnd = bodyStart + bodySize;
            if (bodyEnd < bodyStart || bodyEnd > payloadLimit)
                throw new DecodeSerializationException(
                    $"Protocol corrupted: node body incomplete, expected {bodySize} bytes");

            switch (node.ObjectType)
            {
                case ObjectType.String:
                case ObjectType.I64:
                case ObjectType.I32:
                case ObjectType.I16:
                case ObjectType.I8:
                case ObjectType.Boolean:
                case ObjectType.Double:
                case ObjectType.Float:
                case ObjectType.Bytes:
                case ObjectType.Null:
                    node.SetBytesValue(input.Bytes, bodyStart, bodySize);
                    input.Skip(bodySize);
                    break;

                case ObjectType.Object:
                case ObjectType.List:
                    node.SetBytesValue(input.Bytes, bodyStart, bodySize);
                    while (input.Position < bodyEnd)
                    {
                        var child = NewNode();
                        ReadNode(input, child, bodyEnd);
                        node.AddChild(child);
                    }
                    break;

                default:
                    throw new DecodeSerializationException("Unsupported object type in body: " + node.ObjectType);
            }

            if (input.Position != bodyEnd)
                throw new DecodeSerializationException(
                    $"Invalid serialization: node body size mismatch for '{node.Name}'");
        }

        private static int GetBodySize(BinaryInput input, ObjectType objectType)
        {
            switch (objectType)
            {
                case ObjectType.String:
                case ObjectType.Object:
                case ObjectType.List:
                case ObjectType.Bytes:
                    return input.ReadLength();
                case ObjectType.Null:
                    return 0;
                case ObjectType.Boolean:
                case ObjectType.I8:
                    return 1;
                case ObjectType.I16:
                    return 2;
                case ObjectType.I32:
                case ObjectType.Float:
                    return 4;
                case ObjectType.I64:
                case ObjectType.Double:
                    return 8;
                default:
                    return Constants.InvalidSize;
            }
        }

        private object? ConvertNode(Type type, IBinaryObjectNode node)
        {
            if (node.ObjectType == ObjectType.Null)
                return type.IsValueType ? Activator.CreateInstance(type) : null;

            if (type == typeof(object))
                return ConvertDynamic(node);

            if (IsSimpleType(type))
                return ConvertSimple(type, node);

            if (type.IsArray)
                return ConvertArray(type, node);

            if (IsDictionaryType(type))
                return ConvertMap(type, node);

            if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
                return BuildCollection(type, ElementTypeOf(type), node);

            return ConvertObject(type, node);
        }

        private object? ConvertDynamic(IBinaryObjectNode node)
        {
            switch (node.ObjectType)
            {
                case ObjectType.String: return node.AsString();
                case ObjectType.Boolean: return node.AsBoolean();
                case ObjectType.I8: return (sbyte)(node.AsInt() ?? 0);
                case ObjectType.I16: return (short)(node.AsInt() ?? 0);
                case ObjectType.I32: return node.AsInt();
                case ObjectType.I64: return node.AsLong();
                case ObjectType.Float: return node.AsFloat();
                case ObjectType.Double: return node.AsDouble();
                case ObjectType.Bytes: return node.AsBytes();
                case ObjectType.List:
                    var list = new List<object>();
                    foreach (var child in node.Children) list.Add(ConvertNode(typeof(object), child));
                    return list;
                case ObjectType.Object: return node.AsMap();
                default: return null;
            }
        }

        private object ConvertSimple(Type type, IBinaryObjectNode node)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;

            if (t.IsEnum) return Enum.Parse(t, node.AsString());
            if (t == typeof(string)) return node.AsString();
            if (t == typeof(bool)) return node.AsBoolean() ?? false;
            if (t == typeof(int)) return node.AsInt() ?? 0;
            if (t == typeof(short)) return (short)(node.AsInt() ?? 0);
            if (t == typeof(long)) return node.AsLong() ?? 0L;
            if (t == typeof(float)) return node.AsFloat();
            if (t == typeof(double)) return node.AsDouble();
            if (t == typeof(char)) { var s = node.AsString(); return s.Length > 0 ? s[0] : '\0'; }
            if (t == typeof(decimal)) return decimal.Parse(node.AsString(), CultureInfo.InvariantCulture);
            if (t == typeof(BigInteger)) return BigInteger.Parse(node.AsString(), CultureInfo.InvariantCulture);
            if (t == typeof(ushort)) return (ushort)(node.AsInt() ?? 0);
            if (t == typeof(uint)) return (uint)(node.AsLong() ?? 0L);
            if (t == typeof(ulong)) return (ulong)(node.AsLong() ?? 0L);

            if (t == typeof(byte))
            {
                if (node.ObjectType == ObjectType.Bytes) { var b = node.AsBytes(); return b.Length > 0 ? b[0] : (byte)0; }
                return (byte)(node.AsInt() ?? 0);
            }
            if (t == typeof(sbyte))
            {
                if (node.ObjectType == ObjectType.Bytes) { var b = node.AsBytes(); return b.Length > 0 ? unchecked((sbyte)b[0]) : (sbyte)0; }
                return (sbyte)(node.AsInt() ?? 0);
            }

            throw new DecodeSerializationException("Unsupported simple type: " + t.FullName);
        }

        private object ConvertArray(Type type, IBinaryObjectNode node)
        {
            if (type == typeof(byte[])) return node.AsBytes();

            var elementType = type.GetElementType();
            var children = node.Children;
            var array = Array.CreateInstance(elementType, children.Count);
            for (var i = 0; i < children.Count; i++)
            {
                array.SetValue(ConvertNode(elementType, children[i]), i);
            }
            return array;
        }

        private object ConvertMap(Type type, IBinaryObjectNode node)
        {
            var valueType = typeof(object);
            if (type.IsGenericType)
            {
                var args = type.GetGenericArguments();
                if (args.Length >= 2) valueType = args[1];
            }

            var dict = (IDictionary)InstantiateDictionary(type, valueType);
            foreach (var child in node.Children)
            {
                dict[child.Name] = ConvertNode(valueType, child);
            }
            return dict;
        }

        private object ConvertObject(Type type, IBinaryObjectNode node)
        {
            var instance = CreateInstance(type);
            var fields = ResolveFieldMap(type, SerializationType.DECODE);

            foreach (var entry in fields)
            {
                var props = entry.Value;
                var child = node.GetChild(props.ElementName);
                if (child == null) continue;

                var value = ConvertNode(props.FieldType, child);
                props.Field.SetValue(instance, value);
            }

            return instance;
        }

        private object BuildCollection(Type collectionType, Type elementType, IBinaryObjectNode node)
        {
            var collection = InstantiateCollection(collectionType, elementType);
            var addMethod = collection.GetType().GetMethod("Add", new[] { elementType });

            foreach (var child in node.Children)
            {
                var value = ConvertNode(elementType, child);
                if (addMethod != null) addMethod.Invoke(collection, new[] { value });
                else if (collection is IList list) list.Add(value);
                else throw new DecodeSerializationException(
                    $"Collection type '{collectionType.FullName}' has no usable Add method");
            }
            return collection;
        }

        private static object InstantiateCollection(Type collectionType, Type elementType)
        {
            if (collectionType.IsInterface)
            {
                var concrete = collectionType.IsGenericType
                               && collectionType.GetGenericTypeDefinition() == typeof(ISet<>)
                    ? typeof(HashSet<>).MakeGenericType(elementType)
                    : typeof(List<>).MakeGenericType(elementType);
                return Activator.CreateInstance(concrete);
            }
            return Activator.CreateInstance(collectionType);
        }

        private static object InstantiateDictionary(Type dictionaryType, Type valueType)
        {
            if (dictionaryType.IsInterface)
            {
                return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType));
            }
            return Activator.CreateInstance(dictionaryType);
        }

        private static object CreateInstance(Type type)
        {
            try
            {
                return Activator.CreateInstance(type, nonPublic: true);
            }
            catch (Exception e)
            {
                throw new DecodeSerializationException(
                    $"Failed to instantiate class '{type.FullName}'. It must have a parameterless constructor.", e);
            }
        }

        private static bool IsDictionaryType(Type type)
        {
            if (typeof(IDictionary).IsAssignableFrom(type)) return true;
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)) return true;
            }
            return false;
        }

        private static Type ElementTypeOf(Type type)
        {
            if (type.IsGenericType) return type.GetGenericArguments()[0];
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return i.GetGenericArguments()[0];
            }
            return typeof(object);
        }

        private static void ValidateValidatorByte(BinaryInput input)
        {
            var validator = input.ReadByte();
            if (validator != (byte)Constants.ValidatorByte)
                throw new DecodeSerializationException("Invalid protocol: missing validator byte 0xAA");
        }

        private static void ValidateVersion(BinaryInput input)
        {
            var version = input.ReadByte();
            if (version != Constants.VersionByte && version != Constants.LegacyVersionByte)
                throw new DecodeSerializationException("Unsupported protocol version: " + version);
            input.UseCompactLengths(version == Constants.VersionByte);
        }

        private static byte[] ReadFrame(Stream stream)
        {
            var validator = stream.ReadByte();
            if (validator == -1) throw new StreamEndException("Stream ended before validator byte");
            if ((byte)validator != (byte)Constants.ValidatorByte)
                throw new StreamEndException(
                    $"Invalid protocol: missing validator byte 0xAA (got 0x{validator:X2})");

            var version = stream.ReadByte();
            if (version == -1) throw new StreamEndException("Stream ended before version byte");
            if ((byte)version != Constants.VersionByte && (byte)version != Constants.LegacyVersionByte)
                throw new StreamEndException("Unsupported protocol version: " + version);

            var header = new List<byte> { (byte)validator, (byte)version };

            long payloadLength;
            if ((byte)version == Constants.VersionByte)
            {
                payloadLength = ReadVarLongFromStream(stream, header);
            }
            else
            {
                var lenBytes = ReadExactly(stream, 8);
                header.AddRange(lenBytes);
                payloadLength = ((long)lenBytes[0] << 56) | ((long)lenBytes[1] << 48)
                    | ((long)lenBytes[2] << 40) | ((long)lenBytes[3] << 32)
                    | ((long)lenBytes[4] << 24) | ((long)lenBytes[5] << 16)
                    | ((long)lenBytes[6] << 8) | lenBytes[7];
            }

            if (payloadLength < 0 || payloadLength > int.MaxValue)
                throw new StreamEndException("Invalid payload size: " + payloadLength);

            var payload = ReadExactly(stream, (int)payloadLength);

            var frame = new byte[header.Count + payload.Length];
            header.CopyTo(frame, 0);
            Array.Copy(payload, 0, frame, header.Count, payload.Length);
            return frame;
        }

        private static long ReadVarLongFromStream(Stream stream, List<byte> sink)
        {
            long value = 0;
            var shift = 0;
            for (var i = 0; i < 10; i++)
            {
                var b = stream.ReadByte();
                if (b == -1) throw new StreamEndException("Stream ended in payload length varlong");
                sink.Add((byte)b);
                value |= (long)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return value;
                shift += 7;
            }
            throw new StreamEndException("Invalid varlong length");
        }

        private static byte[] ReadExactly(Stream stream, int n)
        {
            var buffer = new byte[n];
            var read = 0;
            while (read < n)
            {
                var r = stream.Read(buffer, read, n - read);
                if (r == 0) throw new StreamEndException($"Stream ended early: expected {n} bytes, got {read}");
                read += r;
            }
            return buffer;
        }

        private sealed class BinaryInput
        {
            private readonly byte[] _bytes;
            private int _position;
            private bool _compactLengths;

            public BinaryInput(byte[] bytes) => _bytes = bytes;

            public byte[] Bytes => _bytes;
            public int Position => _position;

            public void UseCompactLengths(bool compactLengths) => _compactLengths = compactLengths;

            public int ReadLength() => _compactLengths ? ReadVarInt() : ReadInt();

            public long ReadPayloadLength() => _compactLengths ? ReadVarLong() : ReadLong();

            public byte ReadByte()
            {
                Require(1);
                return _bytes[_position++];
            }

            public int ReadInt()
            {
                Require(4);
                var value = (_bytes[_position] << 24) | (_bytes[_position + 1] << 16)
                    | (_bytes[_position + 2] << 8) | _bytes[_position + 3];
                _position += 4;
                return value;
            }

            public long ReadLong()
            {
                Require(8);
                long value = ((long)_bytes[_position] << 56) | ((long)_bytes[_position + 1] << 48)
                    | ((long)_bytes[_position + 2] << 40) | ((long)_bytes[_position + 3] << 32)
                    | ((long)_bytes[_position + 4] << 24) | ((long)_bytes[_position + 5] << 16)
                    | ((long)_bytes[_position + 6] << 8) | _bytes[_position + 7];
                _position += 8;
                return value;
            }

            public int ReadVarInt()
            {
                var value = 0;
                var shift = 0;
                for (var i = 0; i < 5; i++)
                {
                    var b = ReadByte();
                    value |= (b & 0x7F) << shift;
                    if ((b & 0x80) == 0) return value;
                    shift += 7;
                }
                throw new DecodeSerializationException("Invalid varint length");
            }

            public long ReadVarLong()
            {
                long value = 0;
                var shift = 0;
                for (var i = 0; i < 10; i++)
                {
                    var b = ReadByte();
                    value |= (long)(b & 0x7F) << shift;
                    if ((b & 0x80) == 0) return value;
                    shift += 7;
                }
                throw new DecodeSerializationException("Invalid varlong length");
            }

            public string ReadString(int length)
            {
                if (length < 0) throw new DecodeSerializationException("Invalid string length: " + length);
                Require(length);
                var value = Encoding.UTF8.GetString(_bytes, _position, length);
                _position += length;
                return value;
            }

            public void Skip(int length)
            {
                if (length < 0) throw new DecodeSerializationException("Invalid skip length: " + length);
                Require(length);
                _position += length;
            }

            private void Require(int length)
            {
                if (_position + length < _position || _position + length > _bytes.Length)
                    throw new DecodeSerializationException("Unexpected end of input");
            }
        }
    }
}
