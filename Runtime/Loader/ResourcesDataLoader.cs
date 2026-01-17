using Azathrix.Framework.Core;
using UnityEngine;

namespace Azcel
{
    /// <summary>
    /// 默认数据加载器（使用框架的加载器加载）
    /// </summary>
    public class ResourcesDataLoader : IDataLoader
    {
        private readonly string _dataPath;

        public ResourcesDataLoader(string dataPath = null)
        {
            _dataPath = dataPath ?? AzcelSettings.Instance?.dataOutputPath?.Replace("Assets/Resources/", "") ?? "TableData";
        }

        public byte[] Load(string configName)
        {
            var path = $"{_dataPath}/{configName}";
            var asset = AzathrixFramework.ResourcesLoader.Load<TextAsset>(path);
            return asset?.bytes;
        }
    }
}
