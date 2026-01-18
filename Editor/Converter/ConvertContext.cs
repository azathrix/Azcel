using System;
using System.Collections.Generic;
using System.IO;
using Azathrix.Framework.Core.Pipeline;

namespace Azcel.Editor
{
    /// <summary>
    /// 转换上下文
    /// </summary>
    public class ConvertContext : PipelineContext
    {
        public sealed class PerfRecord
        {
            public string Phase { get; }
            public string Kind { get; }
            public string Name { get; }
            public int Rows { get; }
            public long Milliseconds { get; }

            public PerfRecord(string phase, string kind, string name, int rows, long milliseconds)
            {
                Phase = phase;
                Kind = kind;
                Name = name;
                Rows = rows;
                Milliseconds = milliseconds;
            }
        }

        /// <summary>
        /// Excel 临时目录（非空则覆盖设置里的 excelPaths）
        /// </summary>
        public string TempExcelPath { get; set; }

        /// <summary>
        /// 临时代码输出目录
        /// </summary>
        public string TempCodeOutputPath { get; set; }

        /// <summary>
        /// 临时数据输出目录
        /// </summary>
        public string TempDataOutputPath { get; set; }

        /// <summary>
        /// 跳过 AssetDatabase.Refresh（用于测试）
        /// </summary>
        public bool SkipAssetRefresh { get; set; }

        /// <summary>
        /// 性能记录
        /// </summary>
        public List<PerfRecord> PerfRecords { get; } = new();

        /// <summary>
        /// 临时Excel路径映射（临时 -> 原始）
        /// </summary>
        public Dictionary<string, string> TempExcelPathMap { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 阶段总耗时
        /// </summary>
        public Dictionary<string, long> PhaseTotals { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 导出数据大小（按配置名）
        /// </summary>
        public Dictionary<string, long> DataSizes { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 解析后的表定义
        /// </summary>
        public List<TableDefinition> Tables { get; } = new();

        /// <summary>
        /// 解析后的枚举定义
        /// </summary>
        public List<EnumDefinition> Enums { get; } = new();

        /// <summary>
        /// 解析后的全局配置定义
        /// </summary>
        public List<GlobalDefinition> Globals { get; } = new();

        /// <summary>
        /// 错误列表
        /// </summary>
        public List<string> Errors { get; } = new();

        /// <summary>
        /// 警告列表
        /// </summary>
        public List<string> Warnings { get; } = new();

        /// <summary>
        /// 添加错误
        /// </summary>
        public void AddError(string message)
        {
            Errors.Add(message);
            Aborted = true;
        }

        /// <summary>
        /// 添加警告
        /// </summary>
        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }

        public void AddPerfRecord(string phase, string kind, string name, int rows, long milliseconds)
        {
            PerfRecords.Add(new PerfRecord(phase, kind, name, rows, milliseconds));
        }

        public void SetPhaseTotal(string phase, long milliseconds)
        {
            if (string.IsNullOrEmpty(phase))
                return;
            PhaseTotals[phase] = milliseconds;
        }

        public void SetDataSize(string configName, long bytes)
        {
            if (string.IsNullOrEmpty(configName))
                return;
            DataSizes[configName] = Math.Max(0, bytes);
        }

        public string ResolveOriginalExcelPath(string excelPath)
        {
            if (string.IsNullOrEmpty(excelPath))
                return excelPath;

            var full = Path.GetFullPath(excelPath);
            return TempExcelPathMap.TryGetValue(full, out var original) ? original : excelPath;
        }
    }

    /// <summary>
    /// 表定义
    /// </summary>
    public class TableDefinition
    {
        public string Name { get; set; }
        public string ExcelPath { get; set; }
        public List<FieldDefinition> Fields { get; } = new();
        public List<RowData> Rows { get; } = new();

        // 配置
        public string KeyField { get; set; } = "Id";
        public string KeyType { get; set; } = "int";
        public string ParentTable { get; set; }
        public List<string> IndexFields { get; } = new();
        public string ArraySeparator { get; set; } = "|";
        public string ObjectSeparator { get; set; } = ",";
        public bool FieldKeymap { get; set; }

        // 行配置
        public int FieldRow { get; set; } = 2;
        public int TypeRow { get; set; } = 3;
    }

    /// <summary>
    /// 字段定义
    /// </summary>
    public class FieldDefinition
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Comment { get; set; }
        public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool IsKey { get; set; }
        public bool IsIndex { get; set; }
        public bool IsTableRef { get; set; }
        public string RefTableName { get; set; }
        public bool IsEnumRef { get; set; }
        public string RefEnumName { get; set; }
        public string SourceExcelPath { get; set; }
        public string SourceSheetName { get; set; }
        public int SourceRowIndex { get; set; }
        public int SourceColumnIndex { get; set; }
    }

    /// <summary>
    /// 行数据
    /// </summary>
    public class RowData
    {
        public int RowIndex { get; set; }
        public string SourceExcelPath { get; set; }
        public string SourceSheetName { get; set; }
        public Dictionary<string, string> Values { get; } = new();
    }

    /// <summary>
    /// 枚举定义
    /// </summary>
    public class EnumDefinition
    {
        public string Name { get; set; }
        public string ExcelPath { get; set; }
        public List<EnumValueDefinition> Values { get; } = new();
    }

    /// <summary>
    /// 枚举值定义
    /// </summary>
    public class EnumValueDefinition
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public string Comment { get; set; }
    }

    /// <summary>
    /// 全局配置定义
    /// </summary>
    public class GlobalDefinition
    {
        public string Name { get; set; }
        public string ExcelPath { get; set; }
        public List<GlobalValueDefinition> Values { get; } = new();
    }

    /// <summary>
    /// 全局配置值定义
    /// </summary>
    public class GlobalValueDefinition
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
        public string Comment { get; set; }
        public int RowIndex { get; set; }
        public string SourceExcelPath { get; set; }
        public string SourceSheetName { get; set; }
    }
}
