using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Azcel
{
    /// <summary>
    /// 二进制读写与字符串解析辅助
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
        private static readonly Dictionary<string, ICustomTypeCodec> _customCodecs = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<Type, ICustomTypeCodec> _customTypeMap = new();

        public static void RegisterCustomType(string typeName, ICustomTypeCodec codec)
        {
            if (string.IsNullOrEmpty(typeName) || codec == null || codec.SystemType == null)
                throw new ArgumentException("[AzcelBinary] 自定义类型注册参数无效");

            _customCodecs[typeName] = codec;
            _customTypeMap[codec.SystemType] = codec;
        }

        private static bool TryGetCustomCodec(string typeName, out ICustomTypeCodec codec)
        {
            if (!string.IsNullOrEmpty(typeName) && _customCodecs.TryGetValue(typeName, out codec))
                return true;

            var simpleName = GetSimpleTypeName(typeName);
            if (!string.IsNullOrEmpty(simpleName) && _customCodecs.TryGetValue(simpleName, out codec))
                return true;

            codec = null;
            return false;
        }

        private static bool TryGetCustomCodec(Type type, out ICustomTypeCodec codec)
        {
            if (type != null && _customTypeMap.TryGetValue(type, out codec))
                return true;

            codec = null;
            return false;
        }
        public static void WriteValue(BinaryWriter writer, string type, string value, string arraySep = null, string objectSep = null)
        {
            if (string.IsNullOrEmpty(type))
            {
                writer.Write(value ?? "");
                return;
            }

            arraySep = NormalizeSeparator(arraySep, AzcelSettings.Instance?.arraySeparator, "|");
            objectSep = NormalizeSeparator(objectSep, AzcelSettings.Instance?.objectSeparator, ",");

            if (type.EndsWith("[]", StringComparison.Ordinal))
            {
                var elementType = type[..^2];
                WriteArray(writer, elementType, value, arraySep, objectSep);
                return;
            }

            if (IsMapType(type, out var keyType, out var valueType))
            {
                WriteDictionary(writer, keyType, valueType, value, arraySep, objectSep);
                return;
            }

            if (type.StartsWith("@", StringComparison.Ordinal))
            {
                WriteInt(writer, value);
                return;
            }

            if (type.StartsWith("#", StringComparison.Ordinal))
            {
                WriteEnum(writer, value);
                return;
            }

            if (TryGetCustomCodec(type, out var customCodec))
            {
                customCodec.Write(writer, value, arraySep, objectSep);
                return;
            }

            var simpleType = GetSimpleTypeName(type);
            switch (simpleType.ToLowerInvariant())
            {
                case "int":
                    WriteInt(writer, value);
                    return;
                case "long":
                    writer.Write(long.TryParse(value, out var l) ? l : 0L);
                    return;
                case "float":
                    writer.Write(float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f);
                    return;
                case "double":
                    writer.Write(double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0d);
                    return;
                case "bool":
                    writer.Write(IsTrue(value));
                    return;
                case "string":
                    writer.Write(value ?? "");
                    return;
                case "vector2":
                    WriteVector2(writer, value, objectSep);
                    return;
                case "vector3":
                    WriteVector3(writer, value, objectSep);
                    return;
                case "vector4":
                    WriteVector4(writer, value, objectSep);
                    return;
                case "vector2int":
                    WriteVector2Int(writer, value, objectSep);
                    return;
                case "vector3int":
                    WriteVector3Int(writer, value, objectSep);
                    return;
                case "color":
                    WriteColor(writer, value, objectSep);
                    return;
                case "rect":
                    WriteRect(writer, value, objectSep);
                    return;
            }

            // 未识别类型，退化为字符串写入
            writer.Write(value ?? "");
        }

        public static object ParseValue(string type, string value, string arraySep = null, string objectSep = null)
        {
            if (string.IsNullOrEmpty(type))
                return value ?? "";

            arraySep = NormalizeSeparator(arraySep, AzcelSettings.Instance?.arraySeparator, "|");
            objectSep = NormalizeSeparator(objectSep, AzcelSettings.Instance?.objectSeparator, ",");

            if (type.EndsWith("[]", StringComparison.Ordinal))
            {
                var elementType = type[..^2];
                return ParseArray(elementType, value, arraySep, objectSep);
            }

            if (IsMapType(type, out var keyType, out var valueType))
            {
                return ParseDictionary(keyType, valueType, value, arraySep, objectSep);
            }

            if (type.StartsWith("@", StringComparison.Ordinal))
                return int.TryParse(value, out var id) ? id : 0;

            if (type.StartsWith("#", StringComparison.Ordinal))
                return int.TryParse(value, out var enumValue) ? enumValue : 0;

            if (TryGetCustomCodec(type, out var customCodec))
                return customCodec.Parse(value, arraySep, objectSep);

            var simpleType = GetSimpleTypeName(type);
            switch (simpleType.ToLowerInvariant())
            {
                case "int":
                    return int.TryParse(value, out var i) ? i : 0;
                case "long":
                    return long.TryParse(value, out var l) ? l : 0L;
                case "float":
                    return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
                case "double":
                    return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0d;
                case "bool":
                    return IsTrue(value);
                case "string":
                    return value ?? "";
                case "vector2":
                    return ParseVector2(value, objectSep);
                case "vector3":
                    return ParseVector3(value, objectSep);
                case "vector4":
                    return ParseVector4(value, objectSep);
                case "vector2int":
                    return ParseVector2Int(value, objectSep);
                case "vector3int":
                    return ParseVector3Int(value, objectSep);
                case "color":
                    return ParseColor(value, objectSep);
                case "rect":
                    return ParseRect(value, objectSep);
            }

            return value ?? "";
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

        public static Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            var dict = new Dictionary<TKey, TValue>(Math.Max(0, count));
            for (int i = 0; i < count; i++)
            {
                var key = ReadValue<TKey>(reader);
                var val = ReadValue<TValue>(reader);
                dict[key] = val;
            }
            return dict;
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

        public static void WriteDictionary<TKey, TValue>(BinaryWriter writer, Dictionary<TKey, TValue> value)
        {
            if (value == null || value.Count == 0)
            {
                writer.Write(0);
                return;
            }

            writer.Write(value.Count);
            foreach (var kv in value)
            {
                WriteValue(writer, kv.Key);
                WriteValue(writer, kv.Value);
            }
        }

        public static T[] ParseArray<T>(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Array.Empty<T>();

            var parts = value.Split(separator[0]);
            var result = new T[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                result[i] = ParseValue<T>(parts[i], separator);
            return result;
        }

        public static Dictionary<TKey, TValue> ParseDictionary<TKey, TValue>(string value, string separator)
        {
            var dict = new Dictionary<TKey, TValue>();
            if (string.IsNullOrEmpty(value)) return dict;

            var entries = value.Split(separator[0]);
            foreach (var entry in entries)
            {
                var split = entry.Split(':');
                var keyStr = split.Length > 0 ? split[0] : "";
                var valStr = split.Length > 1 ? split[1] : "";
                var key = ParseValue<TKey>(keyStr, separator);
                var val = ParseValue<TValue>(valStr, separator);
                dict[key] = val;
            }
            return dict;
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

        private static T ParseValue<T>(string value, string separator)
        {
            var type = typeof(T);

            if (type == typeof(int)) return (T)(object)(int.TryParse(value, out var i) ? i : 0);
            if (type == typeof(long)) return (T)(object)(long.TryParse(value, out var l) ? l : 0L);
            if (type == typeof(float)) return (T)(object)(float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f);
            if (type == typeof(double)) return (T)(object)(double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0d);
            if (type == typeof(bool)) return (T)(object)IsTrue(value);
            if (type == typeof(string)) return (T)(object)(value ?? "");
            if (type == typeof(Vector2)) return (T)(object)ParseVector2(value, separator);
            if (type == typeof(Vector3)) return (T)(object)ParseVector3(value, separator);
            if (type == typeof(Vector4)) return (T)(object)ParseVector4(value, separator);
            if (type == typeof(Vector2Int)) return (T)(object)ParseVector2Int(value, separator);
            if (type == typeof(Vector3Int)) return (T)(object)ParseVector3Int(value, separator);
            if (type == typeof(Color)) return (T)(object)ParseColor(value, separator);
            if (type == typeof(Rect)) return (T)(object)ParseRect(value, separator);
            if (type.IsEnum) return (T)Enum.Parse(type, value ?? "0", true);

            if (TryGetCustomCodec(type, out var customCodec))
                return (T)customCodec.Parse(value, separator, separator);

            throw new NotSupportedException($"[AzcelBinary] Unsupported parse type: {type.FullName}");
        }

        private static void WriteArray(BinaryWriter writer, string elementType, string value, string arraySep, string objectSep)
        {
            if (string.IsNullOrEmpty(value))
            {
                writer.Write(0);
                return;
            }

            var parts = Split(value, arraySep);
            writer.Write(parts.Count);
            for (int i = 0; i < parts.Count; i++)
                WriteValue(writer, elementType, parts[i], arraySep, objectSep);
        }

        private static void WriteDictionary(BinaryWriter writer, string keyType, string valueType, string value, string arraySep, string objectSep)
        {
            if (string.IsNullOrEmpty(value))
            {
                writer.Write(0);
                return;
            }

            var entries = Split(value, arraySep);
            writer.Write(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                SplitKeyValue(entries[i], objectSep, out var key, out var val);
                WriteValue(writer, keyType, key, arraySep, objectSep);
                WriteValue(writer, valueType, val, arraySep, objectSep);
            }
        }

        private static object ParseArray(string elementType, string value, string arraySep, string objectSep)
        {
            if (string.IsNullOrEmpty(value))
                return Array.Empty<object>();

            var parts = Split(value, arraySep);
            var elementSystemType = ResolveSystemType(elementType);
            if (elementSystemType == null)
                return Array.Empty<object>();

            var array = Array.CreateInstance(elementSystemType, parts.Count);
            for (int i = 0; i < parts.Count; i++)
                array.SetValue(ParseToSystemType(elementSystemType, elementType, parts[i], arraySep, objectSep), i);
            return array;
        }

        private static object ParseDictionary(string keyType, string valueType, string value, string arraySep, string objectSep)
        {
            var keySystemType = ResolveSystemType(keyType);
            var valueSystemType = ResolveSystemType(valueType);
            if (keySystemType == null || valueSystemType == null)
                return new Dictionary<object, object>();

            var dictType = typeof(Dictionary<,>).MakeGenericType(keySystemType, valueSystemType);
            var dict = (System.Collections.IDictionary)Activator.CreateInstance(dictType);

            if (string.IsNullOrEmpty(value))
                return dict;

            var entries = Split(value, arraySep);
            for (int i = 0; i < entries.Count; i++)
            {
                SplitKeyValue(entries[i], objectSep, out var keyStr, out var valStr);
                var key = ParseToSystemType(keySystemType, keyType, keyStr, arraySep, objectSep);
                var val = ParseToSystemType(valueSystemType, valueType, valStr, arraySep, objectSep);
                dict[key] = val;
            }
            return dict;
        }

        private static object ParseToSystemType(Type systemType, string typeName, string value, string arraySep, string objectSep)
        {
            if (TryGetCustomCodec(typeName, out var customCodec))
                return customCodec.Parse(value, arraySep, objectSep);

            if (systemType == typeof(int)) return int.TryParse(value, out var i) ? i : 0;
            if (systemType == typeof(long)) return long.TryParse(value, out var l) ? l : 0L;
            if (systemType == typeof(float)) return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
            if (systemType == typeof(double)) return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0d;
            if (systemType == typeof(bool)) return IsTrue(value);
            if (systemType == typeof(string)) return value ?? "";
            if (systemType == typeof(Vector2)) return ParseVector2(value, objectSep);
            if (systemType == typeof(Vector3)) return ParseVector3(value, objectSep);
            if (systemType == typeof(Vector4)) return ParseVector4(value, objectSep);
            if (systemType == typeof(Vector2Int)) return ParseVector2Int(value, objectSep);
            if (systemType == typeof(Vector3Int)) return ParseVector3Int(value, objectSep);
            if (systemType == typeof(Color)) return ParseColor(value, objectSep);
            if (systemType == typeof(Rect)) return ParseRect(value, objectSep);
            if (systemType.IsEnum) return Enum.Parse(systemType, value ?? "0", true);

            if (typeName.EndsWith("[]", StringComparison.Ordinal))
                return ParseArray(typeName[..^2], value, arraySep, objectSep);

            if (IsMapType(typeName, out var keyType, out var valueType))
                return ParseDictionary(keyType, valueType, value, arraySep, objectSep);

            return value ?? "";
        }

        private static Type ResolveSystemType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            if (typeName.StartsWith("@", StringComparison.Ordinal))
                return typeof(int);

            if (typeName.StartsWith("#", StringComparison.Ordinal))
                return typeof(int);

            if (TryGetCustomCodec(typeName, out var customCodec))
                return customCodec.SystemType;

            var simpleType = GetSimpleTypeName(typeName);
            switch (simpleType.ToLowerInvariant())
            {
                case "int": return typeof(int);
                case "long": return typeof(long);
                case "float": return typeof(float);
                case "double": return typeof(double);
                case "bool": return typeof(bool);
                case "string": return typeof(string);
                case "vector2": return typeof(Vector2);
                case "vector3": return typeof(Vector3);
                case "vector4": return typeof(Vector4);
                case "vector2int": return typeof(Vector2Int);
                case "vector3int": return typeof(Vector3Int);
                case "color": return typeof(Color);
                case "rect": return typeof(Rect);
            }

            return null;
        }

        private static bool IsMapType(string type, out string keyType, out string valueType)
        {
            keyType = null;
            valueType = null;
            if (!type.StartsWith("map<", StringComparison.OrdinalIgnoreCase) || !type.EndsWith(">", StringComparison.Ordinal))
                return false;

            var inner = type[4..^1];
            var commaIndex = inner.IndexOf(',');
            if (commaIndex <= 0) return false;

            keyType = inner[..commaIndex].Trim();
            valueType = inner[(commaIndex + 1)..].Trim();
            return true;
        }

        private static void WriteInt(BinaryWriter writer, string value)
        {
            writer.Write(int.TryParse(value, out var i) ? i : 0);
        }

        private static void WriteEnum(BinaryWriter writer, string value)
        {
            writer.Write(int.TryParse(value, out var i) ? i : 0);
        }

        private static void WriteVector2(BinaryWriter writer, string value, string separator)
        {
            var v = ParseVector2(value, separator);
            writer.Write(v.x);
            writer.Write(v.y);
        }

        private static void WriteVector3(BinaryWriter writer, string value, string separator)
        {
            var v = ParseVector3(value, separator);
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
        }

        private static void WriteVector4(BinaryWriter writer, string value, string separator)
        {
            var v = ParseVector4(value, separator);
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
            writer.Write(v.w);
        }

        private static void WriteVector2Int(BinaryWriter writer, string value, string separator)
        {
            var v = ParseVector2Int(value, separator);
            writer.Write(v.x);
            writer.Write(v.y);
        }

        private static void WriteVector3Int(BinaryWriter writer, string value, string separator)
        {
            var v = ParseVector3Int(value, separator);
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
        }

        private static void WriteColor(BinaryWriter writer, string value, string separator)
        {
            var v = ParseColor(value, separator);
            writer.Write(v.r);
            writer.Write(v.g);
            writer.Write(v.b);
            writer.Write(v.a);
        }

        private static void WriteRect(BinaryWriter writer, string value, string separator)
        {
            var v = ParseRect(value, separator);
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.width);
            writer.Write(v.height);
        }

        private static Vector2 ParseVector2(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Vector2.zero;
            var parts = value.Split(separator[0]);
            return new Vector2(
                parts.Length > 0 ? ParseFloat(parts[0]) : 0f,
                parts.Length > 1 ? ParseFloat(parts[1]) : 0f
            );
        }

        private static Vector3 ParseVector3(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Vector3.zero;
            var parts = value.Split(separator[0]);
            return new Vector3(
                parts.Length > 0 ? ParseFloat(parts[0]) : 0f,
                parts.Length > 1 ? ParseFloat(parts[1]) : 0f,
                parts.Length > 2 ? ParseFloat(parts[2]) : 0f
            );
        }

        private static Vector4 ParseVector4(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Vector4.zero;
            var parts = value.Split(separator[0]);
            return new Vector4(
                parts.Length > 0 ? ParseFloat(parts[0]) : 0f,
                parts.Length > 1 ? ParseFloat(parts[1]) : 0f,
                parts.Length > 2 ? ParseFloat(parts[2]) : 0f,
                parts.Length > 3 ? ParseFloat(parts[3]) : 0f
            );
        }

        private static Vector2Int ParseVector2Int(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Vector2Int.zero;
            var parts = value.Split(separator[0]);
            return new Vector2Int(
                parts.Length > 0 ? ParseInt(parts[0]) : 0,
                parts.Length > 1 ? ParseInt(parts[1]) : 0
            );
        }

        private static Vector3Int ParseVector3Int(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Vector3Int.zero;
            var parts = value.Split(separator[0]);
            return new Vector3Int(
                parts.Length > 0 ? ParseInt(parts[0]) : 0,
                parts.Length > 1 ? ParseInt(parts[1]) : 0,
                parts.Length > 2 ? ParseInt(parts[2]) : 0
            );
        }

        private static Color ParseColor(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Color.white;
            var parts = value.Split(separator[0]);
            return new Color(
                parts.Length > 0 ? ParseFloat(parts[0]) : 1f,
                parts.Length > 1 ? ParseFloat(parts[1]) : 1f,
                parts.Length > 2 ? ParseFloat(parts[2]) : 1f,
                parts.Length > 3 ? ParseFloat(parts[3]) : 1f
            );
        }

        private static Rect ParseRect(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Rect.zero;
            var parts = value.Split(separator[0]);
            return new Rect(
                parts.Length > 0 ? ParseFloat(parts[0]) : 0f,
                parts.Length > 1 ? ParseFloat(parts[1]) : 0f,
                parts.Length > 2 ? ParseFloat(parts[2]) : 0f,
                parts.Length > 3 ? ParseFloat(parts[3]) : 0f
            );
        }

        private static float ParseFloat(string value)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, out var i) ? i : 0;
        }

        private static bool IsTrue(string value)
        {
            return value == "1" || value?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static List<string> Split(string value, string separator)
        {
            if (string.IsNullOrEmpty(value))
                return new List<string>();

            return new List<string>(value.Split(separator[0], StringSplitOptions.None));
        }

        private static void SplitKeyValue(string value, string separator, out string key, out string val)
        {
            var idx = value.IndexOf(separator, StringComparison.Ordinal);
            if (idx < 0)
            {
                key = value ?? "";
                val = "";
                return;
            }

            key = value[..idx];
            val = value[(idx + separator.Length)..];
        }

        private static string NormalizeSeparator(string separator, string fallback, string defaultValue)
        {
            if (!string.IsNullOrEmpty(separator)) return separator;
            if (!string.IsNullOrEmpty(fallback)) return fallback;
            return defaultValue;
        }

        private static string GetSimpleTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;
            var idx = typeName.LastIndexOf('.');
            return idx >= 0 ? typeName[(idx + 1)..] : typeName;
        }
    }
}
