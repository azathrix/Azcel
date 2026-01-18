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

            var settings = AzcelSettings.Instance;
            var outputPath = string.IsNullOrEmpty(context.TempCodeOutputPath)
                ? settings.codeOutputPath
                : context.TempCodeOutputPath;

            var formatId = string.IsNullOrEmpty(settings.dataFormatId)
                ? string.Empty
                : settings.dataFormatId.Trim();

            var plugin = ConfigFormatPluginRegistry.Get(formatId);
            if (plugin == null)
            {
                Log.Warning($"[CodeGen] 未找到格式 {formatId} 的编辑器插件，跳过代码生成");
                return UniTask.CompletedTask;
            }

            try
            {
                plugin.Generate(context, outputPath, settings.codeNamespace);
            }
            catch (System.Exception e)
            {
                context.AddError($"[CodeGen] 生成失败: {e.Message}");
            }
            return UniTask.CompletedTask;
        }
    }
}
