using System;
using System.Collections.Generic;
using Azathrix.Framework.Interfaces;
using Azathrix.Framework.Interfaces.SystemEvents;
using Azathrix.Framework.Interfaces.SystemEvents;
using Azathrix.Framework.Tools;

namespace Azcel
{
    /// <summary>
    /// Azcel系统 - 统一管理API（通过 AzathrixFramework.GetSystem<AzcelSystem>() 获取）
    /// </summary>
    public class AzcelSystem : ISystem, ISystemEditorSupport, ISystemRegister
    {
        private readonly Dictionary<Type, IConfigTable> _tables = new();
        private readonly Dictionary<Type, IConfigTable> _tablesByType = new();
        private IDataLoader _dataLoader;
        private IConfigTableLoader _tableLoader;
        private static Dictionary<string, Type> _typeCache;
        private static bool _typeCacheReady;
        private bool _useQueryCache = true;

        /// <summary>当前数据加载器</summary>
        public IDataLoader DataLoader => _dataLoader;

        /// <summary>表配置加载器</summary>
        public IConfigTableLoader TableLoader => _tableLoader;

        /// <summary>
        /// 是否启用查询缓存（GetAllConfig/GetByIndex 等）。
        /// 默认开启；关闭时会清空缓存并走非缓存路径。
        /// </summary>
        public bool UseQueryCache
        {
            get => _useQueryCache;
            set
            {
                _useQueryCache = value;
                if (!_useQueryCache)
                    ClearAllTableCaches();
            }
        }


        /// <summary>设置数据加载器</summary>
        public void SetDataLoader(IDataLoader loader) => _dataLoader = loader;

        /// <summary>设置表加载器</summary>
        public void SetTableLoader(IConfigTableLoader loader) => _tableLoader = loader;

        public void OnEditorInitialize()
        {
        }

        public void OnRegister()
        {
            var settings = AzcelSettings.Instance;
            if (settings != null)
                UseQueryCache = settings.useQueryCache;
        }

        public void OnUnRegister()
        {
        }
        //
        // /// <summary>
        // /// 注册配置类型（自动推断主键类型）
        // /// </summary>
        // public void RegisterConfig<TConfig>() where TConfig : ConfigBase, new()
        // {
        //     var type = typeof(TConfig);
        //     if (_tables.ContainsKey(type))
        //         return;
        //
        //     var table = CreateTable(type);
        //     _tables[type] = table;
        //     _tablesByType[table.GetType()] = table;
        // }
        //
        // /// <summary>
        // /// 注册配置类型（显式主键类型）
        // /// </summary>
        // public void RegisterConfig<TConfig, TKey>()
        //     where TConfig : ConfigBase<TKey>, new()
        // {
        //     var table = new ConfigTable<TConfig, TKey>();
        //     _tables[typeof(TConfig)] = table;
        //     _tablesByType[table.GetType()] = table;
        // }
        //
        // public void RegisterConfig(Type configType)
        // {
        //     if (configType == null || !typeof(ConfigBase).IsAssignableFrom(configType))
        //         return;
        //
        //     if (_tables.ContainsKey(configType))
        //         return;
        //
        //     var table = CreateTable(configType);
        //     _tables[configType] = table;
        //     _tablesByType[table.GetType()] = table;
        // }

        public void RegisterTable(IConfigTable table)
        {
            if (table == null || table.ConfigType == null)
                return;

            _tables[table.ConfigType] = table;
            _tablesByType[table.GetType()] = table;
        }

        /// <summary>通过主键获取配置行（object 版本）</summary>
        public TConfig GetConfig<TConfig>(object key) where TConfig : ConfigBase
        {
            if (_tables.TryGetValue(typeof(TConfig), out var table) && table.TryGet(key, out var config))
                return (TConfig)config;
            return null;
        }

        /// <summary>通过主键获取配置行（object 版本）</summary>
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

        /// <summary>
        /// 获取全部配置（可能分配；内部优先走表缓存）。
        /// </summary>
        public IReadOnlyList<TConfig> GetAllConfig<TConfig>() where TConfig : ConfigBase
        {
            if (!_tables.TryGetValue(typeof(TConfig), out var table))
                return Array.Empty<TConfig>();

            var cached = UseQueryCache ? table.GetAllCached() : table.GetAll();
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

        /// <summary>
        /// 按索引获取配置（可能分配；内部优先走表缓存）。
        /// </summary>
        public IReadOnlyList<TConfig> GetByIndex<TConfig>(string indexName, object value) where TConfig : ConfigBase
        {
            if (!_tables.TryGetValue(typeof(TConfig), out var table))
                return Array.Empty<TConfig>();

            var cached = UseQueryCache ? table.GetByIndexCached(indexName, value) : table.GetByIndex(indexName, value);
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

        /// <summary>获取全部表实例</summary>
        public IEnumerable<IConfigTable> GetAllTables() => _tables.Values;

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

        /// <summary>通过主键获取配置行（强类型）</summary>
        public TConfig GetConfig<TConfig, TKey>(TKey key)
            where TConfig : ConfigBase<TKey>, new()
        {
            var table = GetTable<TConfig, TKey>();
            return table?.GetById(key);
        }

        /// <summary>通过主键获取配置行（强类型）</summary>
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

        /// <summary>加载单张表数据</summary>
        public void LoadTable(IConfigTable table, byte[] data)
        {
            if (table == null)
                return;

            _tableLoader?.Load(table, data);
        }

        /// <summary>
        /// 清除配置
        /// </summary>
        public void Clear()
        {
            _tables.Clear();
            _tablesByType.Clear();
            _dataLoader = null;
            _tableLoader = null;
        }

        private void ClearAllTableCaches()
        {
            foreach (var table in _tables.Values)
                table?.ClearCache();
        }

        /// <summary>
        /// 使用当前 Loader/Parser 加载所有数据（仅用于测试）
        /// </summary>
        internal void LoadAllDataInternal()
        {
            var loader = _dataLoader ?? new ResourcesDataLoader();

            foreach (var table in GetAllTables())
            {
                var data = loader.Load(table.ConfigName);
                if (data != null)
                    LoadTable(table, data);
            }
        }

        /// <summary>
        /// 加载注册表
        /// </summary>
        public void LoadTableRegistry()
        {
            var typeName = GetTableRegistryTypeName();
            if (string.IsNullOrEmpty(typeName))
                return;

            var type = FindType(typeName);
            if (type == null)
                return;

            var method = type.GetMethod("Apply", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null)
                return;

            try
            {
                method.Invoke(null, new object[] { this });
            }
            catch (Exception e)
            {
                Log.Warning($"[Azcel] 运行时引导执行失败: {e.Message}");
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

        private string GetTableRegistryTypeName()
        {
            var ns = AzcelSettings.Instance?.codeNamespace;
            if (string.IsNullOrEmpty(ns))
                ns = "Game.Tables";
            return $"{ns}.TableRegistry";
        }
    }
}
