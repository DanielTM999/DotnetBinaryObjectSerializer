using System;
using System.Collections.Generic;
using System.Text;
using DotnetBinaryObjectSerializer.Enums;
using DotnetBinaryObjectSerializer.Exceptions;

namespace DotnetBinaryObjectSerializer.Mapper.Models
{
    public sealed class DefaultBinaryObjectNode : IBinaryObjectNode
    {
        private readonly Func<Type, IBinaryObjectNode, object> _convert;

        private byte[] _bytesValue;
        private byte[] _sourceBytes;
        private int _sourceOffset;
        private int _sourceLength;

        private readonly List<IBinaryObjectNode> _children = new();
        private Dictionary<string, IBinaryObjectNode> _childrenByName;

        public DefaultBinaryObjectNode(Func<Type, IBinaryObjectNode, object> convert)
        {
            _convert = convert;
        }

        public ObjectType ObjectType { get; private set; }
        public string Name { get; private set; }

        public IList<IBinaryObjectNode> Children => _children;

        public IBinaryObjectNode GetChild(string key)
        {
            if (key == null || _children.Count == 0) return null;

            if (_children.Count > 4)
            {
                if (_childrenByName == null)
                {
                    var index = new Dictionary<string, IBinaryObjectNode>(_children.Count);
                    foreach (var child in _children) index[child.Name] = child;
                    _childrenByName = index;
                }
                return _childrenByName.TryGetValue(key, out var found) ? found : null;
            }

            foreach (var child in _children)
            {
                if (child.Name == key) return child;
            }
            return null;
        }

        public IBinaryObjectNode GetChild(int index)
        {
            if (index < 0 || index >= _children.Count) return null;
            return _children[index];
        }

        public string AsString()
        {
            if (ObjectType != ObjectType.String)
                throw new DecodeSerializationException($"Node '{Name}' is not STRING, but {ObjectType}");

            var length = ValueLength();
            return length > 0 ? Encoding.UTF8.GetString(ValueSource(), ValueOffset(), length) : "";
        }

        public long? AsLong()
        {
            if (ObjectType != ObjectType.I64 && ObjectType != ObjectType.I32
                && ObjectType != ObjectType.I16 && ObjectType != ObjectType.I8)
                throw new DecodeSerializationException($"Node '{Name}' is not LONG/INT, but {ObjectType}");

            if (ObjectType != ObjectType.I64) return AsInt();

            if (ValueLength() != 8)
                throw new DecodeSerializationException($"Invalid byte array length for LONG at node '{Name}': {ValueLength()}");
            return ReadLongValue();
        }

        public int? AsInt()
        {
            if (ObjectType != ObjectType.I32 && ObjectType != ObjectType.I16 && ObjectType != ObjectType.I8)
                throw new DecodeSerializationException($"Node '{Name}' is not INT, but {ObjectType}");

            var length = ValueLength();
            if (ObjectType == ObjectType.I8 && length == 1) return ByteAt(0);
            if (ObjectType == ObjectType.I16 && length == 2) return ReadShortValue();
            if (length != 4)
                throw new DecodeSerializationException($"Invalid byte array length for INT at node '{Name}': {length}");
            return ReadIntValue();
        }

        public bool? AsBoolean()
        {
            if (ObjectType != ObjectType.Boolean)
                throw new DecodeSerializationException($"Node '{Name}' is not BOOLEAN, but {ObjectType}");
            if (ValueLength() != 1)
                throw new DecodeSerializationException($"Invalid byte array length for BOOLEAN at node '{Name}': {ValueLength()}");
            return ByteAt(0) != 0;
        }

        public byte[] AsBytes() => MaterializedBytes();

        public float AsFloat()
        {
            if (ObjectType != ObjectType.Float)
                throw new DecodeSerializationException($"Node '{Name}' is not FLOAT, but {ObjectType}");
            if (ValueLength() != 4)
                throw new DecodeSerializationException($"Invalid byte array length for FLOAT at node '{Name}': {ValueLength()}");
            return BitConverter.Int32BitsToSingle(ReadIntValue());
        }

        public double AsDouble()
        {
            if (ObjectType != ObjectType.Double)
                throw new DecodeSerializationException($"Node '{Name}' is not DOUBLE, but {ObjectType}");
            if (ValueLength() != 8)
                throw new DecodeSerializationException($"Invalid byte array length for DOUBLE at node '{Name}': {ValueLength()}");
            return BitConverter.Int64BitsToDouble(ReadLongValue());
        }

        public T AsObject<T>()
        {
            try
            {
                return (T)_convert(typeof(T), this);
            }
            catch (DecodeSerializationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new DecodeSerializationException(e);
            }
        }

        public C AsCollection<C, T>() where C : ICollection<T>, new()
        {
            try
            {
                return (C)_convert(typeof(C), this);
            }
            catch (DecodeSerializationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new DecodeSerializationException(e);
            }
        }

        public IDictionary<string, object> AsMap()
        {
            var map = new Dictionary<string, object>();
            foreach (var node in _children)
            {
                switch (node.ObjectType)
                {
                    case ObjectType.Object:
                        map[node.Name] = node.AsMap();
                        break;
                    case ObjectType.String:
                        map[node.Name] = node.AsString();
                        break;
                    case ObjectType.I8:
                        map[node.Name] = (sbyte)(node.AsInt() ?? 0);
                        break;
                    case ObjectType.I16:
                        map[node.Name] = (short)(node.AsInt() ?? 0);
                        break;
                    case ObjectType.I32:
                        map[node.Name] = node.AsInt() ?? 0;
                        break;
                    case ObjectType.I64:
                        map[node.Name] = node.AsLong() ?? 0;
                        break;
                    case ObjectType.Float:
                        map[node.Name] = node.AsFloat();
                        break;
                    case ObjectType.Double:
                        map[node.Name] = node.AsDouble();
                        break;
                    case ObjectType.Boolean:
                        map[node.Name] = node.AsBoolean() ?? false;
                        break;
                    case ObjectType.Bytes:
                        map[node.Name] = node.AsBytes();
                        break;
                    case ObjectType.List:
                        map[node.Name] = node.AsCollection<List<object>, object>();
                        break;
                }
            }
            return map;
        }

        public IDictionary<string, byte[]> AsByteMap()
        {
            var map = new Dictionary<string, byte[]>();
            foreach (var node in _children) map[node.Name] = node.AsBytes();
            return map;
        }

        public IDictionary<string, IBinaryObjectNode> AsBinaryObjectNodeMap()
        {
            var map = new Dictionary<string, IBinaryObjectNode>();
            foreach (var node in _children) map[node.Name] = node;
            return map;
        }

        public override string ToString() => ToString(0);

        internal void SetObjectType(ObjectType objectType) => ObjectType = objectType;

        internal void SetName(string name) => Name = name;

        internal void SetBytesValue(byte[] sourceBytes, int sourceOffset, int sourceLength)
        {
            _bytesValue = null;
            _sourceBytes = sourceBytes;
            _sourceOffset = sourceOffset;
            _sourceLength = sourceLength;
        }

        internal void AddChild(IBinaryObjectNode child)
        {
            _children.Add(child);
            _childrenByName?.Add(child.Name, child);
        }

        private string ToString(int indent)
        {
            var sb = new StringBuilder();
            var prefix = new string(' ', indent * 2);

            sb.Append(prefix).Append(Name ?? "null").Append(" : ").Append(ObjectType);

            if (ValueLength() > 0 && !BytesValueIsContainer())
            {
                sb.Append(" = ");
                try
                {
                    switch (ObjectType)
                    {
                        case ObjectType.String: sb.Append(AsString()); break;
                        case ObjectType.I32: sb.Append(AsInt()); break;
                        case ObjectType.I64: sb.Append(AsLong()); break;
                        case ObjectType.Boolean: sb.Append(AsBoolean()); break;
                        case ObjectType.Float: sb.Append(AsFloat()); break;
                        case ObjectType.Double: sb.Append(AsDouble()); break;
                        case ObjectType.Null: sb.Append("null"); break;
                        default: sb.Append(BitConverter.ToString(MaterializedBytes())); break;
                    }
                }
                catch (DecodeSerializationException)
                {
                    sb.Append(BitConverter.ToString(MaterializedBytes()));
                }
            }

            if (_children.Count > 0)
            {
                sb.Append(" {\n");
                foreach (var child in _children)
                {
                    if (child is DefaultBinaryObjectNode d) sb.Append(d.ToString(indent + 1)).Append('\n');
                    else sb.Append(new string(' ', (indent + 1) * 2)).Append(child).Append('\n');
                }
                sb.Append(prefix).Append('}');
            }

            return sb.ToString();
        }

        private byte[] ValueSource() => _bytesValue ?? _sourceBytes;

        private int ValueOffset() => _bytesValue != null ? 0 : _sourceOffset;

        private int ValueLength()
        {
            if (_bytesValue != null) return _bytesValue.Length;
            if (_sourceBytes != null) return _sourceLength;
            return 0;
        }

        private byte ByteAt(int index)
        {
            if (index < 0 || index >= ValueLength())
                throw new DecodeSerializationException(
                    $"Invalid byte access at node '{Name}': index {index}, length {ValueLength()}");
            return ValueSource()[ValueOffset() + index];
        }

        private int ReadIntValue() =>
            (ByteAt(0) << 24) | (ByteAt(1) << 16) | (ByteAt(2) << 8) | ByteAt(3);

        private short ReadShortValue() => (short)((ByteAt(0) << 8) | ByteAt(1));

        private long ReadLongValue() =>
            ((long)ByteAt(0) << 56) | ((long)ByteAt(1) << 48) | ((long)ByteAt(2) << 40) | ((long)ByteAt(3) << 32)
            | ((long)ByteAt(4) << 24) | ((long)ByteAt(5) << 16) | ((long)ByteAt(6) << 8) | ByteAt(7);

        private byte[] MaterializedBytes()
        {
            if (_bytesValue == null)
            {
                if (_sourceBytes == null)
                {
                    _bytesValue = Array.Empty<byte>();
                }
                else
                {
                    _bytesValue = new byte[_sourceLength];
                    Array.Copy(_sourceBytes, _sourceOffset, _bytesValue, 0, _sourceLength);
                }
                _sourceBytes = null;
                _sourceOffset = 0;
                _sourceLength = 0;
            }
            return _bytesValue;
        }

        private bool BytesValueIsContainer() =>
            ObjectType == ObjectType.Object || ObjectType == ObjectType.List;
    }
}