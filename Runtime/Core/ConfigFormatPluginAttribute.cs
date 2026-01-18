using System;

namespace Azcel
{
    /// <summary>
    /// 配置格式插件标记（用于扫描注册）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ConfigFormatPluginAttribute : Attribute
    {
        public string FormatId { get; }

        public ConfigFormatPluginAttribute(string formatId)
        {
            FormatId = formatId;
        }
    }
}
