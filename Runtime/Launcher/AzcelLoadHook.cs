using Azathrix.Framework.Core;
using Azathrix.Framework.Core.Launcher;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azcel
{
    /// <summary>
    /// Azcel 数据加载钩子 - 在 Start 阶段之后加载配置数据
    /// 可以通过继承此类并重写方法来自定义加载行为
    /// </summary>
    [HookTarget("Launcher", "Start")]
    public class AzcelLoadHook : IAfterLauncherPhaseHook
    {
        public int Order => 0;

        public async UniTask OnAfterAsync(LauncherContext context)
        {
            var azcel = AzathrixFramework.GetSystem<AzcelSystem>();
            if (azcel == null)
            {
                Log.Warning("[Azcel] AzcelSystem 未注册，跳过数据加载");
                return;
            }

            // 设置默认加载器和解析器
            if (azcel.DataLoader == null)
                azcel.SetDataLoader(new ResourcesDataLoader());
            if (azcel.Parser == null)
                azcel.SetParser(DefaultConfigParser.Instance);

            await LoadAllDataAsync(azcel);
        }

        /// <summary>
        /// 加载所有数据（可重写以自定义加载逻辑）
        /// </summary>
        protected virtual async UniTask LoadAllDataAsync(AzcelSystem azcel)
        {
            var loader = azcel.DataLoader;
            var parser = azcel.Parser;

            // 加载所有已注册的配置
            foreach (var config in azcel.GetAllConfigs())
            {
                var data = loader.Load(config.ConfigName);
                if (data != null)
                    parser.Parse(config, data);
            }

            // 加载所有已注册的全局配置
            foreach (var global in azcel.GetAllGlobals())
            {
                var data = loader.Load(global.ConfigName);
                if (data != null)
                    parser.Parse(global, data);
            }

            await UniTask.CompletedTask;
        }
    }
}
