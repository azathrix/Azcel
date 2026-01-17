using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Azcel
{
    /// <summary>
    /// 配置格式定义（序列化/反序列化成对）
    /// </summary>
    public interface IConfigFormat
    {
        string FormatId { get; }
        string Extension { get; }
        IConfigTableLoader CreateTableLoader();
        IConfigParser CreateGlobalParser();
    }

    public static class ConfigFormatIds
    {
        public const string Binary = "binary";
        public const string Json = "json";
    }

    public static class ConfigFormatRegistry
    {
        private static readonly Dictionary<string, IConfigFormat> Formats = new(StringComparer.OrdinalIgnoreCase);
        private static bool _scanned;
        private static int _version;
        private static int _cachedVersion = -1;
        private static string[] _cachedIds;

        static ConfigFormatRegistry()
        {
            Register(new BinaryConfigFormat());
        }

        public static void Register(IConfigFormat format)
        {
            if (format == null || string.IsNullOrEmpty(format.FormatId))
                return;

            var exists = Formats.ContainsKey(format.FormatId);
            Formats[format.FormatId] = format;
            if (!exists)
            {
                _version++;
                _cachedVersion = -1;
            }
        }

        public static IConfigFormat Get(string formatId)
        {
            EnsureScanned();
            if (string.IsNullOrEmpty(formatId))
                return null;
            return Formats.TryGetValue(formatId, out var format) ? format : null;
        }

        public static IReadOnlyList<string> GetFormatIds()
        {
            return GetFormatIdArray();
        }

        public static string[] GetFormatIdArray()
        {
            EnsureScanned();
            if (Formats.Count == 0)
                return Array.Empty<string>();

            if (_cachedVersion == _version && _cachedIds != null)
                return _cachedIds;

            _cachedIds = Formats.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            _cachedVersion = _version;
            return _cachedIds;
        }

        private static void EnsureScanned()
        {
            if (_scanned)
                return;

            _scanned = true;
            foreach (var type in GetAllFormatTypes())
            {
                if (type == null || type.IsAbstract || type.IsInterface)
                    continue;
                if (!typeof(IConfigFormat).IsAssignableFrom(type))
                    continue;
                if (type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                try
                {
                    var instance = Activator.CreateInstance(type) as IConfigFormat;
                    if (instance != null)
                        Register(instance);
                }
                catch
                {
                    // 忽略无法实例化的格式
                }
            }
        }

        private static IEnumerable<Type> GetAllFormatTypes()
        {
#if UNITY_EDITOR
            foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigFormat>())
                yield return type;
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
                    if (type != null)
                        yield return type;
                }
            }
#endif
        }
    }

    internal sealed class BinaryConfigFormat : IConfigFormat
    {
        public string FormatId => ConfigFormatIds.Binary;
        public string Extension => ".bytes";

        public IConfigTableLoader CreateTableLoader() => BinaryConfigTableLoader.Instance;
        public IConfigParser CreateGlobalParser() => DefaultConfigParser.Instance;
    }
}
