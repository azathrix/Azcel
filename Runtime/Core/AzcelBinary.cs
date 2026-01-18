using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Azcel
{
    /// <summary>
    /// 二进制读写与基础解析辅助
    /// </summary>
    public interface ICustomTypeCodec
    {
        Type SystemType { get; }
        object Parse(string value, string arraySep, string objectSep);
        void Write(BinaryWriter writer, string value, string arraySep, string objectSep);
        object Read(BinaryReader reader);
        void Write(BinaryWriter writer, object value);
    }

    public static class AzcelBinary
    {
        private static readonly Dictionary<string, ICustomTypeCodec> CustomCodecs = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<Type, ICustomTypeCodec> CustomTypeMap = new();

        public static void RegisterCustomType(string typeName, ICustomTypeCodec codec)
        {
            if (string.IsNullOrEmpty(typeName) || codec == null || codec.SystemType == null)
                throw new ArgumentException("[AzcelBinary] 自定义类型注册参数无效");

            CustomCodecs[typeName] = codec;
            CustomTypeMap[codec.SystemType] = codec;
        }

        private static bool TryGetCustomCodec(string typeName, out ICustomTypeCodec codec)
        {
            if (!string.IsNullOrEmpty(typeName) && CustomCodecs.TryGetValue(typeName, out codec))
                return true;

            var simpleName = GetSimpleTypeName(typeName);
            if (!string.IsNullOrEmpty(simpleName) && CustomCodecs.TryGetValue(simpleName, out codec))
                return true;

            codec = null;
            return false;
        }

        private static bool TryGetCustomCodec(Type type, out ICustomTypeCodec codec)
        {
            if (type != null && CustomTypeMap.TryGetValue(type, out codec))
                return true;

            codec = null;
            return false;
        }

        public static T ReadValue<T>(BinaryReader reader)
        {
            var type = typeof(T);

            if (type == typeof(int)) return (T)(object)reader.ReadInt32();
            if (type == typeof(long)) return (T)(object)reader.ReadInt64();
            if (type == typeof(float)) return (T)(object)reader.ReadSingle();
            if (type == typeof(double)) return (T)(object)reader.ReadDouble();
            if (type == typeof(bool)) return (T)(object)reader.ReadBoolean();
            if (type == typeof(string)) return (T)(object)reader.ReadString();
            if (type == typeof(Vector2)) return (T)(object)new Vector2(reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(Vector3)) return (T)(object)new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(Vector4)) return (T)(object)new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(Vector2Int)) return (T)(object)new Vector2Int(reader.ReadInt32(), reader.ReadInt32());
            if (type == typeof(Vector3Int)) return (T)(object)new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            if (type == typeof(Color)) return (T)(object)new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(Rect)) return (T)(object)new Rect(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (type.IsEnum) return (T)Enum.ToObject(type, reader.ReadInt32());

            if (TryGetCustomCodec(type, out var customCodec))
                return (T)customCodec.Read(reader);

            throw new NotSupportedException($"[AzcelBinary] Unsupported read type: {type.FullName}");
        }

        public static T[] ReadArray<T>(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            if (count <= 0) return Array.Empty<T>();
            var array = new T[count];
            for (int i = 0; i < count; i++)
                array[i] = ReadValue<T>(reader);
            return array;
        }

        public static void WriteArray<T>(BinaryWriter writer, T[] value)
        {
            if (value == null || value.Length == 0)
            {
                writer.Write(0);
                return;
            }

            writer.Write(value.Length);
            for (int i = 0; i < value.Length; i++)
                WriteValue(writer, value[i]);
        }

        private static void WriteValue<T>(BinaryWriter writer, T value)
        {
            var type = typeof(T);

            if (type == typeof(int)) { writer.Write((int)(object)value); return; }
            if (type == typeof(long)) { writer.Write((long)(object)value); return; }
            if (type == typeof(float)) { writer.Write((float)(object)value); return; }
            if (type == typeof(double)) { writer.Write((double)(object)value); return; }
            if (type == typeof(bool)) { writer.Write((bool)(object)value); return; }
            if (type == typeof(string)) { writer.Write((string)(object)value ?? ""); return; }

            if (type == typeof(Vector2))
            {
                var v = (Vector2)(object)value;
                writer.Write(v.x);
                writer.Write(v.y);
                return;
            }
            if (type == typeof(Vector3))
            {
                var v = (Vector3)(object)value;
                writer.Write(v.x);
                writer.Write(v.y);
                writer.Write(v.z);
                return;
            }
            if (type == typeof(Vector4))
            {
                var v = (Vector4)(object)value;
                writer.Write(v.x);
                writer.Write(v.y);
                writer.Write(v.z);
                writer.Write(v.w);
                return;
            }
            if (type == typeof(Vector2Int))
            {
                var v = (Vector2Int)(object)value;
                writer.Write(v.x);
                writer.Write(v.y);
                return;
            }
            if (type == typeof(Vector3Int))
            {
                var v = (Vector3Int)(object)value;
                writer.Write(v.x);
                writer.Write(v.y);
                writer.Write(v.z);
                return;
            }
            if (type == typeof(Color))
            {
                var v = (Color)(object)value;
                writer.Write(v.r);
                writer.Write(v.g);
                writer.Write(v.b);
                writer.Write(v.a);
                return;
            }
            if (type == typeof(Rect))
            {
                var v = (Rect)(object)value;
                writer.Write(v.x);
                writer.Write(v.y);
                writer.Write(v.width);
                writer.Write(v.height);
                return;
            }
            if (type.IsEnum)
            {
                writer.Write(Convert.ToInt32(value, CultureInfo.InvariantCulture));
                return;
            }

            if (TryGetCustomCodec(type, out var customCodec))
            {
                customCodec.Write(writer, value);
                return;
            }

            throw new NotSupportedException($"[AzcelBinary] Unsupported write type: {type.FullName}");
        }

        private static string GetSimpleTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;
            var idx = typeName.LastIndexOf('.');
            return idx >= 0 ? typeName[(idx + 1)..] : typeName;
        }
    }
}
