using System.Diagnostics;
using System.IO;
using System.Text;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azcel.Editor
{
    /// <summary>
    /// 代码生成阶段
    /// </summary>
    [PhaseId("CodeGen")]
    [Register]
    public class CodeGenPhase : IConvertPhase
    {
        public int Order => 400;

        public UniTask ExecuteAsync(ConvertContext context)
        {
            if (context.Errors.Count > 0)
            {
                Log.Warning("[CodeGen] 存在错误，跳过代码生成");
                return UniTask.CompletedTask;
            }

            var totalWatch = Stopwatch.StartNew();
            var settings = AzcelSettings.Instance;
            var outputPath = string.IsNullOrEmpty(context.TempCodeOutputPath)
                ? settings.codeOutputPath
                : context.TempCodeOutputPath;

            // 确保输出目录存在
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            // 生成枚举
            foreach (var enumDef in context.Enums)
            {
                var watch = Stopwatch.StartNew();
                var code = EnumCodeGenerator.Generate(enumDef, settings.codeNamespace);
                var filePath = Path.Combine(outputPath, $"{enumDef.Name}.cs");
                File.WriteAllText(filePath, code, Encoding.UTF8);
                watch.Stop();
                context.AddPerfRecord("CodeGen", "Enum", enumDef.Name, enumDef.Values.Count, watch.ElapsedMilliseconds);
            }

            // 生成全局配置
            foreach (var globalDef in context.Globals)
            {
                var watch = Stopwatch.StartNew();
                var code = GlobalCodeGenerator.Generate(globalDef, settings.codeNamespace);
                var filePath = Path.Combine(outputPath, $"GlobalConfig{globalDef.Name}.cs");
                File.WriteAllText(filePath, code, Encoding.UTF8);
                watch.Stop();
                context.AddPerfRecord("CodeGen", "Global", $"GlobalConfig{globalDef.Name}", globalDef.Values.Count,
                    watch.ElapsedMilliseconds);
            }

            // 生成配置
            foreach (var table in context.Tables)
            {
                var watch = Stopwatch.StartNew();
                var code = TableCodeGenerator.Generate(table, settings.codeNamespace);
                var filePath = Path.Combine(outputPath, $"{table.Name}.cs");
                File.WriteAllText(filePath, code, Encoding.UTF8);
                watch.Stop();
                context.AddPerfRecord("CodeGen", "Table", table.Name, table.Rows.Count, watch.ElapsedMilliseconds);
            }

            totalWatch.Stop();
            context.SetPhaseTotal("CodeGen", totalWatch.ElapsedMilliseconds);
            return UniTask.CompletedTask;
        }
    }
}
