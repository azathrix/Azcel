using System.Collections.Generic;
using Azathrix.Framework.Core;

namespace Azcel
{
    /// <summary>
    /// 全局配置基类（KV类型）
    /// </summary>
    public abstract class GlobalConfig : ConfigBase
    {
        protected Dictionary<string, object> _values = new();

        public override int Count => _values.Count;

        /// <summary>
        /// 获取配置值
        /// </summary>
        public T Get<T>(string key)
        {
            if (_values.TryGetValue(key, out var value))
                return (T)value;
            return default;
        }

        /// <summary>
        /// 尝试获取配置值
        /// </summary>
        public bool TryGet<T>(string key, out T value)
        {
            if (_values.TryGetValue(key, out var obj))
            {
                value = (T)obj;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 检查配置是否存在
        /// </summary>
        public bool Contains(string key) => _values.ContainsKey(key);

        /// <summary>
        /// 设置数据（由解析代码调用）
        /// </summary>
        protected void SetData(Dictionary<string, object> values)
        {
            _values = values;
            OnDataLoaded();
        }

        /// <summary>
        /// 数据加载完成后调用
        /// </summary>
        protected virtual void OnDataLoaded()
        {
        }
    }

    /// <summary>
    /// 泛型全局配置基类（单例访问）
    /// </summary>
    public abstract class GlobalConfig<T> : GlobalConfig where T : GlobalConfig<T>
    {
        private static T _instance;

        /// <summary>
        /// 单例访问
        /// </summary>
        public static T I
        {
            get
            {
                if (_instance == null)
                    _instance = AzathrixFramework.GetSystem<AzcelSystem>()?.GetGlobal<T>();
                return _instance;
            }
        }

        /// <summary>
        /// 设置实例（由系统调用）
        /// </summary>
        internal static void SetInstance(T instance)
        {
            _instance = instance;
        }
    }
}
