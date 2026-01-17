using System;
using System.Globalization;
using UnityEngine;

namespace Azcel
{
    /// <summary>
    /// 内置类型注册
    /// </summary>
    public static class BuiltinTypes
    {
        public static void RegisterAll()
        {
            // 基础类型
            TypeRegistry.Register("int", new IntParser());
            TypeRegistry.Register("long", new LongParser());
            TypeRegistry.Register("float", new FloatParser());
            TypeRegistry.Register("double", new DoubleParser());
            TypeRegistry.Register("bool", new BoolParser());
            TypeRegistry.Register("string", new StringParser());

            // Unity类型
            TypeRegistry.Register("Vector2", new Vector2Parser());
            TypeRegistry.Register("Vector3", new Vector3Parser());
            TypeRegistry.Register("Vector4", new Vector4Parser());
            TypeRegistry.Register("Vector2Int", new Vector2IntParser());
            TypeRegistry.Register("Vector3Int", new Vector3IntParser());
            TypeRegistry.Register("Color", new ColorParser());
            TypeRegistry.Register("Rect", new RectParser());
        }
    }

    #region 基础类型解析器

    public class IntParser : ITypeParser
    {
        public string CSharpTypeName => "int";
        public bool IsValueType => true;
        public string DefaultValueExpression => "0";

        public object Parse(string value, string separator)
            => string.IsNullOrEmpty(value) ? 0 : int.Parse(value);

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"int.Parse({valueExpr})";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"{readerExpr}.ReadInt32()";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr})";
    }

    public class LongParser : ITypeParser
    {
        public string CSharpTypeName => "long";
        public bool IsValueType => true;
        public string DefaultValueExpression => "0L";

        public object Parse(string value, string separator)
            => string.IsNullOrEmpty(value) ? 0L : long.Parse(value);

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"long.Parse({valueExpr})";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"{readerExpr}.ReadInt64()";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr})";
    }

    public class FloatParser : ITypeParser
    {
        public string CSharpTypeName => "float";
        public bool IsValueType => true;
        public string DefaultValueExpression => "0f";

        public object Parse(string value, string separator)
            => string.IsNullOrEmpty(value) ? 0f : float.Parse(value, CultureInfo.InvariantCulture);

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"float.Parse({valueExpr}, System.Globalization.CultureInfo.InvariantCulture)";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"{readerExpr}.ReadSingle()";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr})";
    }

    public class DoubleParser : ITypeParser
    {
        public string CSharpTypeName => "double";
        public bool IsValueType => true;
        public string DefaultValueExpression => "0d";

        public object Parse(string value, string separator)
            => string.IsNullOrEmpty(value) ? 0d : double.Parse(value, CultureInfo.InvariantCulture);

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"double.Parse({valueExpr}, System.Globalization.CultureInfo.InvariantCulture)";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"{readerExpr}.ReadDouble()";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr})";
    }

    public class BoolParser : ITypeParser
    {
        public string CSharpTypeName => "bool";
        public bool IsValueType => true;
        public string DefaultValueExpression => "false";

        public object Parse(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"({valueExpr} == \"1\" || {valueExpr}.Equals(\"true\", StringComparison.OrdinalIgnoreCase))";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"{readerExpr}.ReadBoolean()";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr})";
    }

    public class StringParser : ITypeParser
    {
        public string CSharpTypeName => "string";
        public bool IsValueType => false;
        public string DefaultValueExpression => "\"\"";

        public object Parse(string value, string separator)
            => value ?? "";

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => valueExpr;

        public string GenerateBinaryReadCode(string readerExpr)
            => $"{readerExpr}.ReadString()";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr} ?? \"\")";
    }

    #endregion

    #region Unity类型解析器

    public class Vector2Parser : ITypeParser
    {
        public string CSharpTypeName => "UnityEngine.Vector2";
        public bool IsValueType => true;
        public string DefaultValueExpression => "default";

        public object Parse(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Vector2.zero;
            var parts = value.Split(separator[0]);
            return new Vector2(
                parts.Length > 0 ? float.Parse(parts[0], CultureInfo.InvariantCulture) : 0,
                parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : 0
            );
        }

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"ParseVector2({valueExpr}, {separatorExpr})";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Vector2({readerExpr}.ReadSingle(), {readerExpr}.ReadSingle())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.x); {writerExpr}.Write({valueExpr}.y)";
    }

    public class Vector3Parser : ITypeParser
    {
        public string CSharpTypeName => "UnityEngine.Vector3";
        public bool IsValueType => true;
        public string DefaultValueExpression => "default";

        public object Parse(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Vector3.zero;
            var parts = value.Split(separator[0]);
            return new Vector3(
                parts.Length > 0 ? float.Parse(parts[0], CultureInfo.InvariantCulture) : 0,
                parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : 0,
                parts.Length > 2 ? float.Parse(parts[2], CultureInfo.InvariantCulture) : 0
            );
        }

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"ParseVector3({valueExpr}, {separatorExpr})";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Vector3({readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.x); {writerExpr}.Write({valueExpr}.y); {writerExpr}.Write({valueExpr}.z)";
    }

    public class Vector4Parser : ITypeParser
    {
        public string CSharpTypeName => "UnityEngine.Vector4";
        public bool IsValueType => true;
        public string DefaultValueExpression => "default";

        public object Parse(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Vector4.zero;
            var parts = value.Split(separator[0]);
            return new Vector4(
                parts.Length > 0 ? float.Parse(parts[0], CultureInfo.InvariantCulture) : 0,
                parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : 0,
                parts.Length > 2 ? float.Parse(parts[2], CultureInfo.InvariantCulture) : 0,
                parts.Length > 3 ? float.Parse(parts[3], CultureInfo.InvariantCulture) : 0
            );
        }

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"ParseVector4({valueExpr}, {separatorExpr})";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Vector4({readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.x); {writerExpr}.Write({valueExpr}.y); {writerExpr}.Write({valueExpr}.z); {writerExpr}.Write({valueExpr}.w)";
    }

    public class Vector2IntParser : ITypeParser
    {
        public string CSharpTypeName => "UnityEngine.Vector2Int";
        public bool IsValueType => true;
        public string DefaultValueExpression => "default";

        public object Parse(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Vector2Int.zero;
            var parts = value.Split(separator[0]);
            return new Vector2Int(
                parts.Length > 0 ? int.Parse(parts[0]) : 0,
                parts.Length > 1 ? int.Parse(parts[1]) : 0
            );
        }

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"ParseVector2Int({valueExpr}, {separatorExpr})";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Vector2Int({readerExpr}.ReadInt32(), {readerExpr}.ReadInt32())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.x); {writerExpr}.Write({valueExpr}.y)";
    }

    public class Vector3IntParser : ITypeParser
    {
        public string CSharpTypeName => "UnityEngine.Vector3Int";
        public bool IsValueType => true;
        public string DefaultValueExpression => "default";

        public object Parse(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Vector3Int.zero;
            var parts = value.Split(separator[0]);
            return new Vector3Int(
                parts.Length > 0 ? int.Parse(parts[0]) : 0,
                parts.Length > 1 ? int.Parse(parts[1]) : 0,
                parts.Length > 2 ? int.Parse(parts[2]) : 0
            );
        }

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"ParseVector3Int({valueExpr}, {separatorExpr})";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Vector3Int({readerExpr}.ReadInt32(), {readerExpr}.ReadInt32(), {readerExpr}.ReadInt32())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.x); {writerExpr}.Write({valueExpr}.y); {writerExpr}.Write({valueExpr}.z)";
    }

    public class ColorParser : ITypeParser
    {
        public string CSharpTypeName => "UnityEngine.Color";
        public bool IsValueType => true;
        public string DefaultValueExpression => "default";

        public object Parse(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Color.white;
            var parts = value.Split(separator[0]);
            return new Color(
                parts.Length > 0 ? float.Parse(parts[0], CultureInfo.InvariantCulture) : 1,
                parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : 1,
                parts.Length > 2 ? float.Parse(parts[2], CultureInfo.InvariantCulture) : 1,
                parts.Length > 3 ? float.Parse(parts[3], CultureInfo.InvariantCulture) : 1
            );
        }

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"ParseColor({valueExpr}, {separatorExpr})";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Color({readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.r); {writerExpr}.Write({valueExpr}.g); {writerExpr}.Write({valueExpr}.b); {writerExpr}.Write({valueExpr}.a)";
    }

    public class RectParser : ITypeParser
    {
        public string CSharpTypeName => "UnityEngine.Rect";
        public bool IsValueType => true;
        public string DefaultValueExpression => "default";

        public object Parse(string value, string separator)
        {
            if (string.IsNullOrEmpty(value)) return Rect.zero;
            var parts = value.Split(separator[0]);
            return new Rect(
                parts.Length > 0 ? float.Parse(parts[0], CultureInfo.InvariantCulture) : 0,
                parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : 0,
                parts.Length > 2 ? float.Parse(parts[2], CultureInfo.InvariantCulture) : 0,
                parts.Length > 3 ? float.Parse(parts[3], CultureInfo.InvariantCulture) : 0
            );
        }

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"ParseRect({valueExpr}, {separatorExpr})";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Rect({readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.x); {writerExpr}.Write({valueExpr}.y); {writerExpr}.Write({valueExpr}.width); {writerExpr}.Write({valueExpr}.height)";
    }

    #endregion

    #region 复合类型解析器

    public class ArrayTypeParser : ITypeParser
    {
        private readonly ITypeParser _elementParser;

        public ArrayTypeParser(ITypeParser elementParser)
        {
            _elementParser = elementParser;
        }

        public string CSharpTypeName => $"{_elementParser.CSharpTypeName}[]";
        public bool IsValueType => false;
        public string DefaultValueExpression => $"System.Array.Empty<{_elementParser.CSharpTypeName}>()";

        public object Parse(string value, string separator)
        {
            if (string.IsNullOrEmpty(value))
                return Array.CreateInstance(Type.GetType(_elementParser.CSharpTypeName) ?? typeof(object), 0);

            var parts = value.Split(separator[0]);
            var array = Array.CreateInstance(Type.GetType(_elementParser.CSharpTypeName) ?? typeof(object), parts.Length);
            for (int i = 0; i < parts.Length; i++)
                array.SetValue(_elementParser.Parse(parts[i], separator), i);
            return array;
        }

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"AzcelBinary.ParseArray<{_elementParser.CSharpTypeName}>({valueExpr}, {separatorExpr})";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"AzcelBinary.ReadArray<{_elementParser.CSharpTypeName}>({readerExpr})";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"AzcelBinary.WriteArray({writerExpr}, {valueExpr})";
    }

    public class DictionaryTypeParser : ITypeParser
    {
        private readonly ITypeParser _keyParser;
        private readonly ITypeParser _valueParser;

        public DictionaryTypeParser(ITypeParser keyParser, ITypeParser valueParser)
        {
            _keyParser = keyParser;
            _valueParser = valueParser;
        }

        public string CSharpTypeName => $"System.Collections.Generic.Dictionary<{_keyParser.CSharpTypeName}, {_valueParser.CSharpTypeName}>";
        public bool IsValueType => false;
        public string DefaultValueExpression => $"new System.Collections.Generic.Dictionary<{_keyParser.CSharpTypeName}, {_valueParser.CSharpTypeName}>()";

        public object Parse(string value, string separator)
        {
            // 简化实现，实际需要更复杂的解析
            return new System.Collections.Generic.Dictionary<object, object>();
        }

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"AzcelBinary.ParseDictionary<{_keyParser.CSharpTypeName}, {_valueParser.CSharpTypeName}>({valueExpr}, {separatorExpr})";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"AzcelBinary.ReadDictionary<{_keyParser.CSharpTypeName}, {_valueParser.CSharpTypeName}>({readerExpr})";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"AzcelBinary.WriteDictionary({writerExpr}, {valueExpr})";
    }

    public class TableRefTypeParser : ITypeParser
    {
        private readonly string _tableName;

        public TableRefTypeParser(string tableName)
        {
            _tableName = tableName;
        }

        public string CSharpTypeName => _tableName;
        public bool IsValueType => true;
        public string DefaultValueExpression => "default";

        public object Parse(string value, string separator)
        {
            // 表引用在运行时解析
            return string.IsNullOrEmpty(value) ? 0 : int.Parse(value);
        }

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"int.Parse({valueExpr})"; // 存储ID

        public string GenerateBinaryReadCode(string readerExpr)
            => $"{readerExpr}.ReadInt32()"; // 读取ID

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr})"; // 写入ID
    }

    public class EnumRefTypeParser : ITypeParser
    {
        private readonly string _enumName;

        public EnumRefTypeParser(string enumName)
        {
            _enumName = enumName;
        }

        public string CSharpTypeName => _enumName;
        public bool IsValueType => true;
        public string DefaultValueExpression => "default";

        public object Parse(string value, string separator)
        {
            // 枚举在运行时解析
            return string.IsNullOrEmpty(value) ? 0 : int.Parse(value);
        }

        public string GenerateParseCode(string valueExpr, string separatorExpr)
            => $"Enum.Parse<{_enumName}>({valueExpr})";

        public string GenerateBinaryReadCode(string readerExpr)
            => $"({_enumName}){readerExpr}.ReadInt32()";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write((int){valueExpr})";
    }

    #endregion
}
