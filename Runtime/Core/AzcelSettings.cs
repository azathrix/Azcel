using System;
using System.Collections.Generic;
using Azathrix.Framework.Settings;
using UnityEngine;

namespace Azcel
{
    /// <summary>
    /// Azcel 全局配置
    /// </summary>
    [SettingsPath("AzcelSettings")]
    [ShowSetting("Azcel配置")]
    public class AzcelSettings : SettingsBase<AzcelSettings>
    {
        [Header("路径配置")]
        [Tooltip("Excel文件目录列表")]
        public List<string> excelPaths = new() { "Assets/Excel" };

        [Tooltip("生成的C#代码输出目录")]
        public string codeOutputPath = "Assets/Scripts/Tables";

        [Tooltip("生成的数据文件输出目录")]
        public string dataOutputPath = "Assets/Resources/TableData";

        [Header("代码生成")]
        [Tooltip("生成代码的命名空间")]
        public string codeNamespace = "Game.Tables";

        [Header("数据格式")]
        [Tooltip("数据输出格式")]
        public DataFormat dataFormat = DataFormat.Binary;

        [Tooltip("数组元素分隔符")]
        public string arraySeparator = "|";

        [Tooltip("对象字段分隔符")]
        public string objectSeparator = ",";
    }

    /// <summary>
    /// 数据输出格式
    /// </summary>
    public enum DataFormat
    {
        Binary,
        Json
    }
}
