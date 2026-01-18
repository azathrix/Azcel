using System;
using System.Collections.Generic;
using System.Globalization;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azcel.Editor
{
    /// <summary>
    /// 数据校验阶段（类型/枚举/引用值合法性）
    /// </summary>
    [Register]
    [PhaseId("Validate")]
    public class ValidationPhase : IConvertPhase
    {
        public int Order => 350;

        public UniTask ExecuteAsync(ConvertContext context)
        {
            var enumValueMap = BuildEnumValueMap(context);

            foreach (var table in context.Tables)
            {
                foreach (var row in table.Rows)
                {
                    foreach (var field in table.Fields)
                    {
                        if (IsSkipped(field))
                            continue;

                        var value = row.Values.TryGetValue(field.Name, out var v) ? v : "";
                        if (!TryValidateValue(table, field, row, value, enumValueMap, out var error))
                            context.AddError(error);
                    }
                }
            }

            foreach (var global in context.Globals)
            {
                foreach (var value in global.Values)
                {
                    if (!TryValidateGlobalValue(global, value, enumValueMap, out var error))
                        context.AddError(error);
                }
            }

            Log.Info("[Validate] 数据校验完成");
            return UniTask.CompletedTask;
        }

        private static bool IsSkipped(FieldDefinition field)
        {
            if (field == null)
                return false;

            if (!field.Options.TryGetValue("skip", out var value))
                return false;

            if (string.IsNullOrEmpty(value))
                return true;

            return value.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("1", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, Dictionary<string, int>> BuildEnumValueMap(ConvertContext context)
        {
            var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var enumDef in context.Enums)
            {
                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var val in enumDef.Values)
                    map[val.Name] = val.Value;
                result[enumDef.Name] = map;
            }
            return result;
        }

        private static bool TryValidateValue(TableDefinition table, FieldDefinition field, RowData row, string value,
            Dictionary<string, Dictionary<string, int>> enumValueMap, out string error)
        {
            error = null;
            if (field == null)
                return true;

            if (!TryValidateByType(field.Type, value, table.ArraySeparator, table.ObjectSeparator, enumValueMap, out var reason))
            {
                var rowInfo = row?.RowIndex > 0 ? $" 第{row.RowIndex}行" : "";
                var sourcePath = row?.SourceExcelPath ?? table.ExcelPath;
                var sourceSheet = row?.SourceSheetName;
                var sourceInfo = string.IsNullOrEmpty(sourcePath) ? "" : $" (Excel: {sourcePath}";
                if (!string.IsNullOrEmpty(sourceInfo))
                {
                    if (!string.IsNullOrEmpty(sourceSheet))
                        sourceInfo += $", Sheet: {sourceSheet}";
                    sourceInfo += ")";
                }
                error = $"[Validate] 表 {table.Name}{rowInfo} 字段 {field.Name} 类型 {field.Type} 值 \"{value}\" 无法解析：{reason}{sourceInfo}";
                return false;
            }

            return true;
        }

        private static bool TryValidateGlobalValue(GlobalDefinition global, GlobalValueDefinition value,
            Dictionary<string, Dictionary<string, int>> enumValueMap, out string error)
        {
            error = null;
            if (value == null)
                return true;

            var arraySep = global?.ArraySeparator;
            var objectSep = global?.ObjectSeparator;
            if (!TryValidateByType(value.Type, value.Value, arraySep, objectSep, enumValueMap, out var reason))
            {
                var sourceInfo = string.IsNullOrEmpty(value.SourceExcelPath)
                    ? ""
                    : $" (Excel: {value.SourceExcelPath}{(string.IsNullOrEmpty(value.SourceSheetName) ? "" : $", Sheet: {value.SourceSheetName}")}{(value.RowIndex > 0 ? $", 行: {value.RowIndex}" : "")})";
                error = $"[Validate] 全局 {global.Name} 键 {value.Key} 类型 {value.Type} 值 \"{value.Value}\" 无法解析：{reason}{sourceInfo}";
                return false;
            }

            return true;
        }

        private static bool TryValidateByType(string type, string value, string arraySep, string objectSep,
            Dictionary<string, Dictionary<string, int>> enumValueMap, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(type))
                return true;

            arraySep ??= AzcelSettings.Instance?.arraySeparator ?? "|";
            objectSep ??= AzcelSettings.Instance?.objectSeparator ?? ",";

            if (type.EndsWith("[]", StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(value))
                    return true;

                var elementType = type[..^2];
                var parts = value.Split(arraySep[0]);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!TryValidateByType(elementType, parts[i], arraySep, objectSep, enumValueMap, out reason))
                    {
                        reason = $"数组元素[{i}] 无法解析：{reason}";
                        return false;
                    }
                }
                return true;
            }

            if (type.StartsWith("@", StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(value))
                    return true;
                if (int.TryParse(value, out _))
                    return true;
                reason = "引用ID不是有效的 int";
                return false;
            }

            if (type.StartsWith("#", StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(value))
                    return true;
                if (int.TryParse(value, out _))
                    return true;
                var enumName = type[1..];
                if (!string.IsNullOrEmpty(enumName)
                    && enumValueMap.TryGetValue(enumName, out var map)
                    && map.ContainsKey(value ?? ""))
                    return true;
                reason = $"枚举值不存在: {value}";
                return false;
            }

            return TryValidateSimple(type, value, objectSep, out reason);
        }

        private static bool TryValidateSimple(string type, string value, string objectSep, out string reason)
        {
            reason = null;
            var simple = type;
            var dotIndex = simple.LastIndexOf('.');
            if (dotIndex >= 0)
                simple = simple[(dotIndex + 1)..];

            switch (simple.ToLowerInvariant())
            {
                case "int":
                    if (string.IsNullOrEmpty(value) || int.TryParse(value, out _))
                        return true;
                    reason = "不是有效的 int";
                    return false;
                case "long":
                    if (string.IsNullOrEmpty(value) || long.TryParse(value, out _))
                        return true;
                    reason = "不是有效的 long";
                    return false;
                case "float":
                    if (string.IsNullOrEmpty(value) || float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        return true;
                    reason = "不是有效的 float";
                    return false;
                case "double":
                    if (string.IsNullOrEmpty(value) || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        return true;
                    reason = "不是有效的 double";
                    return false;
                case "bool":
                    if (string.IsNullOrEmpty(value))
                        return true;
                    if (value == "1" || value == "0"
                        || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("false", StringComparison.OrdinalIgnoreCase))
                        return true;
                    reason = "不是有效的 bool(1/0/true/false)";
                    return false;
                case "string":
                    return true;
                case "vector2":
                    return ValidateVector(value, objectSep, 2, false, out reason);
                case "vector3":
                    return ValidateVector(value, objectSep, 3, false, out reason);
                case "vector4":
                    return ValidateVector(value, objectSep, 4, false, out reason);
                case "vector2int":
                    return ValidateVector(value, objectSep, 2, true, out reason);
                case "vector3int":
                    return ValidateVector(value, objectSep, 3, true, out reason);
                case "color":
                    return ValidateVector(value, objectSep, 4, false, out reason);
                case "rect":
                    return ValidateVector(value, objectSep, 4, false, out reason);
            }

            return true;
        }

        private static bool ValidateVector(string value, string separator, int components, bool integer, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(value))
                return true;

            var parts = value.Split(separator[0]);
            var count = Math.Min(parts.Length, components);
            for (int i = 0; i < count; i++)
            {
                var part = parts[i];
                if (string.IsNullOrEmpty(part))
                {
                    reason = $"第{i + 1}个分量为空";
                    return false;
                }

                if (integer)
                {
                    if (!int.TryParse(part, out _))
                    {
                        reason = $"第{i + 1}个分量不是有效的 int";
                        return false;
                    }
                }
                else
                {
                    if (!float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        reason = $"第{i + 1}个分量不是有效的 float";
                        return false;
                    }
                }
            }

            return true;
        }

    }
}
