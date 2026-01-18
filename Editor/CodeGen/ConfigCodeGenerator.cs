using System.Diagnostics;
using System.IO;
using System.Text;

namespace Azcel.Editor
{
    /// <summary>
    /// 通用代码生成器（表/枚举/全局）
    /// </summary>
    public static class ConfigCodeGenerator
    {
        public static void Generate(ConvertContext context, string outputPath, string codeNamespace)
        {
            var totalWatch = Stopwatch.StartNew();

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            foreach (var enumDef in context.Enums)
            {
                var watch = Stopwatch.StartNew();
                var code = EnumCodeGenerator.Generate(enumDef, codeNamespace);
                var filePath = Path.Combine(outputPath, $"{enumDef.Name}.cs");
                File.WriteAllText(filePath, code, Encoding.UTF8);
                watch.Stop();
                context.AddPerfRecord("CodeGen", "Enum", enumDef.Name, enumDef.Values.Count, watch.ElapsedMilliseconds);
            }

            foreach (var globalDef in context.Globals)
            {
                var watch = Stopwatch.StartNew();
                var code = GlobalCodeGenerator.Generate(globalDef, codeNamespace);
                var filePath = Path.Combine(outputPath, $"{globalDef.Name}.cs");
                File.WriteAllText(filePath, code, Encoding.UTF8);
                watch.Stop();
                context.AddPerfRecord("CodeGen", "Global", $"{globalDef.Name}", globalDef.Values.Count,
                    watch.ElapsedMilliseconds);
            }

            foreach (var table in context.Tables)
            {
                var watch = Stopwatch.StartNew();
                var code = TableCodeGenerator.Generate(table, codeNamespace);
                var filePath = Path.Combine(outputPath, $"{table.Name}.cs");
                File.WriteAllText(filePath, code, Encoding.UTF8);
                watch.Stop();
                context.AddPerfRecord("CodeGen", "Table", table.Name, table.Rows.Count, watch.ElapsedMilliseconds);
            }

            totalWatch.Stop();
            context.SetPhaseTotal("CodeGen", totalWatch.ElapsedMilliseconds);
        }
    }
}
