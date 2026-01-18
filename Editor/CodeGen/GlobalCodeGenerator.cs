using System;
using System.Globalization;
using System.Text;

namespace Azcel.Editor
{
    /// <summary>
    /// 全局配置代码生成器
    /// </summary>
    public static class GlobalCodeGenerator
    {
        public static string Generate(GlobalDefinition globalDef, string ns)
        {
            var sb = new StringBuilder();
            var className = globalDef.Name;

            sb.AppendLine("// 此文件由 Azcel 自动生成，请勿手动修改");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using Azcel;");
            sb.AppendLine("using Azathrix.Framework.Core;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    public static class {className}");
            sb.AppendLine("    {");

            foreach (var value in globalDef.Values)
            {
                if (string.IsNullOrEmpty(value?.Key))
                    continue;

                if (IsTableRef(value.Type))
                {
                    var tableName = value.Type.Substring(1);
                    var idField = $"{value.Key}Id";
                    sb.AppendLine($"        public static readonly int {idField} = {BuildValueExpression(value.Type, value.Value)};");
                    sb.AppendLine($"        private static {tableName} _{value.Key}Cache;");
                    sb.AppendLine($"        private static bool _{value.Key}CacheReady;");
                    AppendComment(sb, value.Comment, 8);
                    sb.AppendLine($"        public static {tableName} {value.Key}");
                    sb.AppendLine("        {");
                    sb.AppendLine("            get");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                if (!_{value.Key}CacheReady)");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    _{value.Key}CacheReady = true;");
                    sb.AppendLine($"                    var table = AzathrixFramework.GetSystem<AzcelSystem>()?.GetTable<{tableName}Table>();");
                    sb.AppendLine($"                    _{value.Key}Cache = table?.GetById({idField});");
                    sb.AppendLine("                }");
                    sb.AppendLine($"                return _{value.Key}Cache;");
                    sb.AppendLine("            }");
                    sb.AppendLine("        }");
                    sb.AppendLine();
                    continue;
                }

                var typeName = GetCSharpType(value.Type);
                var valueExpr = BuildValueExpression(value.Type, value.Value);
                AppendComment(sb, value.Comment, 8);
                sb.AppendLine($"        public static readonly {typeName} {value.Key} = {valueExpr};");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GetCSharpType(string type)
        {
            if (string.IsNullOrEmpty(type))
                return "string";

            if (IsEnumRef(type))
                return type.Substring(1);

            var parser = TypeRegistry.Get(type);
            return parser?.CSharpTypeName ?? "string";
        }

        private static void AppendComment(StringBuilder sb, string comment, int indent)
        {
            if (string.IsNullOrEmpty(comment))
                return;

            var pad = new string(' ', indent);
            sb.AppendLine($"{pad}/// <summary>");
            var lines = comment.Replace("\r", "").Split('\n');
            foreach (var line in lines)
            {
                var text = EscapeXml(line);
                sb.AppendLine($"{pad}/// {text}");
            }
            sb.AppendLine($"{pad}/// </summary>");
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static string BuildValueExpression(string type, string value)
        {
            if (IsTableRef(type))
            {
                if (TryBuildLiteralExpression("int", value, out var idExpr))
                    return idExpr;
            }

            if (TryBuildLiteralExpression(type, value, out var literalExpr))
                return literalExpr;

            var parser = TypeRegistry.Get(type);
            if (parser == null)
                return ToStringLiteral(value);

            if (string.IsNullOrEmpty(value))
                return parser.DefaultValueExpression;

            var valueExpr = ToStringLiteral(value);
            var sepExpr = "AzcelSettings.Instance?.arraySeparator ?? \"|\"";

            if (IsEnumRef(type))
            {
                var enumName = type.Substring(1);
                return $"({enumName})AzcelBinary.ParseValue(\"{Escape(type)}\", {valueExpr})";
            }

            if (IsArrayOrMap(type))
                return parser.GenerateParseCode(valueExpr, sepExpr);

            var csharpType = parser.CSharpTypeName ?? "object";
            return $"({csharpType})AzcelBinary.ParseValue(\"{Escape(type)}\", {valueExpr})";
        }

        private static bool TryBuildLiteralExpression(string type, string rawValue, out string expr)
        {
            expr = null;
            if (string.IsNullOrEmpty(type))
                return false;

            var arraySep = AzcelSettings.Instance?.arraySeparator ?? "|";
            var objectSep = AzcelSettings.Instance?.objectSeparator ?? ",";

            if (IsEnumRef(type))
            {
                var enumName = type.Substring(1);
                if (string.IsNullOrEmpty(rawValue))
                {
                    expr = $"({enumName})0";
                    return true;
                }

                if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var enumValue))
                {
                    expr = $"({enumName}){enumValue}";
                    return true;
                }

                expr = rawValue.Contains(".") ? rawValue : $"{enumName}.{rawValue}";
                return true;
            }

            if (IsArrayType(type, out var elementType))
            {
                var elementCSharpType = GetCSharpType(elementType);
                if (string.IsNullOrEmpty(rawValue))
                {
                    expr = $"System.Array.Empty<{elementCSharpType}>()";
                    return true;
                }

                var parts = Split(rawValue, arraySep);
                var sb = new StringBuilder();
                sb.Append("new ").Append(elementCSharpType).Append("[] { ");
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!TryBuildLiteralExpression(elementType, parts[i], out var elementExpr))
                        return false;
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append(elementExpr);
                }
                sb.Append(" }");
                expr = sb.ToString();
                return true;
            }

            if (IsMapType(type, out var keyType, out var valueType))
            {
                var keyCSharp = GetCSharpType(keyType);
                var valueCSharp = GetCSharpType(valueType);
                if (string.IsNullOrEmpty(rawValue))
                {
                    expr = $"new System.Collections.Generic.Dictionary<{keyCSharp}, {valueCSharp}>()";
                    return true;
                }

                var entries = Split(rawValue, arraySep);
                var sb = new StringBuilder();
                sb.Append("new System.Collections.Generic.Dictionary<")
                    .Append(keyCSharp).Append(", ").Append(valueCSharp).Append(">")
                    .Append(" { ");

                for (int i = 0; i < entries.Length; i++)
                {
                    SplitKeyValue(entries[i], objectSep, out var keyRaw, out var valueRaw);
                    if (!TryBuildLiteralExpression(keyType, keyRaw, out var keyExpr))
                        return false;
                    if (!TryBuildLiteralExpression(valueType, valueRaw, out var valueExpr))
                        return false;
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append("{ ").Append(keyExpr).Append(", ").Append(valueExpr).Append(" }");
                }
                sb.Append(" }");
                expr = sb.ToString();
                return true;
            }

            var normalized = NormalizeTypeName(type);
            if (normalized == "string")
            {
                expr = ToStringLiteral(rawValue);
                return true;
            }

            if (normalized == "bool")
            {
                var boolValue = !string.IsNullOrEmpty(rawValue) &&
                                (rawValue == "1" || rawValue.Equals("true", StringComparison.OrdinalIgnoreCase));
                expr = boolValue ? "true" : "false";
                return true;
            }

            if (normalized == "int")
            {
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    intValue = 0;
                expr = intValue.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (normalized == "long")
            {
                if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                    longValue = 0L;
                expr = $"{longValue.ToString(CultureInfo.InvariantCulture)}L";
                return true;
            }

            if (normalized == "float")
            {
                if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                    floatValue = 0f;
                expr = $"{floatValue.ToString("R", CultureInfo.InvariantCulture)}f";
                return true;
            }

            if (normalized == "double")
            {
                if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                    doubleValue = 0d;
                expr = $"{doubleValue.ToString("R", CultureInfo.InvariantCulture)}d";
                return true;
            }

            if (normalized == "vector2")
            {
                var parts = Split(rawValue, objectSep);
                var x = ParseFloatLiteral(parts, 0);
                var y = ParseFloatLiteral(parts, 1);
                expr = $"new UnityEngine.Vector2({x}f, {y}f)";
                return true;
            }

            if (normalized == "vector3")
            {
                var parts = Split(rawValue, objectSep);
                var x = ParseFloatLiteral(parts, 0);
                var y = ParseFloatLiteral(parts, 1);
                var z = ParseFloatLiteral(parts, 2);
                expr = $"new UnityEngine.Vector3({x}f, {y}f, {z}f)";
                return true;
            }

            if (normalized == "vector4")
            {
                var parts = Split(rawValue, objectSep);
                var x = ParseFloatLiteral(parts, 0);
                var y = ParseFloatLiteral(parts, 1);
                var z = ParseFloatLiteral(parts, 2);
                var w = ParseFloatLiteral(parts, 3);
                expr = $"new UnityEngine.Vector4({x}f, {y}f, {z}f, {w}f)";
                return true;
            }

            if (normalized == "vector2int")
            {
                var parts = Split(rawValue, objectSep);
                var x = ParseInt(parts, 0);
                var y = ParseInt(parts, 1);
                expr = $"new UnityEngine.Vector2Int({x}, {y})";
                return true;
            }

            if (normalized == "vector3int")
            {
                var parts = Split(rawValue, objectSep);
                var x = ParseInt(parts, 0);
                var y = ParseInt(parts, 1);
                var z = ParseInt(parts, 2);
                expr = $"new UnityEngine.Vector3Int({x}, {y}, {z})";
                return true;
            }

            if (normalized == "color")
            {
                var parts = Split(rawValue, objectSep);
                var r = ParseFloatLiteral(parts, 0);
                var g = ParseFloatLiteral(parts, 1);
                var b = ParseFloatLiteral(parts, 2);
                var a = parts.Length > 3 ? ParseFloatLiteral(parts, 3) : "1";
                expr = $"new UnityEngine.Color({r}f, {g}f, {b}f, {a}f)";
                return true;
            }

            if (normalized == "rect")
            {
                var parts = Split(rawValue, objectSep);
                var x = ParseFloatLiteral(parts, 0);
                var y = ParseFloatLiteral(parts, 1);
                var w = ParseFloatLiteral(parts, 2);
                var h = ParseFloatLiteral(parts, 3);
                expr = $"new UnityEngine.Rect({x}f, {y}f, {w}f, {h}f)";
                return true;
            }

            return false;
        }

        private static bool IsArrayType(string type, out string elementType)
        {
            elementType = null;
            if (string.IsNullOrEmpty(type))
                return false;
            if (!type.EndsWith("[]", StringComparison.Ordinal))
                return false;
            elementType = type[..^2];
            return true;
        }

        private static bool IsMapType(string type, out string keyType, out string valueType)
        {
            keyType = null;
            valueType = null;
            if (string.IsNullOrEmpty(type))
                return false;
            if (!type.StartsWith("map<", StringComparison.OrdinalIgnoreCase) || !type.EndsWith(">", StringComparison.Ordinal))
                return false;

            var inner = type[4..^1];
            var commaIndex = inner.IndexOf(',');
            if (commaIndex <= 0)
                return false;
            keyType = inner[..commaIndex].Trim();
            valueType = inner[(commaIndex + 1)..].Trim();
            return true;
        }

        private static string NormalizeTypeName(string type)
        {
            if (string.IsNullOrEmpty(type))
                return string.Empty;

            var trimmed = type.Trim();
            if (trimmed.StartsWith("UnityEngine.", StringComparison.Ordinal))
                trimmed = trimmed.Substring("UnityEngine.".Length);
            return trimmed.ToLowerInvariant();
        }

        private static string[] Split(string value, string separator)
        {
            if (string.IsNullOrEmpty(value))
                return Array.Empty<string>();
            if (string.IsNullOrEmpty(separator))
                return new[] { value };
            return value.Split(new[] { separator }, StringSplitOptions.None);
        }

        private static void SplitKeyValue(string value, string separator, out string key, out string val)
        {
            if (string.IsNullOrEmpty(value))
            {
                key = string.Empty;
                val = string.Empty;
                return;
            }

            if (string.IsNullOrEmpty(separator))
            {
                key = value;
                val = string.Empty;
                return;
            }

            var idx = value.IndexOf(separator, StringComparison.Ordinal);
            if (idx < 0)
            {
                key = value;
                val = string.Empty;
                return;
            }

            key = value.Substring(0, idx);
            val = value.Substring(idx + separator.Length);
        }

        private static int ParseInt(string[] parts, int index)
        {
            if (parts == null || index >= parts.Length)
                return 0;
            return int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        private static string ParseFloatLiteral(string[] parts, int index)
        {
            if (parts == null || index >= parts.Length)
                return "0";
            var value = float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool IsEnumRef(string type)
        {
            return !string.IsNullOrEmpty(type) && type.StartsWith("#", StringComparison.Ordinal);
        }

        private static bool IsTableRef(string type)
        {
            return !string.IsNullOrEmpty(type) && type.StartsWith("@", StringComparison.Ordinal);
        }

        private static bool IsArrayOrMap(string type)
        {
            if (string.IsNullOrEmpty(type))
                return false;

            if (type.EndsWith("[]", StringComparison.Ordinal))
                return true;

            return type.StartsWith("map<", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToStringLiteral(string value)
        {
            if (value == null)
                return "\"\"";

            var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
            return $"\"{escaped}\"";
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
