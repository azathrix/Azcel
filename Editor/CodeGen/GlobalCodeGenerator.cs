using System;
using System.Globalization;
using System.Text;
using UnityEngine;

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
                    sb.AppendLine($"        public static readonly int {idField} = {BuildValueExpression(globalDef, value)};");
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
                var valueExpr = BuildValueExpression(globalDef, value);
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

        private static string BuildValueExpression(GlobalDefinition globalDef, GlobalValueDefinition valueDef)
        {
            var type = valueDef?.Type;
            var value = valueDef?.Value;
            if (string.IsNullOrEmpty(type))
                return ToStringLiteral(value);

            var arraySep = globalDef?.ArraySeparator;
            var objectSep = globalDef?.ObjectSeparator;
            if (string.IsNullOrEmpty(arraySep))
                arraySep = AzcelSettings.Instance?.arraySeparator ?? "|";
            if (string.IsNullOrEmpty(objectSep))
                objectSep = AzcelSettings.Instance?.objectSeparator ?? ",";

            if (TryBuildLiteralExpression(type, value, arraySep, objectSep, out var literalExpr))
                return literalExpr;

            throw new InvalidOperationException(BuildGlobalLiteralError(globalDef, valueDef));
        }

        private static bool TryBuildLiteralExpression(string type, string rawValue, string arraySep, string objectSep, out string expr)
        {
            expr = null;
            if (string.IsNullOrEmpty(type))
                return false;

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

            if (IsTableRef(type))
            {
                if (string.IsNullOrEmpty(rawValue))
                {
                    expr = "0";
                    return true;
                }

                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idValue))
                    idValue = 0;
                expr = idValue.ToString(CultureInfo.InvariantCulture);
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
                    if (!TryBuildLiteralExpression(elementType, parts[i], arraySep, objectSep, out var elementExpr))
                        return false;
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append(elementExpr);
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

        private static string NormalizeTypeName(string type)
        {
            if (string.IsNullOrEmpty(type))
                return string.Empty;

            var trimmed = type.Trim();
            if (trimmed.StartsWith("UnityEngine.", StringComparison.Ordinal))
                trimmed = trimmed.Substring("UnityEngine.".Length);
            return trimmed.ToLowerInvariant();
        }

        private static string BuildGlobalLiteralError(GlobalDefinition globalDef, GlobalValueDefinition valueDef)
        {
            var name = globalDef?.Name ?? "UnknownGlobal";
            var key = valueDef?.Key ?? "";
            var type = valueDef?.Type ?? "";
            var value = valueDef?.Value ?? "";

            var info = "";
            if (valueDef != null)
            {
                var parts = new StringBuilder();
                if (!string.IsNullOrEmpty(valueDef.SourceExcelPath))
                    parts.Append($"Excel: {valueDef.SourceExcelPath}");
                if (!string.IsNullOrEmpty(valueDef.SourceSheetName))
                {
                    if (parts.Length > 0) parts.Append(", ");
                    parts.Append($"Sheet: {valueDef.SourceSheetName}");
                }
                if (valueDef.RowIndex > 0)
                {
                    if (parts.Length > 0) parts.Append(", ");
                    parts.Append($"行: {valueDef.RowIndex}");
                }
                if (parts.Length > 0)
                    info = $" ({parts})";
            }

            return $"[Azcel] 全局配置无法生成静态字面量: {name}.{key} 类型 {type} 值 \"{value}\"{info}";
        }

        private static string[] Split(string value, string separator)
        {
            if (string.IsNullOrEmpty(value))
                return Array.Empty<string>();
            if (string.IsNullOrEmpty(separator))
                return new[] { value };
            return value.Split(new[] { separator }, StringSplitOptions.None);
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

        private static string ToStringLiteral(string value)
        {
            if (value == null)
                return "\"\"";

            var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
            return $"\"{escaped}\"";
        }

        
    }
}
