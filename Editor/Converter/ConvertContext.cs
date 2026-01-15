using System;
using System.Collections.Generic;
using Azathrix.Framework.Core.Pipeline;

namespace Azcel.Editor
{
    /// <summary>
    /// 转换上下文
    /// </summary>
    public class ConvertContext : PipelineContext
    {
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
        }

        /// <summary>
        /// 添加警告
        /// </summary>
        public void AddWarning(string message)
        {
            Warnings.Add(message);
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

        // 行配置
        public int FieldRow { get; set; } = 2;
        public int TypeRow { get; set; } = 3;
        public int CommentRow { get; set; } = 4;
        public int DataRow { get; set; } = 5;
    }

    /// <summary>
    /// 字段定义
    /// </summary>
    public class FieldDefinition
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Comment { get; set; }
        public bool IsKey { get; set; }
        public bool IsIndex { get; set; }
        public bool IsTableRef { get; set; }
        public string RefTableName { get; set; }
        public bool IsEnumRef { get; set; }
        public string RefEnumName { get; set; }
    }

    /// <summary>
    /// 行数据
    /// </summary>
    public class RowData
    {
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
    }
}
