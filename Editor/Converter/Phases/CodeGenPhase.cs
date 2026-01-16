using System.IO;
using System.Text;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;
using UnityEditor;

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

            var settings = AzcelSettings.Instance;
            var outputPath = settings.codeOutputPath;

            // 确保输出目录存在
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            // 生成枚举
            foreach (var enumDef in context.Enums)
            {
                var code = EnumCodeGenerator.Generate(enumDef, settings.codeNamespace);
                var filePath = Path.Combine(outputPath, $"{enumDef.Name}.cs");
                File.WriteAllText(filePath, code, Encoding.UTF8);
                Log.Info($"[CodeGen] 生成枚举: {enumDef.Name}");
            }

            // 生成全局配置
            foreach (var globalDef in context.Globals)
            {
                var code = GlobalCodeGenerator.Generate(globalDef, settings.codeNamespace, settings.dataFormat);
                var filePath = Path.Combine(outputPath, $"GlobalConfig{globalDef.Name}.cs");
                File.WriteAllText(filePath, code, Encoding.UTF8);
                Log.Info($"[CodeGen] 生成全局配置: GlobalConfig{globalDef.Name}");
            }

            // 生成配置
            foreach (var table in context.Tables)
            {
                var code = TableCodeGenerator.Generate(table, settings.codeNamespace, settings.dataFormat);
                var filePath = Path.Combine(outputPath, $"{table.Name}Config.cs");
                File.WriteAllText(filePath, code, Encoding.UTF8);
                Log.Info($"[CodeGen] 生成配置: {table.Name}Config");
            }

            // 生成配置注册代码
            var registerCode = TableCodeGenerator.GenerateRegister(context.Tables, context.Globals, settings.codeNamespace, settings.dataFormat);
            var registerPath = Path.Combine(outputPath, "ConfigRegister.cs");
            File.WriteAllText(registerPath, registerCode, Encoding.UTF8);

            AssetDatabase.Refresh();
            Log.Info($"[CodeGen] 代码生成完成");
            return UniTask.CompletedTask;
        }
    }
}
