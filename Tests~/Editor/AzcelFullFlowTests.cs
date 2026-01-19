using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Azcel.Sample;
using Azathrix.Framework.Core;
using Azathrix.Framework.Core.Pipeline;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Azcel.Sample.Tests
{
    public class AzcelFullFlowTests
    {
        private AzcelSettings _originalSettings;
        private SystemRuntimeManager _runtimeManager;

        [SetUp]
        public void SetUp()
        {
            _originalSettings = AzcelSettings.Instance;

            var settings = ScriptableObject.CreateInstance<AzcelSettings>();
            settings.excelPaths = new List<string> { AzcelSampleEnvironment.ExcelDirectory };
            settings.codeOutputPath = Path.Combine("Library", "AzcelTest", "Code");
            settings.dataOutputPath = "Assets/Resources/AzcelTestData";
            settings.codeNamespace = "Azcel.Sample";
            settings.dataFormatId = "binary";
            settings.arraySeparator = "|";
            settings.objectSeparator = ",";
            settings.defaultKeyField = "Id";
            settings.defaultKeyType = "int";
            settings.defaultFieldRow = 2;
            settings.defaultTypeRow = 3;

            AzcelSettings.SetSettings(settings);

            EnsureCleanOutput(settings.dataOutputPath);

            _runtimeManager = new SystemRuntimeManager { IsEditorMode = true };
            AzathrixFramework.SetEditorRuntimeManager(_runtimeManager);
            AzathrixFramework.MarkEditorStarted();
        }

        [TearDown]
        public void TearDown()
        {
            AzcelSettings.SetSettings(_originalSettings);
            AzathrixFramework.ResetEditorRuntime();
        }

        [UnityTest]
        public IEnumerator ConvertAndLoad_FullFlow()
        {
            yield return RegisterAzcelSystem().ToCoroutine();

            var converter = PipelineFactory.Get<ConfigConverter>();
            Assert.NotNull(converter, "未找到 Azcel.Converter 管线");

            var context = new ConvertContext { SkipAssetRefresh = false };
            yield return converter.ExecuteAsync(context).ToCoroutine();

            Assert.IsFalse(context.Errors.Count > 0, string.Join("\n", context.Errors));

            // 等待自动加载触发（delayCall）
            yield return null;

            var azcel = AzathrixFramework.GetSystem<AzcelSystem>();
            Assert.NotNull(azcel);

            // 兜底：若未加载，手动加载一次
            if (!azcel.GetAllTables().Any() || azcel.GetAllTables().All(t => t.Count == 0))
                yield return ManualLoad(azcel).ToCoroutine();

            // 基础表/注册表
            var itemTable = azcel.GetTable<TestItemTable>();
            Assert.NotNull(itemTable);
            Assert.AreEqual(3, itemTable.Count);

            var otherTable = azcel.GetTable<TestOtherTable>();
            Assert.NotNull(otherTable);
            Assert.AreEqual(2, otherTable.Count);

            // GetTable<TConfig, TKey>()
            var typedTable = azcel.GetTable<TestItem, int>();
            Assert.NotNull(typedTable);
            Assert.AreEqual(3, typedTable.Count);

            // GetConfig / TryGetConfig
            var item1 = azcel.GetConfig<TestItem, int>(1);
            Assert.NotNull(item1);
            Assert.AreEqual("Sword", item1.Name);

            Assert.IsTrue(azcel.TryGetConfig<TestItem, int>(2, out var item2));
            Assert.AreEqual("Shield", item2.Name);

            Assert.IsFalse(azcel.TryGetConfig<TestItem, int>(999, out _));

            var itemObj = azcel.GetConfig<TestItem>((object)3);
            Assert.NotNull(itemObj);
            Assert.AreEqual("Bow", itemObj.Name);

            // GetAllConfig
            var allAlloc = azcel.GetAllConfig<TestItem>();
            Assert.AreEqual(3, allAlloc.Count);
            var allNoAlloc = azcel.GetAllConfig<TestItem, int>();
            Assert.AreEqual(3, allNoAlloc.Count);

            // 索引查询
            var byType = azcel.GetByIndex<TestItem>("Type", 1);
            Assert.AreEqual(2, byType.Count);

            var byGroup = azcel.GetByIndex<TestItem, int>("Group", 10);
            Assert.AreEqual(2, byGroup.Count);

            // 其他表
            var other = azcel.GetConfig<TestOther, string>("A");
            Assert.NotNull(other);
            Assert.AreEqual(100, other.Value);
            Assert.AreEqual(0.5f, other.Rate, 0.0001f);

            // ConfigBase API
            Assert.AreEqual("Sword", item1.GetValue<string>("Name"));
            Assert.IsTrue(item1.TryGetValue("Type", out int typeValue));
            Assert.AreEqual(1, typeValue);
        }

        private async UniTask RegisterAzcelSystem()
        {
            await _runtimeManager.RegisterSystemAsync<AzcelSystem>();
            var azcel = AzathrixFramework.GetSystem<AzcelSystem>();
            if (azcel.DataLoader == null)
                azcel.SetDataLoader(new ResourcesDataLoader());
            if (azcel.TableLoader == null)
                azcel.SetTableLoader(BinaryConfigTableLoader.Instance);
            azcel.LoadTableRegistry();
        }

        private async UniTask ManualLoad(AzcelSystem azcel)
        {
            var loader = azcel.DataLoader ?? new ResourcesDataLoader();
            foreach (var table in azcel.GetAllTables())
            {
                var data = loader.Load(table.ConfigName);
                if (data != null)
                    azcel.LoadTable(table, data);
            }

            await UniTask.Yield();
        }

        private static void EnsureCleanOutput(string assetPath)
        {
            var full = Path.GetFullPath(assetPath);
            if (Directory.Exists(full))
                Directory.Delete(full, true);
            Directory.CreateDirectory(full);
            AssetDatabase.Refresh();
        }
    }

    internal static class AzcelSampleEnvironment
    {
        private const string WorkbookName = "AzcelTestWorkbook";

        public static string ExcelDirectory
        {
            get
            {
                var guid = AssetDatabase.FindAssets(WorkbookName).FirstOrDefault();
                if (string.IsNullOrEmpty(guid))
                    Assert.Fail($"未找到测试工作簿：{WorkbookName}.xlsx，请先导入 Azcel Tests Sample");

                var path = AssetDatabase.GUIDToAssetPath(guid);
                var dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir))
                    Assert.Fail($"测试工作簿路径无效：{path}");
                return dir.Replace("\\", "/");
            }
        }
    }
}
