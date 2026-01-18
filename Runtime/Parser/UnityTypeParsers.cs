using System;
using System.Globalization;
using Azcel;
using UnityEngine;

namespace Azcel.TypeParsers
{
    [TypeParserPlugin("Vector2")]
    public sealed class Vector2Parser : ITypeParser
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

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Vector2({readerExpr}.ReadSingle(), {readerExpr}.ReadSingle())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.x); {writerExpr}.Write({valueExpr}.y)";

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            var v = (Vector2)Parse(value, TypeParserUtil.NormalizeObjectSeparator(objectSep));
            writer.BeginObject();
            writer.WritePropertyName("x");
            writer.WriteFloat(v.x);
            writer.WritePropertyName("y");
            writer.WriteFloat(v.y);
            writer.EndObject();
        }
    }

    [TypeParserPlugin("Vector3")]
    public sealed class Vector3Parser : ITypeParser
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

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Vector3({readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.x); {writerExpr}.Write({valueExpr}.y); {writerExpr}.Write({valueExpr}.z)";

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            var v = (Vector3)Parse(value, TypeParserUtil.NormalizeObjectSeparator(objectSep));
            writer.BeginObject();
            writer.WritePropertyName("x");
            writer.WriteFloat(v.x);
            writer.WritePropertyName("y");
            writer.WriteFloat(v.y);
            writer.WritePropertyName("z");
            writer.WriteFloat(v.z);
            writer.EndObject();
        }
    }

    [TypeParserPlugin("Vector4")]
    public sealed class Vector4Parser : ITypeParser
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

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Vector4({readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.x); {writerExpr}.Write({valueExpr}.y); {writerExpr}.Write({valueExpr}.z); {writerExpr}.Write({valueExpr}.w)";

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            var v = (Vector4)Parse(value, TypeParserUtil.NormalizeObjectSeparator(objectSep));
            writer.BeginObject();
            writer.WritePropertyName("x");
            writer.WriteFloat(v.x);
            writer.WritePropertyName("y");
            writer.WriteFloat(v.y);
            writer.WritePropertyName("z");
            writer.WriteFloat(v.z);
            writer.WritePropertyName("w");
            writer.WriteFloat(v.w);
            writer.EndObject();
        }
    }

    [TypeParserPlugin("Vector2Int")]
    public sealed class Vector2IntParser : ITypeParser
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

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Vector2Int({readerExpr}.ReadInt32(), {readerExpr}.ReadInt32())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.x); {writerExpr}.Write({valueExpr}.y)";

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            var v = (Vector2Int)Parse(value, TypeParserUtil.NormalizeObjectSeparator(objectSep));
            writer.BeginObject();
            writer.WritePropertyName("x");
            writer.WriteInt(v.x);
            writer.WritePropertyName("y");
            writer.WriteInt(v.y);
            writer.EndObject();
        }
    }

    [TypeParserPlugin("Vector3Int")]
    public sealed class Vector3IntParser : ITypeParser
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

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Vector3Int({readerExpr}.ReadInt32(), {readerExpr}.ReadInt32(), {readerExpr}.ReadInt32())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.x); {writerExpr}.Write({valueExpr}.y); {writerExpr}.Write({valueExpr}.z)";

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            var v = (Vector3Int)Parse(value, TypeParserUtil.NormalizeObjectSeparator(objectSep));
            writer.BeginObject();
            writer.WritePropertyName("x");
            writer.WriteInt(v.x);
            writer.WritePropertyName("y");
            writer.WriteInt(v.y);
            writer.WritePropertyName("z");
            writer.WriteInt(v.z);
            writer.EndObject();
        }
    }

    [TypeParserPlugin("Color")]
    public sealed class ColorParser : ITypeParser
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

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Color({readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.r); {writerExpr}.Write({valueExpr}.g); {writerExpr}.Write({valueExpr}.b); {writerExpr}.Write({valueExpr}.a)";

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            var v = (Color)Parse(value, TypeParserUtil.NormalizeObjectSeparator(objectSep));
            writer.BeginObject();
            writer.WritePropertyName("r");
            writer.WriteFloat(v.r);
            writer.WritePropertyName("g");
            writer.WriteFloat(v.g);
            writer.WritePropertyName("b");
            writer.WriteFloat(v.b);
            writer.WritePropertyName("a");
            writer.WriteFloat(v.a);
            writer.EndObject();
        }
    }

    [TypeParserPlugin("Rect")]
    public sealed class RectParser : ITypeParser
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

        public string GenerateBinaryReadCode(string readerExpr)
            => $"new UnityEngine.Rect({readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle(), {readerExpr}.ReadSingle())";

        public string GenerateBinaryWriteCode(string writerExpr, string valueExpr)
            => $"{writerExpr}.Write({valueExpr}.x); {writerExpr}.Write({valueExpr}.y); {writerExpr}.Write({valueExpr}.width); {writerExpr}.Write({valueExpr}.height)";

        public void Serialize(IValueWriter writer, string value, string arraySep, string objectSep)
        {
            var v = (Rect)Parse(value, TypeParserUtil.NormalizeObjectSeparator(objectSep));
            writer.BeginObject();
            writer.WritePropertyName("x");
            writer.WriteFloat(v.x);
            writer.WritePropertyName("y");
            writer.WriteFloat(v.y);
            writer.WritePropertyName("width");
            writer.WriteFloat(v.width);
            writer.WritePropertyName("height");
            writer.WriteFloat(v.height);
            writer.EndObject();
        }
    }
}
