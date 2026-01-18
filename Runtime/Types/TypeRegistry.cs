using System;
using System.Collections.Generic;
using System.Reflection;

namespace Azcel
{
    /// <summary>
    /// 类型解析器接口
    /// </summary>
    public interface ITypeParser
    {
        /// <summary>
        /// C#类型名
        /// </summary>
        string CSharpTypeName { get; }

        /// <summary>
        /// 是否为值类型
        /// </summary>
        bool IsValueType { get; }

        /// <summary>
        /// 默认值表达式
        /// </summary>
        string DefaultValueExpression { get; }

        /// <summary>
        /// 解析字符串为值
        /// </summary>
        object Parse(string value, string separator);

        /// <summary>
        /// 生成解析代码
        /// </summary>
        string GenerateParseCode(string valueExpr, string separatorExpr);

        /// <summary>
        /// 生成二进制读取代码
        /// </summary>
        string GenerateBinaryReadCode(string readerExpr);

        /// <summary>
        /// 生成二进制写入代码
        /// </summary>
        string GenerateBinaryWriteCode(string writerExpr, string valueExpr);

        /// <summary>
        /// 将字符串值序列化为基础写入操作
        /// </summary>
        void Serialize(IValueWriter writer, string value, string arraySep, string objectSep);
    }

    /// <summary>
    /// 类型注册表
    /// </summary>
    public static class TypeRegistry
    {
        private static readonly Dictionary<string, ITypeParser> _parsers = new(StringComparer.OrdinalIgnoreCase);
        private static bool _scanned;

        static TypeRegistry()
        {
            // 注册内置类型
            BuiltinTypes.RegisterAll();
        }

        /// <summary>
        /// 注册类型解析器
        /// </summary>
        public static void Register(string typeName, ITypeParser parser)
        {
            _parsers[typeName] = parser;
        }

        /// <summary>
        /// 获取类型解析器
        /// </summary>
        public static ITypeParser Get(string typeName)
        {
            EnsureScanned();
            // 处理数组类型
            if (typeName.EndsWith("[]"))
            {
                var elementType = typeName[..^2];
                var elementParser = Get(elementType);
                if (elementParser != null)
                    return new ArrayTypeParser(elementParser);
            }

            // 处理字典类型 map<K,V>
            if (typeName.StartsWith("map<") && typeName.EndsWith(">"))
            {
                var inner = typeName[4..^1];
                var commaIndex = inner.IndexOf(',');
                if (commaIndex > 0)
                {
                    var keyType = inner[..commaIndex].Trim();
                    var valueType = inner[(commaIndex + 1)..].Trim();
                    var keyParser = Get(keyType);
                    var valueParser = Get(valueType);
                    if (keyParser != null && valueParser != null)
                        return new DictionaryTypeParser(keyParser, valueParser);
                }
            }

            // 处理表引用 @TableName
            if (typeName.StartsWith("@"))
            {
                var tableName = typeName[1..];
                return new TableRefTypeParser(tableName);
            }

            // 处理枚举引用 #EnumName
            if (typeName.StartsWith("#"))
            {
                var enumName = typeName[1..];
                return new EnumRefTypeParser(enumName);
            }

            return _parsers.TryGetValue(typeName, out var parser) ? parser : null;
        }

        private static void EnsureScanned()
        {
            if (_scanned)
                return;

            _scanned = true;

#if UNITY_EDITOR
            foreach (var type in UnityEditor.TypeCache.GetTypesWithAttribute<TypeParserPluginAttribute>())
            {
                if (type == null || type.IsAbstract || type.IsInterface)
                    continue;
                if (!typeof(ITypeParser).IsAssignableFrom(type))
                    continue;
                if (type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                try
                {
                    var attr = (TypeParserPluginAttribute)Attribute.GetCustomAttribute(type, typeof(TypeParserPluginAttribute));
                    var instance = Activator.CreateInstance(type) as ITypeParser;
                    var typeName = attr?.TypeName;
                    if (string.IsNullOrEmpty(typeName))
                        continue;
                    Register(typeName, instance);
                }
                catch
                {
                    // 忽略无法实例化的插件
                }
            }
#else
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                if (types == null)
                    continue;

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface)
                        continue;
                    if (!typeof(ITypeParser).IsAssignableFrom(type))
                        continue;
                    if (!Attribute.IsDefined(type, typeof(TypeParserPluginAttribute)))
                        continue;
                    if (type.GetConstructor(Type.EmptyTypes) == null)
                        continue;

                    try
                    {
                        var attr = (TypeParserPluginAttribute)Attribute.GetCustomAttribute(type, typeof(TypeParserPluginAttribute));
                        var instance = Activator.CreateInstance(type) as ITypeParser;
                        var typeName = attr?.TypeName;
                        if (string.IsNullOrEmpty(typeName))
                            continue;
                        Register(typeName, instance);
                    }
                    catch
                    {
                        // 忽略无法实例化的插件
                    }
                }
            }
#endif
        }

        /// <summary>
        /// 检查类型是否已注册
        /// </summary>
        public static bool IsRegistered(string typeName)
        {
            return Get(typeName) != null;
        }
    }
}
