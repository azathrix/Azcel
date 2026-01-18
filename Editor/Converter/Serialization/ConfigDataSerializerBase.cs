using System;
using System.Collections.Generic;
using System.Diagnostics;
using Azcel;

namespace Azcel.Editor
{
    /// <summary>
    /// 配置数据序列化基类（负责遍历表/行/字段）
    /// </summary>
    public abstract class ConfigDataSerializerBase
    {
        public void Serialize(ConvertContext context, string outputPath)
        {
            var enumValueMap = BuildEnumValueMap(context);
            var totalWatch = Stopwatch.StartNew();

            foreach (var table in context.Tables)
            {
                var watch = Stopwatch.StartNew();
                var writer = BeginTable(table, outputPath);

                foreach (var row in table.Rows)
                {
                    BeginRow(table, row, writer);
                    foreach (var field in table.Fields)
                    {
                        if (IsSkipped(field))
                            continue;

                        var raw = row.Values.TryGetValue(field.Name, out var v) ? v : "";
                        var normalized = NormalizeEnumValue(field.Type, raw, table.ArraySeparator, table.ObjectSeparator, enumValueMap);
                        var parser = TypeRegistry.Get(field.Type);
                        if (parser == null)
                        {
                            context.AddError($"[DataExport] 表 {table.Name} 字段 {field.Name} 类型 {field.Type} 未注册");
                            break;
                        }

                        WriteField(table, row, field, writer, parser, normalized);
                    }
                    EndRow(table, row, writer);
                    if (context.Errors.Count > 0)
                        break;
                }

                var size = EndTable(table, writer);
                watch.Stop();
                if (size >= 0)
                    context.SetDataSize(table.Name, size);
                context.AddPerfRecord("DataExport", "Table", table.Name, table.Rows.Count, watch.ElapsedMilliseconds);
                if (context.Errors.Count > 0)
                    return;
            }

            totalWatch.Stop();
            context.SetPhaseTotal("DataExport", totalWatch.ElapsedMilliseconds);
        }

        protected virtual void BeginRow(TableDefinition table, RowData row, IValueWriter writer) { }
        protected virtual void EndRow(TableDefinition table, RowData row, IValueWriter writer) { }

        protected abstract IValueWriter BeginTable(TableDefinition table, string outputPath);
        protected virtual void WriteField(TableDefinition table, RowData row, FieldDefinition field, IValueWriter writer,
            ITypeParser parser, string normalizedValue)
        {
            writer.WritePropertyName(field.Name);
            parser.Serialize(writer, normalizedValue, table.ArraySeparator, table.ObjectSeparator);
        }
        protected abstract long EndTable(TableDefinition table, IValueWriter writer);

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

        private static string NormalizeEnumValue(string type, string value, string arraySep, string objectSep,
            Dictionary<string, Dictionary<string, int>> enumValueMap)
        {
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(value))
                return value ?? "";

            arraySep ??= AzcelSettings.Instance?.arraySeparator ?? "|";
            objectSep ??= AzcelSettings.Instance?.objectSeparator ?? ",";

            if (type.EndsWith("[]", StringComparison.Ordinal))
            {
                var elementType = type[..^2];
                var parts = value.Split(arraySep[0]);
                for (int i = 0; i < parts.Length; i++)
                    parts[i] = NormalizeEnumValue(elementType, parts[i], arraySep, objectSep, enumValueMap);
                return string.Join(arraySep, parts);
            }

            if (type.StartsWith("#", StringComparison.Ordinal))
            {
                if (int.TryParse(value, out _))
                    return value;

                var enumName = type[1..];
                if (enumValueMap.TryGetValue(enumName, out var map) && map.TryGetValue(value, out var mapped))
                    return mapped.ToString();
            }

            return value ?? "";
        }

    }
}
