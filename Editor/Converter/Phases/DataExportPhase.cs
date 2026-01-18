using System.IO;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azcel.Editor
{
    /// <summary>
    /// 数据导出阶段
    /// </summary>
    [Register]
    [PhaseId("DataExport")]
    public class DataExportPhase : IConvertPhase
    {
        public int Order => 500;

        public UniTask ExecuteAsync(ConvertContext context)
        {
            if (context.Errors.Count > 0)
            {
                Log.Warning("[DataExport] 存在错误，跳过数据导出");
                return UniTask.CompletedTask;
            }

            var settings = AzcelSettings.Instance;
            var outputPath = string.IsNullOrEmpty(context.TempDataOutputPath)
                ? settings.dataOutputPath
                : context.TempDataOutputPath;
            var formatId = string.IsNullOrEmpty(settings.dataFormatId)
                ? string.Empty
                : settings.dataFormatId.Trim();

            // 确保输出目录存在
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            if (string.IsNullOrEmpty(formatId))
            {
                context.AddError("[DataExport] 未配置格式ID(dataFormatId)，无法导出数据");
                return UniTask.CompletedTask;
            }

            var plugin = ConfigFormatPluginRegistry.Get(formatId);
            if (plugin == null)
            {
                context.AddError($"[DataExport] 未找到格式 {formatId} 的编辑器插件，无法导出数据");
                return UniTask.CompletedTask;
            }

            try
            {
                plugin.Serialize(context, outputPath);
            }
            catch (System.Exception e)
            {
                context.AddError($"[DataExport] 序列化失败: {e.Message}");
                return UniTask.CompletedTask;
            }

            if (context.Errors.Count > 0)
                return UniTask.CompletedTask;

            return UniTask.CompletedTask;
        }
    }
}
