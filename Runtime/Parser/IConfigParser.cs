namespace Azcel
{
    /// <summary>
    /// 配置解析器接口 - 负责将二进制数据解析映射到对象
    /// 默认使用生成代码实现高性能解析，也可自定义实现
    /// </summary>
    public interface IConfigParser
    {
        /// <summary>
        /// 解析配置数据
        /// </summary>
        /// <param name="config">目标配置对象</param>
        /// <param name="data">二进制数据</param>
        void Parse(ConfigBase config, byte[] data);
    }

    /// <summary>
    /// 默认解析器 - 调用生成代码的解析方法（高性能低GC）
    /// </summary>
    public class DefaultConfigParser : IConfigParser
    {
        public static readonly DefaultConfigParser Instance = new();

        public void Parse(ConfigBase config, byte[] data)
        {
            // 调用生成代码的解析方法
            config.ParseData(data);
        }
    }
}
