namespace Azcel
{
    /// <summary>
    /// 数据加载器接口 - 负责加载原始二进制数据
    /// </summary>
    public interface IDataLoader
    {
        /// <summary>
        /// 加载配置数据
        /// </summary>
        /// <param name="configName">配置名</param>
        /// <returns>二进制数据，如果不存在返回null</returns>
        byte[] Load(string configName);
    }
}
