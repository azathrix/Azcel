using System.IO;
using System.Text;

namespace Azcel.Editor
{
    /// <summary>
    /// Binary 格式插件（编辑器）
    /// </summary>
    [ConfigFormatPlugin("binary")]
    public sealed class BinaryFormatEditor : IConfigFormat
    {
        private readonly BinaryConfigDataSerializer _serializer = new();

        public string FormatId => "binary";

        public void Serialize(ConvertContext context, string outputPath)
        {
            _serializer.Serialize(context, outputPath);
        }

        public void Generate(ConvertContext context, string outputPath, string codeNamespace)
        {
            ConfigCodeGenerator.Generate(context, outputPath, codeNamespace);

            var bootstrap = RuntimeBootstrapGenerator.Generate(
                codeNamespace,
                "Azcel.BinaryConfigTableLoader.Instance",
                context.Tables);
            File.WriteAllText(Path.Combine(outputPath, "TableRegistry.cs"), bootstrap, Encoding.UTF8);
        }
    }
}
