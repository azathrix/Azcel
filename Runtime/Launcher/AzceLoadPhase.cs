using System.Diagnostics;
using Azathrix.Framework.Core;
using Azathrix.Framework.Core.Launcher;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azcel
{
    [Register]
    public class AzceLoadPhase : ILauncherPhase
    {
        
        public int Order => 430;
        public async UniTask ExecuteAsync(LauncherContext context)
        {
            var azcel = AzathrixFramework.GetSystem<AzcelSystem>();
            if (azcel == null)
            {
                Log.Warning("[Azcel] AzcelSystem 未注册，跳过数据加载");
                return;
            }

            // 设置默认加载器
            if (azcel.DataLoader == null)
                azcel.SetDataLoader(new ResourcesDataLoader());

            azcel.LoadTableRegistry();

            if (azcel.TableLoader == null)
                azcel.SetTableLoader(BinaryConfigTableLoader.Instance);

            await LoadAllDataAsync(azcel);
        }

      

        /// <summary>
        /// 加载所有数据（可重写以自定义加载逻辑）
        /// </summary>
        protected virtual async UniTask LoadAllDataAsync(AzcelSystem azcel)
        {
            var loader = azcel.DataLoader;
            var totalWatch = Stopwatch.StartNew();

            // 加载所有已注册的表配置
            foreach (var table in azcel.GetAllTables())
            {
                var data = loader.Load(table.ConfigName);
                if (data != null)
                {
                    var watch = Stopwatch.StartNew();
                    azcel.LoadTable(table, data);
                    watch.Stop();
                    Log.Info($"[Azcel] 加载表: {table.ConfigName}，行数: {table.Count}，耗时: {watch.ElapsedMilliseconds}ms");
                }
                else
                {
                    Log.Warning($"[Azcel] 表数据缺失: {table.ConfigName}");
                }
            }

            totalWatch.Stop();
            Log.Info($"[Azcel] 加载完成，总耗时: {totalWatch.ElapsedMilliseconds}ms");
            await UniTask.CompletedTask;
        }

    }
}
