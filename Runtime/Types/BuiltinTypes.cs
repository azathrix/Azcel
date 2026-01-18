using System;
using System.Globalization;

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

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            writer.WriteInt(string.IsNullOrEmpty(value) ? 0 : int.Parse(value));
        }
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

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            writer.WriteLong(string.IsNullOrEmpty(value) ? 0L : long.Parse(value));
        }
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

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            writer.WriteFloat(string.IsNullOrEmpty(value)
                ? 0f
                : float.Parse(value, CultureInfo.InvariantCulture));
        }
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

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            writer.WriteDouble(string.IsNullOrEmpty(value)
                ? 0d
                : double.Parse(value, CultureInfo.InvariantCulture));
        }
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

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            if (string.IsNullOrEmpty(value))
            {
                writer.WriteBool(false);
                return;
            }

            writer.WriteBool(value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
        }
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

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            writer.WriteString(value ?? "");
        }
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

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            arraySep = TypeParserUtil.NormalizeArraySeparator(arraySep);
            if (string.IsNullOrEmpty(value))
            {
                writer.BeginArray(0);
                writer.EndArray();
                return;
            }

            var parts = value.Split(arraySep[0]);
            writer.BeginArray(parts.Length);
            for (int i = 0; i < parts.Length; i++)
                _elementParser.Serialize(writer, parts[i], arraySep, objectSep);
            writer.EndArray();
        }
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

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            arraySep = TypeParserUtil.NormalizeArraySeparator(arraySep);
            objectSep = TypeParserUtil.NormalizeObjectSeparator(objectSep);

            if (string.IsNullOrEmpty(value))
            {
                writer.BeginArray(0);
                writer.EndArray();
                return;
            }

            var entries = value.Split(arraySep[0]);
            writer.BeginArray(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                TypeParserUtil.SplitKeyValue(entries[i], objectSep, out var key, out var val);
                writer.BeginObject();
                writer.WritePropertyName("k");
                _keyParser.Serialize(writer, key, arraySep, objectSep);
                writer.WritePropertyName("v");
                _valueParser.Serialize(writer, val, arraySep, objectSep);
                writer.EndObject();
            }
            writer.EndArray();
        }
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

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            writer.WriteInt(string.IsNullOrEmpty(value) ? 0 : int.Parse(value));
        }
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

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            writer.WriteInt(string.IsNullOrEmpty(value) ? 0 : int.Parse(value));
        }
    }

    #endregion

}
