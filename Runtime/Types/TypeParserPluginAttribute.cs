using System;

namespace Azcel
{
    /// <summary>
    /// 类型解析器插件标记（用于动态扫描注册）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TypeParserPluginAttribute : Attribute
    {
        public string TypeName { get; }

        public TypeParserPluginAttribute(string typeName)
        {
            TypeName = typeName;
        }
    }
}
