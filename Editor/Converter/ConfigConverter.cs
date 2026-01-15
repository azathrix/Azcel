using System.Collections.Generic;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azcel.Editor
{
    /// <summary>
    /// 配置转换器
    /// </summary>
    [PipelineId("Azcel.Converter")]
    [PipelineDisplayName("Azcel配置转换")]
    public class ConfigConverter : PipelineBase<IConvertPhase, ConvertContext>
    {
        public ConfigConverter()
        {
            // 添加默认阶段
            AddPhase(new ExcelParsePhase());
            AddPhase(new TableMergePhase());
            AddPhase(new InheritancePhase());
            AddPhase(new ReferenceResolvePhase());
            AddPhase(new CodeGenPhase());
            AddPhase(new DataExportPhase());
        }

        public override async UniTask ExecuteAsync(ConvertContext context)
        {
            Log.Info("[Azcel] 开始配置转换...");

            await base.ExecuteAsync(context);

            if (context.Errors.Count > 0)
            {
                Log.Error($"[Azcel] 转换完成，但有 {context.Errors.Count} 个错误:");
                foreach (var error in context.Errors)
                    Log.Error($"  - {error}");
            }
            else
            {
                Log.Info($"[Azcel] 转换完成! 表: {context.Tables.Count}, 枚举: {context.Enums.Count}, 全局配置: {context.Globals.Count}");
            }

            if (context.Warnings.Count > 0)
            {
                Log.Warning($"[Azcel] 有 {context.Warnings.Count} 个警告:");
                foreach (var warning in context.Warnings)
                    Log.Warning($"  - {warning}");
            }
        }
    }
}
