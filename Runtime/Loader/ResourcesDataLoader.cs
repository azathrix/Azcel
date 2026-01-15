using UnityEngine;

namespace Azcel
{
    /// <summary>
    /// 默认数据加载器（从Resources加载）
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
            var asset = Resources.Load<TextAsset>(path);
            return asset?.bytes;
        }
    }
}
