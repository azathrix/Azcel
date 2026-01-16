#if AZCEL_EXCEL_READER
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;
using ExcelDataReader;

namespace Azcel.Editor
{
    /// <summary>
    /// Excel解析阶段
    /// </summary>
    [PhaseId("ExcelParse")]
    [Register]
    public class ExcelParsePhase : IConvertPhase
    {
        public int Order => 100;

        // 默认解析配置
        private const string DefaultKeyField = "Id";
        private const string DefaultKeyType = "int";
        private const int DefaultFieldRow = 2;
        private const int DefaultTypeRow = 3;
        private const int DefaultCommentRow = 4;
        private const int DefaultDataRow = 5;

        public UniTask ExecuteAsync(ConvertContext context)
        {
            var settings = AzcelSettings.Instance;

            // 注册编码提供程序（ExcelDataReader需要，用于读取.xls文件）
            RegisterCodePagesEncodingProvider();

            // 临时存储，用于合表（按表名分组）
            var tableDataMap = new Dictionary<string, List<(TableDefinition def, int depth)>>();
            var enumDataMap = new Dictionary<string, List<(EnumDefinition def, int depth)>>();
            var globalDataMap = new Dictionary<string, List<(GlobalDefinition def, int depth)>>();

            // 遍历所有Excel目录
            foreach (var excelPath in settings.excelPaths)
            {
                if (!Directory.Exists(excelPath))
                {
                    context.AddWarning($"Excel目录不存在: {excelPath}");
                    continue;
                }

                // 递归扫描所有Excel文件
                ScanDirectory(excelPath, 0, settings, context, tableDataMap, enumDataMap, globalDataMap);
            }

            // 合并表数据（深度大的覆盖深度小的）
            MergeTables(context, tableDataMap);
            MergeEnums(context, enumDataMap);
            MergeGlobals(context, globalDataMap);

            Log.Info($"[ExcelParse] 解析完成: {context.Tables.Count} 表, {context.Enums.Count} 枚举, {context.Globals.Count} 全局配置");
            return UniTask.CompletedTask;
        }

        private void ScanDirectory(string dirPath, int depth, AzcelSettings settings, ConvertContext context,
            Dictionary<string, List<(TableDefinition, int)>> tableDataMap,
            Dictionary<string, List<(EnumDefinition, int)>> enumDataMap,
            Dictionary<string, List<(GlobalDefinition, int)>> globalDataMap)
        {
            // 扫描当前目录的Excel文件
            var xlsxFiles = Directory.GetFiles(dirPath, "*.xlsx", SearchOption.TopDirectoryOnly);
            var xlsFiles = Directory.GetFiles(dirPath, "*.xls", SearchOption.TopDirectoryOnly)
                .Where(f => !f.EndsWith(".xlsx"));

            foreach (var file in xlsxFiles.Concat(xlsFiles))
            {
                if (Path.GetFileName(file).StartsWith("~$"))
                    continue;

                try
                {
                    ParseExcelFile(file, depth, settings, context, tableDataMap, enumDataMap, globalDataMap);
                }
                catch (Exception e)
                {
                    context.AddError($"解析Excel失败 [{file}]: {e.Message}");
                }
            }

            // 递归扫描子目录
            foreach (var subDir in Directory.GetDirectories(dirPath))
            {
                ScanDirectory(subDir, depth + 1, settings, context, tableDataMap, enumDataMap, globalDataMap);
            }
        }

        private void ParseExcelFile(string filePath, int depth, AzcelSettings settings, ConvertContext context,
            Dictionary<string, List<(TableDefinition, int)>> tableDataMap,
            Dictionary<string, List<(EnumDefinition, int)>> enumDataMap,
            Dictionary<string, List<(GlobalDefinition, int)>> globalDataMap)
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataSet = reader.AsDataSet();

            foreach (DataTable sheet in dataSet.Tables)
            {
                if (sheet.Rows.Count < 2)
                    continue;

                var configRow = sheet.Rows[0];
                var tableName = configRow[0]?.ToString()?.Trim();

                if (string.IsNullOrEmpty(tableName))
                    continue;

                if (tableName.StartsWith("Enum."))
                {
                    var enumDef = ParseEnumTable(sheet, tableName[5..], filePath, settings);
                    if (!enumDataMap.ContainsKey(enumDef.Name))
                        enumDataMap[enumDef.Name] = new List<(EnumDefinition, int)>();
                    enumDataMap[enumDef.Name].Add((enumDef, depth));
                }
                else if (tableName.StartsWith("GlobalConfig."))
                {
                    var globalDef = ParseGlobalTable(sheet, tableName[13..], filePath, settings);
                    if (!globalDataMap.ContainsKey(globalDef.Name))
                        globalDataMap[globalDef.Name] = new List<(GlobalDefinition, int)>();
                    globalDataMap[globalDef.Name].Add((globalDef, depth));
                }
                else
                {
                    var tableDef = ParseDataTable(sheet, tableName, filePath, settings);
                    if (!tableDataMap.ContainsKey(tableDef.Name))
                        tableDataMap[tableDef.Name] = new List<(TableDefinition, int)>();
                    tableDataMap[tableDef.Name].Add((tableDef, depth));
                }
            }
        }

        private void MergeTables(ConvertContext context, Dictionary<string, List<(TableDefinition def, int depth)>> dataMap)
        {
            foreach (var kvp in dataMap)
            {
                var sorted = kvp.Value.OrderBy(x => x.depth).ToList();
                var merged = sorted[0].def;

                // 合并数据行（深度大的覆盖深度小的）
                var keyField = merged.KeyField;
                var rowMap = new Dictionary<string, RowData>();

                foreach (var (def, _) in sorted)
                {
                    foreach (var row in def.Rows)
                    {
                        if (row.Values.TryGetValue(keyField, out var key) && !string.IsNullOrEmpty(key))
                            rowMap[key] = row; // 后面的覆盖前面的
                    }
                }

                merged.Rows.Clear();
                merged.Rows.AddRange(rowMap.Values);
                context.Tables.Add(merged);
            }
        }

        private void MergeEnums(ConvertContext context, Dictionary<string, List<(EnumDefinition def, int depth)>> dataMap)
        {
            foreach (var kvp in dataMap)
            {
                var sorted = kvp.Value.OrderBy(x => x.depth).ToList();
                var merged = sorted[0].def;

                var valueMap = new Dictionary<string, EnumValueDefinition>();
                foreach (var (def, _) in sorted)
                {
                    foreach (var val in def.Values)
                        valueMap[val.Name] = val;
                }

                merged.Values.Clear();
                merged.Values.AddRange(valueMap.Values);
                context.Enums.Add(merged);
            }
        }

        private void MergeGlobals(ConvertContext context, Dictionary<string, List<(GlobalDefinition def, int depth)>> dataMap)
        {
            foreach (var kvp in dataMap)
            {
                var sorted = kvp.Value.OrderBy(x => x.depth).ToList();
                var merged = sorted[0].def;

                var valueMap = new Dictionary<string, GlobalValueDefinition>();
                foreach (var (def, _) in sorted)
                {
                    foreach (var val in def.Values)
                        valueMap[val.Key] = val;
                }

                merged.Values.Clear();
                merged.Values.AddRange(valueMap.Values);
                context.Globals.Add(merged);
            }
        }

        private TableDefinition ParseDataTable(DataTable sheet, string tableName, string filePath, AzcelSettings settings)
        {
            var table = new TableDefinition
            {
                Name = tableName,
                ExcelPath = filePath,
                KeyField = DefaultKeyField,
                KeyType = DefaultKeyType,
                ArraySeparator = settings.arraySeparator,
                ObjectSeparator = settings.objectSeparator,
                FieldRow = DefaultFieldRow,
                TypeRow = DefaultTypeRow,
                CommentRow = DefaultCommentRow,
                DataRow = DefaultDataRow
            };

            // 解析配置行参数
            var configRow = sheet.Rows[0];
            for (int col = 1; col < configRow.ItemArray.Length; col++)
            {
                var param = configRow[col]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(param))
                    continue;

                var parts = param.Split(':');
                if (parts.Length != 2)
                    continue;

                var key = parts[0].Trim().ToLower();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "key": table.KeyField = value; break;
                    case "keytype": table.KeyType = value; break;
                    case "extends": table.ParentTable = value; break;
                    case "arrayseparator": table.ArraySeparator = value; break;
                    case "objectseparator": table.ObjectSeparator = value; break;
                    case "index": table.IndexFields.AddRange(value.Split(',')); break;
                    case "fieldrow": table.FieldRow = int.Parse(value); break;
                    case "typerow": table.TypeRow = int.Parse(value); break;
                    case "commentrow": table.CommentRow = int.Parse(value); break;
                    case "datarow": table.DataRow = int.Parse(value); break;
                }
            }

            // 解析字段
            var fieldRow = sheet.Rows[table.FieldRow - 1];
            var typeRow = sheet.Rows[table.TypeRow - 1];
            var commentRow = table.CommentRow <= sheet.Rows.Count ? sheet.Rows[table.CommentRow - 1] : null;

            for (int col = 0; col < fieldRow.ItemArray.Length; col++)
            {
                var fieldName = fieldRow[col]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(fieldName) || fieldName.StartsWith("#"))
                    continue;

                var fieldType = col < typeRow.ItemArray.Length ? typeRow[col]?.ToString()?.Trim() : "string";
                var comment = commentRow != null && col < commentRow.ItemArray.Length
                    ? commentRow[col]?.ToString()?.Trim()
                    : "";

                var field = new FieldDefinition
                {
                    Name = fieldName,
                    Type = fieldType ?? "string",
                    Comment = comment ?? "",
                    IsKey = fieldName.Equals(table.KeyField, StringComparison.OrdinalIgnoreCase),
                    IsIndex = table.IndexFields.Contains(fieldName)
                };

                if (fieldType?.StartsWith("@") == true)
                {
                    field.IsTableRef = true;
                    field.RefTableName = fieldType[1..];
                }
                else if (fieldType?.StartsWith("#") == true)
                {
                    field.IsEnumRef = true;
                    field.RefEnumName = fieldType[1..];
                }

                table.Fields.Add(field);
            }

            // 解析数据行
            for (int row = table.DataRow - 1; row < sheet.Rows.Count; row++)
            {
                var dataRow = sheet.Rows[row];
                var rowData = new RowData();
                var hasData = false;

                for (int col = 0; col < table.Fields.Count && col < dataRow.ItemArray.Length; col++)
                {
                    var value = dataRow[col]?.ToString() ?? "";
                    rowData.Values[table.Fields[col].Name] = value;
                    if (!string.IsNullOrEmpty(value))
                        hasData = true;
                }

                if (hasData)
                    table.Rows.Add(rowData);
            }

            return table;
        }

        private EnumDefinition ParseEnumTable(DataTable sheet, string enumName, string filePath, AzcelSettings settings)
        {
            var enumDef = new EnumDefinition
            {
                Name = enumName,
                ExcelPath = filePath
            };

            for (int row = DefaultDataRow - 1; row < sheet.Rows.Count; row++)
            {
                var dataRow = sheet.Rows[row];
                var name = dataRow[0]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;

                var valueStr = dataRow.ItemArray.Length > 1 ? dataRow[1]?.ToString()?.Trim() : "";
                var comment = dataRow.ItemArray.Length > 2 ? dataRow[2]?.ToString()?.Trim() : "";

                enumDef.Values.Add(new EnumValueDefinition
                {
                    Name = name,
                    Value = int.TryParse(valueStr, out var v) ? v : enumDef.Values.Count,
                    Comment = comment ?? ""
                });
            }

            return enumDef;
        }

        private GlobalDefinition ParseGlobalTable(DataTable sheet, string globalName, string filePath, AzcelSettings settings)
        {
            var globalDef = new GlobalDefinition
            {
                Name = globalName,
                ExcelPath = filePath
            };

            for (int row = DefaultDataRow - 1; row < sheet.Rows.Count; row++)
            {
                var dataRow = sheet.Rows[row];
                var key = dataRow[0]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(key))
                    continue;

                var value = dataRow.ItemArray.Length > 1 ? dataRow[1]?.ToString() : "";
                var type = dataRow.ItemArray.Length > 2 ? dataRow[2]?.ToString()?.Trim() : "string";

                globalDef.Values.Add(new GlobalValueDefinition
                {
                    Key = key,
                    Value = value ?? "",
                    Type = type ?? "string"
                });
            }

            return globalDef;
        }

        private static bool _encodingRegistered;

        private static void RegisterCodePagesEncodingProvider()
        {
            if (_encodingRegistered) return;
            _encodingRegistered = true;

            try
            {
                var providerType = Type.GetType("System.Text.CodePagesEncodingProvider, System.Text.Encoding.CodePages");
                if (providerType == null) return;

                var instanceProp = providerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp == null) return;

                var provider = instanceProp.GetValue(null) as System.Text.EncodingProvider;
                if (provider != null)
                    System.Text.Encoding.RegisterProvider(provider);
            }
            catch
            {
                // 忽略错误，.xlsx 文件不需要这个编码提供程序
            }
        }
    }
}
#else
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azcel.Editor
{
    /// <summary>
    /// Excel解析阶段（ExcelDataReader未安装时的占位实现）
    /// </summary>
    [PhaseId("ExcelParse")]
    public class ExcelParsePhase : IConvertPhase
    {
        public int Order => 100;

        public UniTask ExecuteAsync(ConvertContext context)
        {
            context.AddError("ExcelDataReader 未安装。请通过环境管理器安装 ExcelDataReader 依赖。");
            Log.Error("[ExcelParse] ExcelDataReader 未安装，无法解析Excel文件");
            return UniTask.CompletedTask;
        }
    }
}
#endif
