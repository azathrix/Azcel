using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Azathrix.Framework.Tools;

namespace Azcel.Editor
{
    public interface IConfigDataExporter
    {
        void Export(ConvertContext context, string outputPath);
    }

    public static class ConfigDataExporterRegistry
    {
        private static readonly Dictionary<string, IConfigDataExporter> Exporters = new(StringComparer.OrdinalIgnoreCase);

        static ConfigDataExporterRegistry()
        {
            Register(ConfigFormatIds.Binary, new BinaryConfigDataExporter());
        }

        public static IConfigDataExporter Current { get; set; } = new BinaryConfigDataExporter();

        public static void Register(string formatId, IConfigDataExporter exporter)
        {
            if (string.IsNullOrEmpty(formatId) || exporter == null)
                return;

            Exporters[formatId] = exporter;
        }

        public static IConfigDataExporter Get(string formatId)
        {
            if (string.IsNullOrEmpty(formatId))
                return null;

            return Exporters.TryGetValue(formatId, out var exporter) ? exporter : null;
        }
    }

    public sealed class BinaryConfigDataExporter : IConfigDataExporter
    {
        public void Export(ConvertContext context, string outputPath)
        {
            var enumValueMap = BuildEnumValueMap(context);
            var totalWatch = Stopwatch.StartNew();

            foreach (var table in context.Tables)
            {
                var watch = Stopwatch.StartNew();
                var filePath = Path.Combine(outputPath, $"{table.Name}.bytes");
                using var stream = File.Create(filePath);
                using var writer = new BinaryWriter(stream);

                writer.Write(table.Rows.Count);
                foreach (var row in table.Rows)
                {
                    foreach (var field in table.Fields)
                    {
                        if (IsSkipped(field))
                            continue;

                        var value = row.Values.TryGetValue(field.Name, out var v) ? v : "";
                        if (!TryValidateValue(table, field, row, value, enumValueMap, out var error))
                        {
                            context.AddError(error);
                            return;
                        }
                        WriteValue(writer, field.Type, value, table.ArraySeparator, table.ObjectSeparator, enumValueMap);
                    }
                }

                writer.Flush();
                stream.Flush();
                watch.Stop();
                context.SetDataSize(table.Name, stream.Length);
                context.AddPerfRecord("DataExport", "Table", table.Name, table.Rows.Count, watch.ElapsedMilliseconds);
            }

            foreach (var global in context.Globals)
            {
                var watch = Stopwatch.StartNew();
                var filePath = Path.Combine(outputPath, $"GlobalConfig{global.Name}.bytes");
                using var stream = File.Create(filePath);
                using var writer = new BinaryWriter(stream);

                writer.Write(global.Values.Count);
                foreach (var value in global.Values)
                {
                    if (!TryValidateGlobalValue(global, value, enumValueMap, out var error))
                    {
                        context.AddError(error);
                        return;
                    }
                    writer.Write(value.Key);
                    writer.Write(value.Type);
                    writer.Write(value.Value);
                }

                writer.Flush();
                stream.Flush();
                watch.Stop();
                var globalConfigName = $"GlobalConfig{global.Name}";
                context.SetDataSize(globalConfigName, stream.Length);
                context.AddPerfRecord("DataExport", "Global", $"GlobalConfig{global.Name}", global.Values.Count,
                    watch.ElapsedMilliseconds);
            }

            totalWatch.Stop();
            context.SetPhaseTotal("DataExport", totalWatch.ElapsedMilliseconds);
        }

        private static void WriteValue(BinaryWriter writer, string type, string value, string arraySep, string objectSep,
            Dictionary<string, Dictionary<string, int>> enumValueMap)
        {
            if (!string.IsNullOrEmpty(type) && type.StartsWith("#", StringComparison.Ordinal))
            {
                var enumName = type[1..];
                if (!int.TryParse(value, out var enumValue))
                {
                    if (enumValueMap.TryGetValue(enumName, out var map) && map.TryGetValue(value ?? "", out var mapped))
                        value = mapped.ToString();
                    else
                        value = "0";
                }
            }

            AzcelBinary.WriteValue(writer, type, value, arraySep, objectSep);
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
                error = $"[DataExport] 表 {table.Name}{rowInfo} 字段 {field.Name} 类型 {field.Type} 值 \"{value}\" 无法解析：{reason}";
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

            if (!TryValidateByType(value.Type, value.Value, null, null, enumValueMap, out var reason))
            {
                error = $"[DataExport] 全局 {global.Name} 键 {value.Key} 类型 {value.Type} 值 \"{value.Value}\" 无法解析：{reason}";
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

            if (TryParseMapType(type, out var keyType, out var valueType))
            {
                if (string.IsNullOrEmpty(value))
                    return true;

                var entries = value.Split(arraySep[0]);
                for (int i = 0; i < entries.Length; i++)
                {
                    SplitKeyValue(entries[i], objectSep, out var key, out var val);
                    if (!TryValidateByType(keyType, key, arraySep, objectSep, enumValueMap, out reason))
                    {
                        reason = $"字典键[{i}] 无法解析：{reason}";
                        return false;
                    }
                    if (!TryValidateByType(valueType, val, arraySep, objectSep, enumValueMap, out reason))
                    {
                        reason = $"字典值[{i}] 无法解析：{reason}";
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

        private static bool TryParseMapType(string type, out string keyType, out string valueType)
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
    }
}
