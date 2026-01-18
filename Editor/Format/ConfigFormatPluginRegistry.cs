using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Azcel.Editor
{
    /// <summary>
    /// 编辑器格式插件注册表（按需扫描）
    /// </summary>
    public static class ConfigFormatPluginRegistry
    {
        private static readonly Dictionary<string, IConfigFormat> Plugins = new(StringComparer.OrdinalIgnoreCase);
        private static bool _scanned;

        public static IConfigFormat Get(string formatId)
        {
            EnsureScanned();
            if (string.IsNullOrEmpty(formatId))
                return null;
            return Plugins.TryGetValue(formatId, out var plugin) ? plugin : null;
        }

        public static string[] GetFormatIdArray()
        {
            EnsureScanned();
            return Plugins.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static void EnsureScanned()
        {
            if (_scanned)
                return;

            _scanned = true;
            foreach (var type in TypeCache.GetTypesWithAttribute<ConfigFormatPluginAttribute>())
            {
                if (type == null || type.IsAbstract || type.IsInterface)
                    continue;
                if (!typeof(IConfigFormat).IsAssignableFrom(type))
                    continue;
                if (type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                try
                {
                    var attr = type.GetCustomAttribute<ConfigFormatPluginAttribute>();
                    var instance = Activator.CreateInstance(type) as IConfigFormat;
                    var formatId =attr.FormatId;

                    if (string.IsNullOrEmpty(formatId))
                        continue;

                    Plugins[formatId] = instance;
                }
                catch
                {
                    // 忽略无法实例化的插件
                }
            }
        }
    }
}
