namespace Azcel.Editor
{
    /// <summary>
    /// 编辑器格式插件（序列化数据 + 生成代码）
    /// </summary>
    public interface IConfigFormat
    {
        /// <summary>序列化干净数据到目标目录</summary>
        void Serialize(ConvertContext context, string outputPath);

        /// <summary>生成反序列化代码（可留空实现）</summary>
        void Generate(ConvertContext context, string outputPath, string codeNamespace);
    }
}
