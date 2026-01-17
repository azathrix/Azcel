using System;
using System.Collections.Generic;
using Azathrix.Framework.Interfaces;
using Azathrix.Framework.Tools;

namespace Azcel
{
    /// <summary>
    /// Azcel系统 - 统一管理API（通过 AzathrixFramework.GetSystem<AzcelSystem>() 获取）
    /// </summary>
    public class AzcelSystem : ISystem
    {
        private readonly Dictionary<Type, IConfigTable> _tables = new();
        private readonly Dictionary<Type, IConfigTable> _tablesByType = new();
        private readonly Dictionary<Type, GlobalConfig> _globals = new();
        private IDataLoader _dataLoader;
        private IConfigParser _globalParser;
        private IConfigTableLoader _tableLoader;
        private AzcelManifest _manifest;
        private static Dictionary<string, Type> _typeCache;
        private static bool _typeCacheReady;

        /// <summary>
        /// 当前数据加载器
        /// </summary>
        public IDataLoader DataLoader => _dataLoader;

        /// <summary>
        /// 全局配置解析器
        /// </summary>
        public IConfigParser Parser => _globalParser;

        /// <summary>
        /// 表配置加载器
        /// </summary>
        public IConfigTableLoader TableLoader => _tableLoader;

        public AzcelManifest Manifest => _manifest;

        public void SetDataLoader(IDataLoader loader) => _dataLoader = loader;

        public void SetParser(IConfigParser parser) => _globalParser = parser;

        public void SetTableLoader(IConfigTableLoader loader) => _tableLoader = loader;

        public void SetFormat(IConfigFormat format, bool force = false)
        {
            if (format == null)
                return;

            if (force || _tableLoader == null)
                _tableLoader = format.CreateTableLoader();
            if (force || _globalParser == null)
                _globalParser = format.CreateGlobalParser();
        }

        /// <summary>
        /// 注册配置类型（自动推断主键类型）
        /// </summary>
        internal void RegisterConfig<TConfig>() where TConfig : ConfigBase, new()
        {
            var type = typeof(TConfig);
            if (_tables.ContainsKey(type))
                return;

            var table = CreateTable(type);
            _tables[type] = table;
            _tablesByType[table.GetType()] = table;
        }

        /// <summary>
        /// 注册配置类型（显式主键类型）
        /// </summary>
        internal void RegisterConfig<TConfig, TKey>()
            where TConfig : ConfigBase<TKey>, new()
        {
            var table = new ConfigTable<TConfig, TKey>();
            _tables[typeof(TConfig)] = table;
            _tablesByType[table.GetType()] = table;
        }

        internal void RegisterConfig(Type configType)
        {
            if (configType == null || !typeof(ConfigBase).IsAssignableFrom(configType))
                return;

            if (_tables.ContainsKey(configType))
                return;

            var table = CreateTable(configType);
            _tables[configType] = table;
            _tablesByType[table.GetType()] = table;
        }

        internal void RegisterTable(IConfigTable table)
        {
            if (table == null || table.ConfigType == null)
                return;

            _tables[table.ConfigType] = table;
            _tablesByType[table.GetType()] = table;
        }

        /// <summary>
        /// 注册全局配置
        /// </summary>
        internal void RegisterGlobal<T>(T config) where T : GlobalConfig
        {
            _globals[typeof(T)] = config;
        }

        internal void RegisterGlobal(Type globalType)
        {
            if (globalType == null || !typeof(GlobalConfig).IsAssignableFrom(globalType))
                return;

            if (_globals.ContainsKey(globalType))
                return;

            var instance = Activator.CreateInstance(globalType) as GlobalConfig;
            if (instance == null)
                return;

            _globals[globalType] = instance;
        }

        /// <summary>
        /// 通过主键获取配置行
        /// </summary>
        public TConfig GetConfig<TConfig>(object key) where TConfig : ConfigBase
        {
            if (_tables.TryGetValue(typeof(TConfig), out var table) && table.TryGet(key, out var config))
                return (TConfig)config;
            return null;
        }

        public bool TryGetConfig<TConfig>(object key, out TConfig config) where TConfig : ConfigBase
        {
            config = null;
            if (_tables.TryGetValue(typeof(TConfig), out var table) && table.TryGet(key, out var result))
            {
                config = (TConfig)result;
                return true;
            }
            return false;
        }

        public IReadOnlyList<TConfig> GetAllConfig<TConfig>() where TConfig : ConfigBase
        {
            if (!_tables.TryGetValue(typeof(TConfig), out var table))
                return Array.Empty<TConfig>();

            var cached = table.GetAllCached();
            if (cached is IReadOnlyList<TConfig> cachedList)
                return cachedList;

            var list = table.GetAll();
            if (list.Count == 0)
                return Array.Empty<TConfig>();

            var result = new TConfig[list.Count];
            for (int i = 0; i < list.Count; i++)
                result[i] = (TConfig)list[i];
            return result;
        }

        /// <summary>
        /// 获取全部配置（无额外分配，推荐用于性能敏感场景）
        /// </summary>
        public IReadOnlyList<TConfig> GetAllConfig<TConfig, TKey>()
            where TConfig : ConfigBase<TKey>, new()
        {
            var table = GetTable<TConfig, TKey>();
            return table?.GetAllConfigs() ?? Array.Empty<TConfig>();
        }

        public IReadOnlyList<TConfig> GetByIndex<TConfig>(string indexName, object value) where TConfig : ConfigBase
        {
            if (!_tables.TryGetValue(typeof(TConfig), out var table))
                return Array.Empty<TConfig>();

            var cached = table.GetByIndexCached(indexName, value);
            if (cached is IReadOnlyList<TConfig> cachedList)
                return cachedList;

            var list = table.GetByIndex(indexName, value);
            if (list.Count == 0)
                return Array.Empty<TConfig>();

            var result = new TConfig[list.Count];
            for (int i = 0; i < list.Count; i++)
                result[i] = (TConfig)list[i];
            return result;
        }

        /// <summary>
        /// 按索引获取配置（无额外分配，推荐用于性能敏感场景）
        /// </summary>
        public IReadOnlyList<TConfig> GetByIndex<TConfig, TKey>(string indexName, object value)
            where TConfig : ConfigBase<TKey>, new()
        {
            var table = GetTable<TConfig, TKey>();
            return table?.GetByIndexConfigs(indexName, value) ?? Array.Empty<TConfig>();
        }

        public T GetGlobal<T>() where T : GlobalConfig
        {
            return _globals.TryGetValue(typeof(T), out var config) ? (T)config : null;
        }

        /// <summary>获取全部表实例</summary>
        public IEnumerable<IConfigTable> GetAllTables() => _tables.Values;

        /// <summary>获取全部全局配置实例</summary>
        public IEnumerable<GlobalConfig> GetAllGlobals() => _globals.Values;

        /// <summary>
        /// 获取表实例（按表类型）。
        /// 示例：var table = azcel.GetTable<MyConfigTable>();
        /// </summary>
        public TTable GetTable<TTable>() where TTable : class, IConfigTable
        {
            return _tablesByType.TryGetValue(typeof(TTable), out var table) ? table as TTable : null;
        }

        /// <summary>
        /// 获取表实例（按配置类型）。
        /// 示例：var table = azcel.GetTable<MyConfig, int>();
        /// </summary>
        public IConfigTable<TConfig, TKey> GetTable<TConfig, TKey>()
            where TConfig : ConfigBase<TKey>, new()
        {
            return _tables.TryGetValue(typeof(TConfig), out var table)
                ? table as IConfigTable<TConfig, TKey>
                : null;
        }

        public TConfig GetConfig<TConfig, TKey>(TKey key)
            where TConfig : ConfigBase<TKey>, new()
        {
            var table = GetTable<TConfig, TKey>();
            return table?.GetById(key);
        }

        public bool TryGetConfig<TConfig, TKey>(TKey key, out TConfig config)
            where TConfig : ConfigBase<TKey>, new()
        {
            var table = GetTable<TConfig, TKey>();
            if (table == null)
            {
                config = null;
                return false;
            }

            return table.TryGet(key, out config);
        }

        public void LoadTable(IConfigTable table, byte[] data)
        {
            if (table == null)
                return;

            var loader = _tableLoader ?? BinaryConfigTableLoader.Instance;
            loader.Load(table, data);
        }

        /// <summary>
        /// 重置系统状态（仅用于测试）
        /// </summary>
        internal void ResetForTests()
        {
            _tables.Clear();
            _tablesByType.Clear();
            _globals.Clear();
            _manifest = null;
            _dataLoader = null;
            _globalParser = null;
            _tableLoader = null;
        }

        /// <summary>
        /// 使用当前 Loader/Parser 加载所有数据（仅用于测试）
        /// </summary>
        internal void LoadAllDataInternal()
        {
            var loader = _dataLoader ?? new ResourcesDataLoader();
            var parser = _globalParser ?? DefaultConfigParser.Instance;

            foreach (var table in GetAllTables())
            {
                var data = loader.Load(table.ConfigName);
                if (data != null)
                    LoadTable(table, data);
            }

            foreach (var global in GetAllGlobals())
            {
                var data = loader.Load(global.ConfigName);
                if (data != null)
                    parser.Parse(global, data);
            }
        }

        public bool TryLoadManifest()
        {
            if (_manifest != null)
                return true;

            var loader = _dataLoader ?? new ResourcesDataLoader();
            var data = loader.Load(AzcelManifest.ManifestName);
            if (data == null || data.Length == 0)
                return false;

            _manifest = AzcelManifest.Deserialize(data);
            if (_manifest == null)
                return false;

            return true;
        }

        public void ApplyFormatFromManifest(bool force = false)
        {
            if (_manifest == null || string.IsNullOrEmpty(_manifest.FormatId))
                return;

            var format = ConfigFormatRegistry.Get(_manifest.FormatId);
            if (format == null)
            {
                Log.Warning($"[Azcel] 未找到格式: {_manifest.FormatId}，将使用默认格式");
                return;
            }

            SetFormat(format, force);
        }

        public void AutoRegisterFromManifest()
        {
            if (_manifest == null)
                return;

            foreach (var entry in _manifest.Entries)
            {
                if (string.IsNullOrEmpty(entry.TypeName))
                    continue;

                var type = FindType(entry.TypeName);
                if (type == null)
                {
                    Log.Warning($"[Azcel] 未找到配置类型: {entry.TypeName}");
                    continue;
                }

                if (entry.EntryType == AzcelManifestEntryType.Global)
                {
                    RegisterGlobal(type);
                    continue;
                }

                if (typeof(IConfigTable).IsAssignableFrom(type))
                {
                    var tableInstance = Activator.CreateInstance(type) as IConfigTable;
                    if (tableInstance == null)
                    {
                        Log.Warning($"[Azcel] 表类型实例化失败: {entry.TypeName}");
                        continue;
                    }
                    RegisterTable(tableInstance);
                    continue;
                }

                RegisterConfig(type);
            }
        }

        private static IConfigTable CreateTable(Type configType)
        {
            var baseType = configType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(ConfigBase<>))
                    break;
                baseType = baseType.BaseType;
            }

            if (baseType == null)
                throw new InvalidOperationException($"Config type {configType.Name} does not inherit ConfigBase<TKey>.");

            var keyType = baseType.GetGenericArguments()[0];
            var tableType = typeof(ConfigTable<,>).MakeGenericType(configType, keyType);
            return (IConfigTable)Activator.CreateInstance(tableType);
        }

        private static Type FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            var direct = Type.GetType(typeName);
            if (direct != null)
                return direct;

            EnsureTypeCache();
            if (_typeCache != null && _typeCache.TryGetValue(typeName, out var cached))
                return cached;

            return null;
        }

        private static void EnsureTypeCache()
        {
            if (_typeCacheReady)
                return;

            _typeCacheReady = true;
            var cache = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var assembly in AzcelTypeScanner.GetAssemblies())
            {
                foreach (var type in AzcelTypeScanner.GetTypes(assembly))
                {
                    if (type == null || string.IsNullOrEmpty(type.FullName))
                        continue;

                    if (!cache.ContainsKey(type.FullName))
                        cache[type.FullName] = type;
                }
            }

            _typeCache = cache;
        }
    }
}
