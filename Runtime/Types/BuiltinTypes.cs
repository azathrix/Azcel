using System;
using System.Globalization;
using System.Reflection;

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
            var elementType = ResolveElementType(_elementParser.CSharpTypeName);
            if (string.IsNullOrEmpty(value))
                return Array.CreateInstance(elementType, 0);

            var parts = value.Split(separator[0]);
            var array = Array.CreateInstance(elementType, parts.Length);
            for (int i = 0; i < parts.Length; i++)
                array.SetValue(_elementParser.Parse(parts[i], separator), i);
            return array;
        }

        private static Type ResolveElementType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeof(object);

            switch (typeName)
            {
                case "int": return typeof(int);
                case "long": return typeof(long);
                case "float": return typeof(float);
                case "double": return typeof(double);
                case "bool": return typeof(bool);
                case "string": return typeof(string);
            }

            var direct = Type.GetType(typeName);
            if (direct != null)
                return direct;

            var needNameMatch = !typeName.Contains(".");
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                var type = assembly.GetType(typeName);
                if (type != null)
                    return type;

                if (!needNameMatch)
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                if (types == null)
                    continue;

                for (int t = 0; t < types.Length; t++)
                {
                    var candidate = types[t];
                    if (candidate != null && string.Equals(candidate.Name, typeName, StringComparison.Ordinal))
                        return candidate;
                }
            }

            return typeof(object);
        }

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
