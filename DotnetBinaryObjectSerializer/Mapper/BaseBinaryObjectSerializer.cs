using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DotnetBinaryObjectSerializer.Annotations;
using DotnetBinaryObjectSerializer.Enums;
using DotnetBinaryObjectSerializer.Exceptions;

namespace DotnetBinaryObjectSerializer.Mapper
{
    public abstract class BaseBinaryObjectSerializer
    {
        private const BindingFlags DeclaredInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly ConcurrentDictionary<(Type, SerializationType), List<FieldCacheProps>> FieldCache = new();
        private static readonly ConcurrentDictionary<(Type, SerializationType), Dictionary<string, FieldCacheProps>> FieldMapCache = new();

        protected static List<FieldCacheProps> ResolveFields(Type type, SerializationType serializationType)
        {
            try
            {
                if (IsSimpleType(type)) return new List<FieldCacheProps>();
                return FieldCache.GetOrAdd((type, serializationType), key => ExtractFields(key.Item1, key.Item2));
            }
            catch (Exception e)
            {
                throw new SerializationException(
                    $"Failed to resolve fields for class: {type.FullName} ({serializationType})", e);
            }
        }

        protected static Dictionary<string, FieldCacheProps> ResolveFieldMap(Type type, SerializationType serializationType)
        {
            if (IsSimpleType(type)) return new Dictionary<string, FieldCacheProps>();

            return FieldMapCache.GetOrAdd((type, serializationType), key =>
            {
                var fields = ResolveFields(key.Item1, key.Item2);
                var map = new Dictionary<string, FieldCacheProps>(fields.Count);
                foreach (var field in fields)
                {
                    map[field.ElementName] = field;
                }
                return map;
            });
        }

        protected static bool IsSimpleType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive
                   || type == typeof(string)
                   || type == typeof(decimal)
                   || type == typeof(System.Numerics.BigInteger)
                   || type.IsEnum;
        }

        protected static bool TypeIs(Type comparison, params Type[] types)
        {
            return Array.IndexOf(types, comparison) >= 0;
        }

        protected static bool IsSystemType(Type type)
        {
            var ns = type.Namespace;
            return ns != null && (ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal));
        }

        private static List<FieldCacheProps> ExtractFields(Type type, SerializationType phase)
        {
            var fields = new List<FieldCacheProps>();
            if (IsSystemType(type)) return fields;

            var ignoreSuper = false;
            var ignoreSuperClass = type.GetCustomAttribute<IgnoreSuperClass>();
            if (ignoreSuperClass != null && Array.IndexOf(ignoreSuperClass.Value, phase) >= 0)
            {
                ignoreSuper = true;
            }

            var current = type;
            while (current != null && current != typeof(object))
            {
                foreach (var field in current.GetFields(DeclaredInstance))
                {
                    if (ShouldIgnoreField(field, phase)) continue;

                    var elementName = GetNameByElement(field);
                    fields.Add(new FieldCacheProps(
                        current,
                        phase,
                        field,
                        field.FieldType,
                        elementName,
                        Encoding.UTF8.GetBytes(elementName)));
                }

                if (ignoreSuper) break;

                var baseType = current.BaseType;
                if (baseType == null || IsSystemType(baseType)) break;
                current = baseType;
            }

            fields.Sort((a, b) =>
            {
                var byType = string.CompareOrdinal(a.Field.DeclaringType?.FullName, b.Field.DeclaringType?.FullName);
                return byType != 0 ? byType : string.CompareOrdinal(a.Field.Name, b.Field.Name);
            });

            return fields;
        }

        private static string GetNameByElement(FieldInfo field)
        {
            var elementRef = field.GetCustomAttribute<ElementRef>();
            return elementRef != null ? elementRef.Value : field.Name;
        }

        private static bool ShouldIgnoreField(FieldInfo field, SerializationType phase)
        {
            var ignore = field.GetCustomAttribute<IgnoreElement>();
            return ignore != null && Array.IndexOf(ignore.Value, phase) >= 0;
        }

        protected sealed class FieldCacheProps
        {
            public FieldCacheProps(Type declaringType, SerializationType serializationType, FieldInfo field,
                Type fieldType, string elementName, byte[] elementNameBytes)
            {
                DeclaringType = declaringType;
                SerializationType = serializationType;
                Field = field;
                FieldType = fieldType;
                ElementName = elementName;
                ElementNameBytes = elementNameBytes;
            }

            public Type DeclaringType { get; }
            public SerializationType SerializationType { get; }
            public FieldInfo Field { get; }
            public Type FieldType { get; }
            public string ElementName { get; }
            public byte[] ElementNameBytes { get; }
        }
    }
}
