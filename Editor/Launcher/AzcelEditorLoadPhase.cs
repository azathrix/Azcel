using System.Diagnostics;
using Azathrix.Framework.Core;
using Azathrix.Framework.Core.Launcher;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Editor.Launcher;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azcel.Editor
{
    /// <summary>
    /// 编辑器加载配置阶段
    /// </summary>
    [Register]
    [PhaseId("AzcelEditorLoad")]
    public class AzcelEditorLoadPhase : IEditorInitPhase
    {
        public int Order => 520;

        public UniTask ExecuteAsync(LauncherContext context)
        {
            if (!AzathrixFramework.HasSystem<AzcelSystem>())
            {
                if (!context.SilentMode)
                    Log.Warning("[Azcel][Editor] AzcelSystem 未注册，跳过编辑器配置加载");
                return UniTask.CompletedTask;
            }

            var azcel = AzathrixFramework.GetSystem<AzcelSystem>();
            if (azcel == null)
            {
                if (!context.SilentMode)
                    Log.Warning("[Azcel][Editor] AzcelSystem 未创建，跳过编辑器配置加载");
                return UniTask.CompletedTask;
            }

            if (azcel.DataLoader == null)
                azcel.SetDataLoader(new ResourcesDataLoader());

            azcel.LoadTableRegistry(); 

            if (azcel.TableLoader == null)
                azcel.SetTableLoader(BinaryConfigTableLoader.Instance);

            var loader = azcel.DataLoader;
            var totalWatch = Stopwatch.StartNew();

            foreach (var table in azcel.GetAllTables())
            {
                var data = loader.Load(table.ConfigName);
                if (data != null)
                {
                    var watch = Stopwatch.StartNew();
                    azcel.LoadTable(table, data);
                    watch.Stop();
                    if (!context.SilentMode)
                        Log.Info($"[Azcel][Editor] 加载表: {table.ConfigName}，行数: {table.Count}，耗时: {watch.ElapsedMilliseconds}ms");
                }
                else
                {
                    if (!context.SilentMode)
                        Log.Warning($"[Azcel][Editor] 表数据缺失: {table.ConfigName}");
                }
            }

            totalWatch.Stop();
            if (!context.SilentMode)
                Log.Info($"[Azcel][Editor] 加载完成，总耗时: {totalWatch.ElapsedMilliseconds}ms");

            return UniTask.CompletedTask;
        }
    }
}
