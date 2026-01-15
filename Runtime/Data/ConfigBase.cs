using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Azcel
{
    /// <summary>
    /// 配置基类
    /// </summary>
    public abstract class ConfigBase
    {
        /// <summary>
        /// 配置名
        /// </summary>
        public abstract string ConfigName { get; }

        /// <summary>
        /// 行数
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// 解析数据（由生成代码实现，高性能低GC）
        /// </summary>
        public abstract void ParseData(byte[] data);
    }

    /// <summary>
    /// 泛型配置基类（高性能）
    /// </summary>
    public abstract class ConfigBase<TRow, TKey> : ConfigBase
        where TRow : struct
    {
        protected TRow[] _rows = Array.Empty<TRow>();
        protected Dictionary<TKey, int> _keyIndex = new();

        public override int Count => _rows.Length;

        /// <summary>
        /// 所有行（零GC遍历）
        /// </summary>
        public ReadOnlySpan<TRow> All => _rows;

        /// <summary>
        /// 通过主键获取行（零拷贝）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TRow GetByKeyRef(TKey key)
        {
            if (_keyIndex.TryGetValue(key, out var index))
                return ref _rows[index];
            throw new KeyNotFoundException($"Key {key} not found in config {ConfigName}");
        }

        /// <summary>
        /// 尝试通过主键获取行
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetByKey(TKey key, out TRow row)
        {
            if (_keyIndex.TryGetValue(key, out var index))
            {
                row = _rows[index];
                return true;
            }
            row = default;
            return false;
        }

        /// <summary>
        /// 检查主键是否存在
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key) => _keyIndex.ContainsKey(key);

        /// <summary>
        /// 设置数据（由解析代码调用）
        /// </summary>
        protected void SetData(TRow[] rows, Dictionary<TKey, int> keyIndex)
        {
            _rows = rows;
            _keyIndex = keyIndex;
            OnDataLoaded();
        }

        /// <summary>
        /// 数据加载完成后调用（子类可重写以构建索引）
        /// </summary>
        protected virtual void OnDataLoaded()
        {
        }
    }
}
