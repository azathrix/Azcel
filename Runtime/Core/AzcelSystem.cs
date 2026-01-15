using System;
using System.Collections.Generic;
using Azathrix.Framework.Interfaces;

namespace Azcel
{
    /// <summary>
    /// Azcel系统 - 统一管理API（通过 AzathrixFramework.GetSystem<AzcelSystem>() 获取）
    /// </summary>
    public class AzcelSystem : ISystem
    {
        private readonly Dictionary<Type, ConfigBase> _configs = new();
        private readonly Dictionary<Type, GlobalConfig> _globals = new();
        private IDataLoader _dataLoader;
        private IConfigParser _parser;

        /// <summary>
        /// 当前数据加载器
        /// </summary>
        public IDataLoader DataLoader => _dataLoader;

        /// <summary>
        /// 当前配置解析器
        /// </summary>
        public IConfigParser Parser => _parser;

        /// <summary>
        /// 设置数据加载器
        /// </summary>
        public void SetDataLoader(IDataLoader loader) => _dataLoader = loader;

        /// <summary>
        /// 设置配置解析器
        /// </summary>
        public void SetParser(IConfigParser parser) => _parser = parser;

        /// <summary>
        /// 注册配置
        /// </summary>
        public void RegisterConfig<T>(T config) where T : ConfigBase
        {
            _configs[typeof(T)] = config;
        }

        /// <summary>
        /// 注册全局配置
        /// </summary>
        public void RegisterGlobal<T>(T config) where T : GlobalConfig
        {
            _globals[typeof(T)] = config;
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        public T Get<T>() where T : ConfigBase
        {
            return _configs.TryGetValue(typeof(T), out var config) ? (T)config : null;
        }

        /// <summary>
        /// 获取全局配置
        /// </summary>
        public T GetGlobal<T>() where T : GlobalConfig
        {
            return _globals.TryGetValue(typeof(T), out var config) ? (T)config : null;
        }

        /// <summary>
        /// 获取所有已注册的配置
        /// </summary>
        public IEnumerable<ConfigBase> GetAllConfigs() => _configs.Values;

        /// <summary>
        /// 获取所有已注册的全局配置
        /// </summary>
        public IEnumerable<GlobalConfig> GetAllGlobals() => _globals.Values;
    }
}
