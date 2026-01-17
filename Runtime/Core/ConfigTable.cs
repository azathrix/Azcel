using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Azathrix.Framework.Tools;

namespace Azcel
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ConfigIndexAttribute : Attribute
    {
        public string Name { get; }
        public ConfigIndexAttribute(string name = null)
        {
            Name = name;
        }
    }

    public interface IConfigTable
    {
        /// <summary>配置类型</summary>
        Type ConfigType { get; }
        /// <summary>主键类型</summary>
        Type KeyType { get; }
        /// <summary>配置名（用于资源名/表名）</summary>
        string ConfigName { get; }
        /// <summary>当前表行数</summary>
        int Count { get; }
        /// <summary>
        /// 查询缓存容量（条数）。0 表示禁用缓存。
        /// 建议按查询热点数量设置，例如 64/128。
        /// </summary>
        int CacheCapacity { get; set; }

        /// <summary>清空表数据（会同时清空缓存）</summary>
        void Clear();
        /// <summary>仅清空查询缓存</summary>
        void ClearCache();
        /// <summary>创建配置实例（反序列化用）</summary>
        object CreateInstance();
        /// <summary>反序列化一条配置</summary>
        void Deserialize(object config, BinaryReader reader);
        /// <summary>添加一条配置</summary>
        void Add(object config);
        /// <summary>按主键查询（object 版本）</summary>
        bool TryGet(object key, out object config);
        /// <summary>获取全部配置（会分配 object[]）</summary>
        IReadOnlyList<object> GetAll();
        /// <summary>按索引查询（会分配 object[]）</summary>
        IReadOnlyList<object> GetByIndex(string indexName, object value);
        /// <summary>获取全部配置（缓存版本，用于降低 GC）</summary>
        IReadOnlyList<object> GetAllCached();
        /// <summary>按索引查询（缓存版本，用于降低 GC）</summary>
        IReadOnlyList<object> GetByIndexCached(string indexName, object value);
        /// <summary>重建索引（会清空缓存）</summary>
        void BuildIndexes();
    }

    public interface IConfigTable<TConfig, TKey> : IConfigTable
        where TConfig : ConfigBase<TKey>, new()
    {
        /// <summary>按主键查询</summary>
        TConfig GetById(TKey key);
        /// <summary>按主键查询</summary>
        bool TryGet(TKey key, out TConfig config);
        /// <summary>获取全部配置（不分配）</summary>
        IReadOnlyList<TConfig> GetAllConfigs();
        /// <summary>按索引查询（不分配）</summary>
        IReadOnlyList<TConfig> GetByIndexConfigs(string indexName, object value);
        /// <summary>获取全部配置（缓存版本，用于降低 GC）</summary>
        IReadOnlyList<TConfig> GetAllConfigsCached();
        /// <summary>按索引查询（缓存版本，用于降低 GC）</summary>
        IReadOnlyList<TConfig> GetByIndexConfigsCached(string indexName, object value);
    }

    public interface IConfigTableLoader
    {
        void Load(IConfigTable table, byte[] data);
    }

    public sealed class BinaryConfigTableLoader : IConfigTableLoader
    {
        public static readonly BinaryConfigTableLoader Instance = new();

        public void Load(IConfigTable table, byte[] data)
        {
            table.Clear();

            if (data == null || data.Length == 0)
            {
                table.BuildIndexes();
                return;
            }

            using var reader = new BinaryReader(new MemoryStream(data));
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var config = table.CreateInstance();
                table.Deserialize(config, reader);
                table.Add(config);
            }

            table.BuildIndexes();
        }
    }

    /// <summary>
    /// 配置表实现。
    /// 常用用法：
    /// - 获取表：azcel.GetTable<MyConfig, int>()
    /// - 按主键：table.GetById(id)
    /// - 按索引：table.GetByIndexConfigs("Name", "Sword")
    /// - 缓存查询：table.CacheCapacity = 128; table.GetByIndexConfigsCached(...)
    /// </summary>
    public class ConfigTable<TConfig, TKey> : IConfigTable, IConfigTable<TConfig, TKey>
        where TConfig : ConfigBase<TKey>, new()
    {
        private readonly Dictionary<TKey, TConfig> _configs = new();
        private readonly List<TConfig> _all = new();
        private readonly Dictionary<string, IIndexStore> _indexStores = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<IndexAccessor> _indexAccessors;
        private readonly string _configName;
        private readonly Dictionary<QueryCacheKey, CacheEntry> _queryCache = new();
        private int _cacheCapacity = 64;

        public ConfigTable()
        {
            var sample = new TConfig();
            _configName = sample.ConfigName;
            _indexAccessors = BuildIndexAccessors();
        }

        /// <summary>配置类型</summary>
        public Type ConfigType => typeof(TConfig);
        /// <summary>主键类型</summary>
        public Type KeyType => typeof(TKey);
        /// <summary>配置名（用于资源名/表名）</summary>
        public string ConfigName => _configName;
        /// <summary>当前表行数</summary>
        public int Count => _all.Count;

        /// <summary>
        /// 查询缓存容量（条数）。0 表示禁用缓存。
        /// 适合对 GetAll/ByIndex 这类数组查询做缓存，降低 GC。
        /// </summary>
        public int CacheCapacity
        {
            get => _cacheCapacity;
            set
            {
                _cacheCapacity = value < 0 ? 0 : value;
                if (_cacheCapacity == 0)
                    ClearCache();
            }
        }

        /// <summary>清空表数据（会同时清空缓存）</summary>
        public void Clear()
        {
            _configs.Clear();
            _all.Clear();
            _indexStores.Clear();
            ClearCache();
        }

        /// <summary>仅清空查询缓存</summary>
        public void ClearCache()
        {
            if (_queryCache.Count > 0)
                _queryCache.Clear();
        }

        /// <summary>创建配置实例（反序列化用）</summary>
        public object CreateInstance() => new TConfig();

        /// <summary>反序列化一条配置</summary>
        public void Deserialize(object config, BinaryReader reader)
        {
            ((TConfig)config).Deserialize(reader);
        }

        /// <summary>添加一条配置</summary>
        public void Add(object config)
        {
            var typed = (TConfig)config;
            if (_configs.ContainsKey(typed.Id))
            {
                Log.Warning($"[Azcel] Duplicate key in {typeof(TConfig).Name}: {typed.Id}");
                return;
            }

            _configs.Add(typed.Id, typed);
            _all.Add(typed);
            ClearCache();
        }

        /// <summary>按主键查询（object 版本）</summary>
        public bool TryGet(object key, out object config)
        {
            config = null;
            if (!TryConvertKey(key, out TKey converted))
                return false;

            if (_configs.TryGetValue(converted, out var result))
            {
                config = result;
                return true;
            }

            return false;
        }

        /// <summary>获取全部配置（会分配 object[]）</summary>
        public IReadOnlyList<object> GetAll()
        {
            if (_all.Count == 0)
                return Array.Empty<object>();

            var result = new object[_all.Count];
            for (int i = 0; i < _all.Count; i++)
                result[i] = _all[i];
            return result;
        }

        /// <summary>按索引查询（会分配 object[]）</summary>
        public IReadOnlyList<object> GetByIndex(string indexName, object value)
        {
            if (string.IsNullOrEmpty(indexName))
                return Array.Empty<object>();

            if (!_indexStores.TryGetValue(indexName, out var store))
                return Array.Empty<object>();

            if (!store.TryGet(value, out var list))
                return Array.Empty<object>();

            return ToObjectArray(list);
        }

        /// <summary>按主键查询</summary>
        public TConfig GetById(TKey key)
        {
            return _configs.TryGetValue(key, out var config) ? config : null;
        }

        /// <summary>按主键查询</summary>
        public bool TryGet(TKey key, out TConfig config)
        {
            return _configs.TryGetValue(key, out config);
        }

        /// <summary>
        /// 获取全部配置（不分配），推荐用于性能敏感场景。
        /// </summary>
        /// <summary>获取全部配置（不分配），推荐用于性能敏感场景</summary>
        public IReadOnlyList<TConfig> GetAllConfigs()
        {
            if (_all.Count == 0)
                return Array.Empty<TConfig>();

            return _all;
        }

        /// <summary>
        /// 按索引获取配置（不分配），推荐用于性能敏感场景。
        /// </summary>
        /// <summary>按索引获取配置（不分配），推荐用于性能敏感场景</summary>
        public IReadOnlyList<TConfig> GetByIndexConfigs(string indexName, object value)
        {
            if (string.IsNullOrEmpty(indexName))
                return Array.Empty<TConfig>();

            if (!_indexStores.TryGetValue(indexName, out var store))
                return Array.Empty<TConfig>();

            if (!store.TryGet(value, out var list))
                return Array.Empty<TConfig>();

            return list;
        }

        /// <summary>
        /// 获取全部配置（缓存版本）。
        /// 示例：table.CacheCapacity = 128; var list = table.GetAllConfigsCached();
        /// </summary>
        /// <summary>获取全部配置（缓存版本，用于降低 GC）</summary>
        public IReadOnlyList<TConfig> GetAllConfigsCached()
        {
            if (_all.Count == 0)
                return Array.Empty<TConfig>();

            if (_cacheCapacity <= 0)
                return _all;

            return GetCached(new QueryCacheKey(isAll: true, indexName: null, value: null), () => CopyToArray(_all));
        }

        /// <summary>
        /// 按索引获取配置（缓存版本）。
        /// 示例：table.GetByIndexConfigsCached("Name", "Sword");
        /// </summary>
        /// <summary>按索引获取配置（缓存版本，用于降低 GC）</summary>
        public IReadOnlyList<TConfig> GetByIndexConfigsCached(string indexName, object value)
        {
            if (string.IsNullOrEmpty(indexName))
                return Array.Empty<TConfig>();

            if (_cacheCapacity <= 0)
                return GetByIndexConfigs(indexName, value);

            return GetCached(new QueryCacheKey(isAll: false, indexName: indexName, value: value), () =>
            {
                if (!_indexStores.TryGetValue(indexName, out var store))
                    return Array.Empty<TConfig>();

                if (!store.TryGet(value, out var list) || list.Count == 0)
                    return Array.Empty<TConfig>();

                return CopyToArray(list);
            });
        }

        IReadOnlyList<object> IConfigTable.GetAllCached()
        {
            return GetAllConfigsCached();
        }

        IReadOnlyList<object> IConfigTable.GetByIndexCached(string indexName, object value)
        {
            return GetByIndexConfigsCached(indexName, value);
        }

        /// <summary>重建索引（会清空缓存）</summary>
        public void BuildIndexes()
        {
            _indexStores.Clear();
            ClearCache();
            if (_indexAccessors.Count == 0)
                return;

            foreach (var accessor in _indexAccessors)
            {
                var store = CreateIndexStore(accessor);
                store.Build(_all);
                _indexStores[accessor.Name] = store;
            }
        }

        private static bool TryConvertKey(object key, out TKey result)
        {
            if (TryConvertKey(key, typeof(TKey), out var converted))
            {
                result = (TKey)converted;
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryConvertKey(object key, Type targetType, out object result)
        {
            if (key == null)
            {
                result = null;
                return false;
            }

            if (targetType.IsInstanceOfType(key))
            {
                result = key;
                return true;
            }

            try
            {
                if (targetType.IsEnum)
                {
                    if (key is string s)
                    {
                        result = Enum.Parse(targetType, s, true);
                        return true;
                    }

                    var underlying = Convert.ChangeType(key, Enum.GetUnderlyingType(targetType));
                    result = Enum.ToObject(targetType, underlying);
                    return true;
                }

                if (targetType == typeof(Guid))
                {
                    if (key is Guid guid)
                    {
                        result = guid;
                        return true;
                    }

                    if (key is string guidStr && Guid.TryParse(guidStr, out var parsedGuid))
                    {
                        result = parsedGuid;
                        return true;
                    }
                }

                result = Convert.ChangeType(key, targetType);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        private static List<IndexAccessor> BuildIndexAccessors()
        {
            var result = new List<IndexAccessor>();
            var members = typeof(TConfig).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var member in members)
            {
                if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                    continue;

                var attr = member.GetCustomAttribute<ConfigIndexAttribute>();
                if (attr == null)
                    continue;

                if (member is PropertyInfo prop && prop.GetMethod == null)
                    continue;

                var name = string.IsNullOrEmpty(attr.Name) ? member.Name : attr.Name;
                var valueType = member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;
                result.Add(new IndexAccessor(name, member, valueType));
            }

            return result;
        }

        private static IIndexStore CreateIndexStore(IndexAccessor accessor)
        {
            if (!(accessor.ValueType == typeof(string) || accessor.ValueType.IsValueType))
                return new ObjectIndexStore(accessor);

            var storeType = typeof(IndexStore<>);
            var argCount = storeType.GetGenericArguments().Length;
            try
            {
                Type constructed;
                if (argCount == 1)
                {
                    constructed = storeType.MakeGenericType(accessor.ValueType);
                }
                else if (argCount == 3)
                {
                    constructed = storeType.MakeGenericType(typeof(TConfig), typeof(TKey), accessor.ValueType);
                }
                else
                {
                    return new ObjectIndexStore(accessor);
                }

                return (IIndexStore)Activator.CreateInstance(constructed, accessor);
            }
            catch
            {
                return new ObjectIndexStore(accessor);
            }
        }

        private IReadOnlyList<TConfig> GetCached(QueryCacheKey key, Func<IReadOnlyList<TConfig>> factory)
        {
            if (_queryCache.TryGetValue(key, out var entry))
            {
                entry.UseCount++;
                return entry.Result;
            }

            var result = factory();
            if (result == null)
                result = Array.Empty<TConfig>();

            _queryCache[key] = new CacheEntry(result);
            TrimCacheIfNeeded();
            return result;
        }

        private void TrimCacheIfNeeded()
        {
            if (_cacheCapacity <= 0 || _queryCache.Count <= _cacheCapacity)
                return;

            var entries = new CacheEntryInfo[_queryCache.Count];
            var i = 0;
            foreach (var kvp in _queryCache)
            {
                entries[i++] = new CacheEntryInfo(kvp.Key, kvp.Value.UseCount);
            }

            Array.Sort(entries, CacheEntryInfoComparer.Instance);
            var removeCount = entries.Length / 2;
            for (i = 0; i < removeCount; i++)
                _queryCache.Remove(entries[i].Key);
        }

        private static TConfig[] CopyToArray(IReadOnlyList<TConfig> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<TConfig>();

            var result = new TConfig[list.Count];
            for (int i = 0; i < list.Count; i++)
                result[i] = list[i];
            return result;
        }

        private static IReadOnlyList<object> ToObjectArray(IReadOnlyList<TConfig> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<object>();

            var result = new object[list.Count];
            for (int i = 0; i < list.Count; i++)
                result[i] = list[i];
            return result;
        }

        private readonly struct QueryCacheKey : IEquatable<QueryCacheKey>
        {
            public readonly bool IsAll;
            public readonly string IndexName;
            public readonly object Value;

            public QueryCacheKey(bool isAll, string indexName, object value)
            {
                IsAll = isAll;
                IndexName = indexName;
                Value = value;
            }

            public bool Equals(QueryCacheKey other)
            {
                if (IsAll != other.IsAll)
                    return false;
                if (!string.Equals(IndexName, other.IndexName, StringComparison.Ordinal))
                    return false;
                return Equals(Value, other.Value);
            }

            public override bool Equals(object obj)
            {
                return obj is QueryCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = IsAll ? 1 : 0;
                    hash = (hash * 397) ^ (IndexName != null ? StringComparer.Ordinal.GetHashCode(IndexName) : 0);
                    hash = (hash * 397) ^ (Value != null ? Value.GetHashCode() : 0);
                    return hash;
                }
            }
        }

        private sealed class CacheEntry
        {
            public IReadOnlyList<TConfig> Result { get; }
            public int UseCount { get; set; }

            public CacheEntry(IReadOnlyList<TConfig> result)
            {
                Result = result;
                UseCount = 1;
            }
        }

        private readonly struct CacheEntryInfo
        {
            public readonly QueryCacheKey Key;
            public readonly int UseCount;

            public CacheEntryInfo(QueryCacheKey key, int useCount)
            {
                Key = key;
                UseCount = useCount;
            }
        }

        private sealed class CacheEntryInfoComparer : IComparer<CacheEntryInfo>
        {
            public static readonly CacheEntryInfoComparer Instance = new();

            public int Compare(CacheEntryInfo x, CacheEntryInfo y)
            {
                return x.UseCount.CompareTo(y.UseCount);
            }
        }

        private readonly struct IndexAccessor
        {
            public string Name { get; }
            public Type ValueType { get; }
            private readonly MemberInfo _member;

            public IndexAccessor(string name, MemberInfo member, Type valueType)
            {
                Name = name;
                _member = member;
                ValueType = valueType;
            }

            public object GetValue(TConfig config)
            {
                if (_member is FieldInfo field)
                    return field.GetValue(config);
                if (_member is PropertyInfo prop)
                    return prop.GetValue(config);
                return null;
            }
        }

        private interface IIndexStore
        {
            string Name { get; }
            Type ValueType { get; }
            void Build(List<TConfig> items);
            bool TryGet(object value, out IReadOnlyList<TConfig> list);
        }

        private sealed class IndexStore<TValue> : IIndexStore
        {
            private readonly IndexAccessor _accessor;
            private readonly Dictionary<TValue, List<TConfig>> _map = new();

            public IndexStore(IndexAccessor accessor)
            {
                _accessor = accessor;
            }

            public string Name => _accessor.Name;
            public Type ValueType => typeof(TValue);

            public void Build(List<TConfig> items)
            {
                _map.Clear();
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var keyObj = _accessor.GetValue(item);
                    if (keyObj == null)
                        continue;

                    if (!TryConvertKey(keyObj, typeof(TValue), out var converted))
                        continue;

                    var key = (TValue)converted;
                    if (!_map.TryGetValue(key, out var list))
                        _map[key] = list = new List<TConfig>();
                    list.Add(item);
                }
            }

            public bool TryGet(object value, out IReadOnlyList<TConfig> list)
            {
                list = Array.Empty<TConfig>();
                if (!TryConvertKey(value, typeof(TValue), out var converted))
                    return false;

                if (!_map.TryGetValue((TValue)converted, out var found))
                    return false;

                list = found;
                return true;
            }
        }

        private sealed class ObjectIndexStore : IIndexStore
        {
            private readonly IndexAccessor _accessor;
            private readonly Dictionary<object, List<TConfig>> _map = new();

            public ObjectIndexStore(IndexAccessor accessor)
            {
                _accessor = accessor;
            }

            public string Name => _accessor.Name;
            public Type ValueType => typeof(object);

            public void Build(List<TConfig> items)
            {
                _map.Clear();
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var keyObj = _accessor.GetValue(item);
                    if (keyObj == null)
                        continue;

                    if (!_map.TryGetValue(keyObj, out var list))
                        _map[keyObj] = list = new List<TConfig>();
                    list.Add(item);
                }
            }

            public bool TryGet(object value, out IReadOnlyList<TConfig> list)
            {
                list = Array.Empty<TConfig>();
                if (value == null)
                    return false;

                if (!_map.TryGetValue(value, out var found))
                    return false;

                list = found;
                return true;
            }
        }
    }
}
