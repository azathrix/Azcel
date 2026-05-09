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
            _dataPath = NormalizeResourcesPath(dataPath ?? AzcelSettings.Instance?.dataOutputPath) ?? "TableData";
        }

        public byte[] Load(string configName)
        {
            var path = $"{_dataPath}/{configName}";
            var asset = AzathrixFramework.ResourcesLoader.Load<TextAsset>(path);
            return asset?.bytes;
        }

        private static string NormalizeResourcesPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var normalized = path.Replace('\\', '/').Trim('/');
            const string resources = "/Resources/";
            var index = normalized.IndexOf(resources, System.StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                normalized = normalized[(index + resources.Length)..];
            else if (normalized.StartsWith("Assets/Resources", System.StringComparison.OrdinalIgnoreCase))
                normalized = normalized["Assets/Resources".Length..].Trim('/');

            return string.IsNullOrEmpty(normalized) ? null : normalized;
        }
    }
}
