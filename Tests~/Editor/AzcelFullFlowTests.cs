using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Azathrix.Framework.Core;
using Azathrix.Framework.Core.Pipeline;
using Azcel.Editor;
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
            var excelDir = AzcelSampleEnvironment.ExcelDirectory;
            var testsRoot = Directory.GetParent(excelDir)?.Parent?.FullName ?? excelDir;
            settings.excelPaths = new List<string> { excelDir };
            settings.codeOutputPath = Path.Combine(testsRoot, "Generate");
            settings.dataOutputPath = Path.Combine(testsRoot, "Resources", "AzcelTestData");
            settings.codeNamespace = "Azcel.Sample";
            settings.dataFormatId = "binary";
            settings.arraySeparator = "|";
            settings.objectSeparator = ",";
            settings.defaultKeyField = "Id";
            settings.defaultKeyType = "int";
            settings.defaultFieldRow = 2;
            settings.defaultTypeRow = 3;

            AzcelSettings.SetSettings(settings);

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

        [UnityTest, Order(1)]
        public IEnumerator ConvertOnly_GenerateAndValidate()
        {
            yield return RegisterAzcelSystem().ToCoroutine();

            EnsureCleanOutput(AzcelSettings.Instance?.dataOutputPath);
            var context = new ConvertContext { SkipAssetRefresh = false };
            yield return RunConvertAsync(context).ToCoroutine();
            AssertHasDataFiles(AzcelSettings.Instance?.dataOutputPath);

            // 解析覆盖：enum / global / comment / setting（不依赖具体表名）
            Assert.GreaterOrEqual(context.Tables.Count, 1);
            Assert.GreaterOrEqual(context.Enums.Count, 1);
            Assert.GreaterOrEqual(context.Globals.Count, 1);

            Assert.IsTrue(context.Tables.SelectMany(t => t.Fields)
                .Any(f => !string.IsNullOrEmpty(f.Comment)), "未解析到任何字段注释");

            Assert.IsTrue(context.Tables.SelectMany(t => t.Fields)
                .Any(f => f.Options.Count > 0), "未解析到任何字段配置");

            // 全局配置：通过反射验证生成代码内容（不依赖具体键名）
            var globalDef = context.Globals.First();
            var genMethod = typeof(GlobalCodeGenerator).GetMethod("Generate", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(genMethod);
            var generated = genMethod.Invoke(null, new object[] { globalDef, AzcelSettings.Instance?.codeNamespace ?? "Game.Tables" }) as string;
            Assert.NotNull(generated);
            foreach (var value in globalDef.Values)
            {
                if (!string.IsNullOrEmpty(value.Key))
                    Assert.IsTrue(generated.Contains(value.Key), $"未包含全局键: {value.Key}");
            }
        }

        [UnityTest, Order(2)]
        public IEnumerator LoadAndValidate_Runtime()
        {
            yield return RegisterAzcelSystem().ToCoroutine();

            var azcel = AzathrixFramework.GetSystem<AzcelSystem>();
            Assert.NotNull(azcel);

            var dataPath = AzcelSettings.Instance?.dataOutputPath;
            Debug.Log($"[AzcelTests] runtime dataOutputPath: {Path.GetFullPath(dataPath ?? string.Empty)}, bytes: {CountDataFiles(dataPath)}");
            if (!HasDataFiles(dataPath))
                Assert.Inconclusive("未检测到输出数据，请先运行 ConvertOnly_GenerateAndValidate");

            // 兜底：若未加载，手动加载一次
            if (!azcel.GetAllTables().Any() || azcel.GetAllTables().All(t => t.Count == 0))
                yield return ManualLoad(azcel).ToCoroutine();

            var context = new ConvertContext { SkipAssetRefresh = true };
            yield return BuildContextForValidationAsync(context).ToCoroutine();
            Assert.IsFalse(context.Errors.Count > 0, string.Join("\n", context.Errors));

            ValidateTablesByReflection(azcel, context);
            ValidateTableValues(azcel, context);
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_MarkerRowsAndOptions_ShouldParse()
        {
            var tempDir = CreateTempDir("MarkerRows");
            try
            {
                var path = Path.Combine(tempDir, "marker.xlsx");
                WriteSimpleXlsx(path, "MarkerTable", new[]
                {
                    new[] { "MarkerTable" },
                    new[] { "Id", "Name", "Value" },
                    new[] { "int", "string", "int" },
                    new[] { "#skip", "x", "y" },
                    new[] { "#setting", "", "skip", "" },
                    new[] { "1", "Alpha", "10" },
                    new[] { "#comment", "IdDesc", "NameDesc", "ValueDesc" },
                    new[] { "2", "Beta", "20" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase()).ToCoroutine();
                Assert.IsFalse(context.Errors.Count > 0, string.Join("\n", context.Errors));

                var table = context.Tables.FirstOrDefault(t => t.Name == "MarkerTable");
                Assert.NotNull(table);
                Assert.AreEqual(2, table.Rows.Count, "# 行应被跳过，仅保留两条数据");

                var nameField = table.Fields.FirstOrDefault(f => f.Name == "Name");
                Assert.NotNull(nameField);
                Assert.IsTrue(nameField.Options.ContainsKey("skip"));
                Assert.AreEqual("NameDesc", nameField.Comment);
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_IndexParam_ShouldSetIsIndex()
        {
            var tempDir = CreateTempDir("IndexParam");
            try
            {
                var path = Path.Combine(tempDir, "index.xlsx");
                WriteSimpleXlsx(path, "IndexTable", new[]
                {
                    new[] { "IndexTable", "index:Name,Value" },
                    new[] { "Id", "Name", "Value" },
                    new[] { "int", "string", "int" },
                    new[] { "1", "A", "10" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase()).ToCoroutine();
                Assert.IsFalse(context.Errors.Count > 0, string.Join("\n", context.Errors));

                var table = context.Tables.FirstOrDefault(t => t.Name == "IndexTable");
                Assert.NotNull(table);
                Assert.IsTrue(table.Fields.First(f => f.Name == "Name").IsIndex);
                Assert.IsTrue(table.Fields.First(f => f.Name == "Value").IsIndex);
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_Inheritance_ShouldAppendFields()
        {
            var tempDir = CreateTempDir("Inheritance");
            try
            {
                var basePath = Path.Combine(tempDir, "base.xlsx");
                WriteSimpleXlsx(basePath, "BaseTable", new[]
                {
                    new[] { "BaseTable" },
                    new[] { "Id", "BaseValue" },
                    new[] { "int", "string" },
                    new[] { "1", "Base" }
                });

                var childPath = Path.Combine(tempDir, "child.xlsx");
                WriteSimpleXlsx(childPath, "ChildTable", new[]
                {
                    new[] { "ChildTable", "extends:BaseTable" },
                    new[] { "Id", "ChildValue" },
                    new[] { "int", "int" },
                    new[] { "1", "5" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase(), new InheritancePhase()).ToCoroutine();
                Assert.IsFalse(context.Errors.Count > 0, string.Join("\n", context.Errors));

                var child = context.Tables.FirstOrDefault(t => t.Name == "ChildTable");
                Assert.NotNull(child);
                Assert.IsTrue(child.Fields.Any(f => f.Name == "BaseValue"), "继承字段未追加");
                Assert.IsTrue(child.Fields.Any(f => f.Name == "ChildValue"));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_MergeOverrideByDepth_ShouldUseDeeperRow()
        {
            var tempDir = CreateTempDir("MergeOverride");
            try
            {
                var basePath = Path.Combine(tempDir, "base.xlsx");
                WriteSimpleXlsx(basePath, "MergeTable", new[]
                {
                    new[] { "MergeTable" },
                    new[] { "Id", "Value" },
                    new[] { "int", "int" },
                    new[] { "1", "10" }
                });

                var patchDir = Path.Combine(tempDir, "Patch");
                Directory.CreateDirectory(patchDir);
                var patchPath = Path.Combine(patchDir, "patch.xlsx");
                WriteSimpleXlsx(patchPath, "MergeTable", new[]
                {
                    new[] { "MergeTable" },
                    new[] { "Id", "Value" },
                    new[] { "int", "int" },
                    new[] { "1", "20" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase()).ToCoroutine();
                Assert.IsFalse(context.Errors.Count > 0, string.Join("\n", context.Errors));

                var table = context.Tables.FirstOrDefault(t => t.Name == "MergeTable");
                Assert.NotNull(table);
                Assert.AreEqual(1, table.Rows.Count);
                Assert.AreEqual("20", table.Rows[0].Values["Value"]);
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_MergeMissingField_ShouldNotError()
        {
            var tempDir = CreateTempDir("MergeMissingField");
            try
            {
                var basePath = Path.Combine(tempDir, "base.xlsx");
                WriteSimpleXlsx(basePath, "MergeTable", new[]
                {
                    new[] { "MergeTable" },
                    new[] { "Id", "A" },
                    new[] { "int", "int" },
                    new[] { "1", "10" }
                });

                var patchDir = Path.Combine(tempDir, "Patch");
                Directory.CreateDirectory(patchDir);
                var patchPath = Path.Combine(patchDir, "patch.xlsx");
                WriteSimpleXlsx(patchPath, "MergeTable", new[]
                {
                    new[] { "MergeTable" },
                    new[] { "Id", "B" },
                    new[] { "int", "string" },
                    new[] { "1", "X" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase()).ToCoroutine();
                Assert.IsFalse(context.Errors.Any(e => e.Contains("[Schema]")), string.Join("\n", context.Errors));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_EnumAndGlobal_ShouldParse()
        {
            var tempDir = CreateTempDir("EnumGlobal");
            try
            {
                var enumPath = Path.Combine(tempDir, "enum.xlsx");
                WriteSimpleXlsx(enumPath, "KindEnum", new[]
                {
                    new[] { "KindEnum", "config_type:enum" },
                    new[] { "" },
                    new[] { "" },
                    new[] { "" },
                    new[] { "Alpha", "1", "A" },
                    new[] { "Beta", "2", "B" }
                });

                var globalPath = Path.Combine(tempDir, "global.xlsx");
                WriteSimpleXlsx(globalPath, "GameGlobal", new[]
                {
                    new[] { "GameGlobal", "config_type:global" },
                    new[] { "#comment", "skip" },
                    new[] { "键", "值", "类型", "说明" },
                    new[] { "MaxLevel", "99", "int", "最大等级" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase(), new ValidationPhase()).ToCoroutine();
                Assert.IsFalse(context.Errors.Count > 0, string.Join("\n", context.Errors));

                Assert.AreEqual(1, context.Enums.Count);
                Assert.AreEqual(1, context.Globals.Count);
                Assert.AreEqual(2, context.Enums[0].Values.Count);
                Assert.AreEqual("MaxLevel", context.Globals[0].Values[0].Key);
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_ReferenceResolve_ShouldSucceed()
        {
            var tempDir = CreateTempDir("ReferenceResolve");
            try
            {
                var enumPath = Path.Combine(tempDir, "enum.xlsx");
                WriteSimpleXlsx(enumPath, "KindEnum", new[]
                {
                    new[] { "KindEnum", "config_type:enum" },
                    new[] { "" },
                    new[] { "" },
                    new[] { "" },
                    new[] { "Alpha", "1", "A" }
                });

                var targetPath = Path.Combine(tempDir, "target.xlsx");
                WriteSimpleXlsx(targetPath, "RefTarget", new[]
                {
                    new[] { "RefTarget" },
                    new[] { "Id", "Name" },
                    new[] { "int", "string" },
                    new[] { "1", "Target" }
                });

                var holderPath = Path.Combine(tempDir, "holder.xlsx");
                WriteSimpleXlsx(holderPath, "RefHolder", new[]
                {
                    new[] { "RefHolder" },
                    new[] { "Id", "Ref", "Kind" },
                    new[] { "int", "@RefTarget", "#KindEnum" },
                    new[] { "1", "1", "Alpha" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase(), new ReferenceResolvePhase(), new ValidationPhase()).ToCoroutine();
                Assert.IsFalse(context.Errors.Count > 0, string.Join("\n", context.Errors));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_MissingRefTable_ShouldReportError()
        {
            var tempDir = CreateTempDir("MissingRefTable");
            try
            {
                var path = Path.Combine(tempDir, "missing_ref.xlsx");
                WriteSimpleXlsx(path, "RefMissing", new[]
                {
                    new[] { "RefMissing" },
                    new[] { "Id", "Ref" },
                    new[] { "int", "@NotExist" },
                    new[] { "1", "1" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase(), new ReferenceResolvePhase()).ToCoroutine();
                Assert.IsTrue(context.Errors.Any(e => e.Contains("RefMissing") && e.Contains("不存在")));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_EmptyTable_ShouldAllowNoRows()
        {
            var tempDir = CreateTempDir("EmptyTable");
            try
            {
                var path = Path.Combine(tempDir, "empty.xlsx");
                WriteSimpleXlsx(path, "EmptyTable", new[]
                {
                    new[] { "EmptyTable" },
                    new[] { "Id", "Name" },
                    new[] { "int", "string" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase(), new ValidationPhase()).ToCoroutine();
                Assert.IsFalse(context.Errors.Count > 0, string.Join("\n", context.Errors));

                var table = context.Tables.FirstOrDefault(t => t.Name == "EmptyTable");
                Assert.NotNull(table);
                Assert.AreEqual(0, table.Rows.Count);
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_InvalidValue_ShouldReportError()
        {
            var tempDir = CreateTempDir("InvalidValue");
            try
            {
                var path = Path.Combine(tempDir, "bad.xlsx");
                WriteSimpleXlsx(path, "BadTable", new[]
                {
                    new[] { "BadTable" },
                    new[] { "Id", "Value" },
                    new[] { "int", "int" },
                    new[] { "1", "abc" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase(), new ValidationPhase()).ToCoroutine();
                Assert.IsTrue(context.Errors.Any(e => e.Contains("BadTable") && e.Contains("无法解析")));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_SchemaConflict_ShouldReportError()
        {
            var tempDir = CreateTempDir("SchemaConflict");
            try
            {
                var basePath = Path.Combine(tempDir, "base.xlsx");
                WriteSimpleXlsx(basePath, "MergeTable", new[]
                {
                    new[] { "MergeTable" },
                    new[] { "Id", "Value" },
                    new[] { "int", "int" },
                    new[] { "1", "10" }
                });

                var patchDir = Path.Combine(tempDir, "Patch");
                Directory.CreateDirectory(patchDir);
                var patchPath = Path.Combine(patchDir, "patch.xlsx");
                WriteSimpleXlsx(patchPath, "MergeTable", new[]
                {
                    new[] { "MergeTable" },
                    new[] { "Id", "Value" },
                    new[] { "int", "string" },
                    new[] { "1", "bad" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase()).ToCoroutine();
                Assert.IsTrue(context.Errors.Any(e => e.Contains("[Schema]") && e.Contains("MergeTable")));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_InvalidEnumRef_ShouldReportError()
        {
            var tempDir = CreateTempDir("InvalidEnumRef");
            try
            {
                var path = Path.Combine(tempDir, "enum.xlsx");
                WriteSimpleXlsx(path, "EnumBad", new[]
                {
                    new[] { "EnumBad" },
                    new[] { "Id", "Kind" },
                    new[] { "int", "#MissingEnum" },
                    new[] { "1", "Unknown" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase(), new ValidationPhase()).ToCoroutine();
                Assert.IsTrue(context.Errors.Any(e => e.Contains("EnumBad") && e.Contains("枚举值不存在")));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_InvalidBool_ShouldReportError()
        {
            var tempDir = CreateTempDir("InvalidBool");
            try
            {
                var path = Path.Combine(tempDir, "bool.xlsx");
                WriteSimpleXlsx(path, "BoolBad", new[]
                {
                    new[] { "BoolBad" },
                    new[] { "Id", "Flag" },
                    new[] { "int", "bool" },
                    new[] { "1", "TURE" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase(), new ValidationPhase()).ToCoroutine();
                Assert.IsTrue(context.Errors.Any(e => e.Contains("BoolBad") && e.Contains("bool")));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_InvalidArrayElement_ShouldReportError()
        {
            var tempDir = CreateTempDir("InvalidArray");
            try
            {
                var path = Path.Combine(tempDir, "array.xlsx");
                WriteSimpleXlsx(path, "ArrayBad", new[]
                {
                    new[] { "ArrayBad" },
                    new[] { "Id", "Nums" },
                    new[] { "int", "int[]" },
                    new[] { "1", "1|x|3" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase(), new ValidationPhase()).ToCoroutine();
                Assert.IsTrue(context.Errors.Any(e => e.Contains("ArrayBad") && e.Contains("数组元素")));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_InvalidTableRef_ShouldReportError()
        {
            var tempDir = CreateTempDir("InvalidRef");
            try
            {
                var path = Path.Combine(tempDir, "ref.xlsx");
                WriteSimpleXlsx(path, "RefBad", new[]
                {
                    new[] { "RefBad" },
                    new[] { "Id", "Ref" },
                    new[] { "int", "@AnyTable" },
                    new[] { "1", "abc" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase(), new ValidationPhase()).ToCoroutine();
                Assert.IsTrue(context.Errors.Any(e => e.Contains("RefBad") && e.Contains("引用ID")));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_InvalidVectorComponent_ShouldReportError()
        {
            var tempDir = CreateTempDir("InvalidVector");
            try
            {
                var path = Path.Combine(tempDir, "vector.xlsx");
                WriteSimpleXlsx(path, "VectorBad", new[]
                {
                    new[] { "VectorBad" },
                    new[] { "Id", "Pos" },
                    new[] { "int", "Vector3" },
                    new[] { "1", "1,a,3" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase(), new ValidationPhase()).ToCoroutine();
                Assert.IsTrue(context.Errors.Any(e => e.Contains("VectorBad") && e.Contains("分量")));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_InvalidGlobalHeader_ShouldReportError()
        {
            var tempDir = CreateTempDir("InvalidGlobalHeader");
            try
            {
                var path = Path.Combine(tempDir, "global.xlsx");
                WriteSimpleXlsx(path, "BadGlobal", new[]
                {
                    new[] { "BadGlobal", "config_type:global" },
                    new[] { "Name", "Val" },
                    new[] { "A", "1" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase()).ToCoroutine();
                Assert.IsTrue(context.Errors.Any(e => e.Contains("BadGlobal") && e.Contains("未找到列定义行")));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        [UnityTest, Order(10)]
        public IEnumerator Convert_InvalidGlobalValue_ShouldReportError()
        {
            var tempDir = CreateTempDir("InvalidGlobalValue");
            try
            {
                var path = Path.Combine(tempDir, "global_value.xlsx");
                WriteSimpleXlsx(path, "GlobalBad", new[]
                {
                    new[] { "GlobalBad", "config_type:global" },
                    new[] { "key", "value", "type", "comment" },
                    new[] { "Max", "abc", "int", "" }
                });

                var context = new ConvertContext { TempExcelPath = tempDir, SkipAssetRefresh = true };
                yield return ExecutePhases(context, new ExcelParsePhase(), new ValidationPhase()).ToCoroutine();
                Assert.IsTrue(context.Errors.Any(e => e.Contains("GlobalBad") && e.Contains("不是有效的 int")));
            }
            finally
            {
                SafeDeleteDir(tempDir);
            }
        }

        private async UniTask RegisterAzcelSystem()
        {
            await _runtimeManager.RegisterSystemAsync<AzcelSystem>();
            var azcel = AzathrixFramework.GetSystem<AzcelSystem>();
            if (azcel.DataLoader == null)
                azcel.SetDataLoader(new ResourcesDataLoader("AzcelTestData"));
            if (azcel.TableLoader == null)
                azcel.SetTableLoader(BinaryConfigTableLoader.Instance);
            azcel.LoadTableRegistry();
            if (!azcel.GetAllTables().Any())
                RegisterTablesByReflection(azcel, AzcelSettings.Instance?.codeNamespace);
        }

        private static async UniTask RunConvertAsync(ConvertContext context)
        {
            var converter = PipelineFactory.Get<ConfigConverter>();
            Assert.NotNull(converter, "未找到 Azcel.Converter 管线");
            await converter.ExecuteAsync(context);
            Assert.IsFalse(context.Errors.Count > 0, string.Join("\n", context.Errors));
        }

        private static async UniTask BuildContextForValidationAsync(ConvertContext context)
        {
            var phases = new IConvertPhase[]
            {
                new ExcelParsePhase(),
                new TableMergePhase(),
                new InheritancePhase(),
                new ReferenceResolvePhase(),
                new ValidationPhase()
            };

            foreach (var phase in phases)
                await phase.ExecuteAsync(context);
        }

        private static async UniTask ExecutePhases(ConvertContext context, params IConvertPhase[] phases)
        {
            foreach (var phase in phases)
                await phase.ExecuteAsync(context);
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

        private static void AssertHasDataFiles(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
                Assert.Fail("dataOutputPath 为空，无法检查数据输出");

            var full = Path.GetFullPath(outputPath);
            if (!Directory.Exists(full))
                Assert.Fail($"数据输出目录不存在: {full}");

            var files = Directory.GetFiles(full, "*.bytes", SearchOption.AllDirectories);
            Debug.Log($"[AzcelTests] dataOutputPath: {full}, bytes: {files.Length}");
            Assert.IsTrue(files.Length > 0, $"未生成任何数据文件: {full}");
        }

        private static bool HasDataFiles(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
                return false;

            var full = Path.GetFullPath(outputPath);
            if (!Directory.Exists(full))
                return false;

            var files = Directory.GetFiles(full, "*.bytes", SearchOption.AllDirectories);
            return files.Length > 0;
        }

        private static int CountDataFiles(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
                return 0;

            var full = Path.GetFullPath(outputPath);
            if (!Directory.Exists(full))
                return 0;

            return Directory.GetFiles(full, "*.bytes", SearchOption.AllDirectories).Length;
        }

        private static string CreateTempDir(string name)
        {
            var root = Path.Combine(Path.GetTempPath(), "AzcelTests");
            var dir = Path.Combine(root, $"{name}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void SafeDeleteDir(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
                // 忽略清理失败
            }
        }

        private static void WriteSimpleXlsx(string path, string sheetName, string[][] rows)
        {
            if (File.Exists(path))
                File.Delete(path);

            using var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

            WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
            WriteEntry(archive, "_rels/.rels", BuildRootRelsXml());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbookXml(sheetName));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
            WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(rows));
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        private static string BuildContentTypesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                   "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                   "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                   "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                   "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                   "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                   "</Types>";
        }

        private static string BuildRootRelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                   "</Relationships>";
        }

        private static string BuildWorkbookXml(string sheetName)
        {
            var safeName = EscapeXml(sheetName ?? "Sheet1");
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                   "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                   $"<sheets><sheet name=\"{safeName}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                   "</workbook>";
        }

        private static string BuildWorkbookRelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                   "</Relationships>";
        }

        private static string BuildWorksheetXml(string[][] rows)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

            if (rows != null)
            {
                for (int r = 0; r < rows.Length; r++)
                {
                    var row = rows[r];
                    if (row == null)
                        continue;

                    var rowIndex = r + 1;
                    sb.Append($"<row r=\"{rowIndex}\">");
                    for (int c = 0; c < row.Length; c++)
                    {
                        var value = row[c];
                        if (value == null)
                            continue;

                        var cellRef = GetCellRef(c, rowIndex);
                        var safe = EscapeXml(value);
                        sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t>{safe}</t></is></c>");
                    }
                    sb.Append("</row>");
                }
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string GetCellRef(int columnIndex, int rowIndex)
        {
            return $"{GetColumnName(columnIndex)}{rowIndex}";
        }

        private static string GetColumnName(int index)
        {
            index += 1;
            var name = "";
            while (index > 0)
            {
                var rem = (index - 1) % 26;
                name = (char)('A' + rem) + name;
                index = (index - 1) / 26;
            }
            return name;
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private static void RegisterTablesByReflection(AzcelSystem azcel, string codeNamespace)
        {
            if (azcel == null)
                return;

            var tables = FindTableTypes(codeNamespace);
            foreach (var tableType in tables)
            {
                try
                {
                    var instance = Activator.CreateInstance(tableType) as IConfigTable;
                    if (instance != null)
                        azcel.RegisterTable(instance);
                }
                catch
                {
                    // 忽略无法创建的表
                }
            }
        }

        private static List<Type> FindTableTypes(string codeNamespace)
        {
            var results = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                if (types == null)
                    continue;

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                        continue;
                    if (!typeof(IConfigTable).IsAssignableFrom(type))
                        continue;
                    if (!IsInNamespace(type, codeNamespace))
                        continue;

                    results.Add(type);
                }
            }

            return results;
        }

        private static bool IsInNamespace(Type type, string codeNamespace)
        {
            if (string.IsNullOrEmpty(codeNamespace))
                return true;
            var ns = type.Namespace ?? string.Empty;
            return ns == codeNamespace || ns.StartsWith(codeNamespace + ".", StringComparison.Ordinal);
        }

        private static void ValidateTablesByReflection(AzcelSystem azcel, ConvertContext context)
        {
            var tables = azcel.GetAllTables().ToList();
            if (tables.Count == 0)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    Assert.Inconclusive("未加载到任何表（检测到编译中，请编译完成后重新运行测试）");

                Assert.Fail("未加载到任何表（请确认生成代码已编译且命名空间匹配）");
            }

            if (tables.All(t => t.Count == 0))
                Assert.Fail("表已注册但没有任何数据，请确认 .bytes 已生成且资源可加载");

            var tableMap = tables
                .Where(t => t != null && !string.IsNullOrEmpty(t.ConfigName))
                .ToDictionary(t => t.ConfigName, t => t, System.StringComparer.OrdinalIgnoreCase);

            foreach (var table in tables)
            {
                Assert.NotNull(table);
                Assert.NotNull(table.ConfigType);

                var def = context.Tables.FirstOrDefault(t =>
                    string.Equals(t.Name, table.ConfigName, System.StringComparison.OrdinalIgnoreCase));
                Assert.NotNull(def, $"未找到表定义: {table.ConfigName}");

                ValidateTableStructure(table, def, tableMap);
                ValidateTableApi(azcel, table, def);
            }
        }

        private static void ValidateTableValues(AzcelSystem azcel, ConvertContext context)
        {
            var enumMap = BuildEnumValueMap(context);
            foreach (var def in context.Tables)
            {
                var table = azcel.GetAllTables().FirstOrDefault(t =>
                    string.Equals(t.ConfigName, def.Name, StringComparison.OrdinalIgnoreCase));
                if (table == null)
                    continue;

                foreach (var row in def.Rows)
                {
                    var keyRaw = row.Values.TryGetValue(def.KeyField, out var keyValue) ? keyValue : "";
                    var keyObj = ParseValue(def.KeyType, keyRaw, def.ArraySeparator, def.ObjectSeparator, enumMap);
                    if (keyObj == null)
                        Assert.Fail($"无法解析主键: {def.Name}.{def.KeyField}={keyRaw}");

                    var config = GetConfigByKey(azcel, table, keyObj);
                    Assert.NotNull(config, $"未找到配置: {def.Name} key={keyRaw}");

                    foreach (var field in def.Fields)
                    {
                        if (field.Options.ContainsKey("skip"))
                            continue;

                        var raw = row.Values.TryGetValue(field.Name, out var v) ? v : "";
                        var normalized = NormalizeEnumValue(field.Type, raw, def.ArraySeparator, def.ObjectSeparator, enumMap);
                        var member = FindMember(config.GetType(), field.Name);
                        if (member == null)
                            continue;
                        var actual = GetMemberValue(config, member);

                        var expected = ParseValue(field.Type, normalized, def.ArraySeparator, def.ObjectSeparator, enumMap);
                        Assert.IsTrue(AreValuesEqual(field, expected, actual),
                            $"值不匹配: {def.Name}.{field.Name} 期望={FormatValue(expected)} 实际={FormatValue(actual)}");
                    }
                }
            }
        }

        private static void ValidateTableStructure(IConfigTable table, TableDefinition def,
            Dictionary<string, IConfigTable> tableMap)
        {
            var configType = table.ConfigType;
            foreach (var field in def.Fields)
            {
                if (field.Options.ContainsKey("skip"))
                    continue;

                var member = FindMember(configType, field.Name);
                Assert.NotNull(member, $"字段缺失: {configType.Name}.{field.Name}");

                var memberType = GetMemberType(member);
                Assert.NotNull(memberType, $"字段类型未知: {configType.Name}.{field.Name}");

                Assert.IsTrue(IsCompatibleFieldType(field, memberType, tableMap),
                    $"字段类型不匹配: {configType.Name}.{field.Name} -> {memberType.Name}");
            }
        }

        private static void ValidateTableApi(AzcelSystem azcel, IConfigTable table, TableDefinition def)
        {
            var all = table.GetAllCached();
            Assert.AreEqual(table.Count, all.Count);

            if (all.Count == 0)
                return;

            var first = all[0];
            Assert.NotNull(first);

            var key = GetIdValue(first);
            Assert.NotNull(key);

            var configType = table.ConfigType;
            var keyType = table.KeyType;

            var getTableMethod = FindGenericMethod(typeof(AzcelSystem), "GetTable", 2, 0);
            var typedTable = getTableMethod.MakeGenericMethod(configType, keyType).Invoke(azcel, null);
            Assert.NotNull(typedTable);

            var getConfigMethod = FindGenericMethod(typeof(AzcelSystem), "GetConfig", 2, 1);
            var config = getConfigMethod.MakeGenericMethod(configType, keyType).Invoke(azcel, new[] { key });
            Assert.NotNull(config);

            var tryGetMethod = FindGenericMethod(typeof(AzcelSystem), "TryGetConfig", 2, 2);
            var tryArgs = new object[] { key, null };
            var ok = (bool)tryGetMethod.MakeGenericMethod(configType, keyType).Invoke(azcel, tryArgs);
            Assert.IsTrue(ok);
            Assert.NotNull(tryArgs[1]);

            var getConfigObjMethod = FindGenericMethod(typeof(AzcelSystem), "GetConfig", 1, 1, typeof(object));
            var configObj = getConfigObjMethod.MakeGenericMethod(configType).Invoke(azcel, new[] { key });
            Assert.NotNull(configObj);

            var tryGetConfigObjMethod = FindGenericMethod(typeof(AzcelSystem), "TryGetConfig", 1, 2, typeof(object));
            var tryObjArgs = new object[] { key, null };
            var okObj = (bool)tryGetConfigObjMethod.MakeGenericMethod(configType).Invoke(azcel, tryObjArgs);
            Assert.IsTrue(okObj);
            Assert.NotNull(tryObjArgs[1]);

            var getAllMethod = FindGenericMethod(typeof(AzcelSystem), "GetAllConfig", 1, 0);
            var allTyped = getAllMethod.MakeGenericMethod(configType).Invoke(azcel, null);
            AssertNotEmpty(allTyped);

            var getAllTypedMethod = FindGenericMethod(typeof(AzcelSystem), "GetAllConfig", 2, 0);
            var allTypedNoAlloc = getAllTypedMethod.MakeGenericMethod(configType, keyType).Invoke(azcel, null);
            AssertNotEmpty(allTypedNoAlloc);

            var indexMembers = GetIndexMembers(configType);
            foreach (var (indexName, member) in indexMembers)
            {
                var value = GetMemberValue(first, member);
                var getByIndex = FindGenericMethod(typeof(AzcelSystem), "GetByIndex", 1, 2);
                var list = getByIndex.MakeGenericMethod(configType).Invoke(azcel, new[] { indexName, value }) as System.Collections.IEnumerable;
                AssertContains(list, first, $"{configType.Name}.{indexName}");

                var getByIndexTyped = FindGenericMethod(typeof(AzcelSystem), "GetByIndex", 2, 2);
                var listTyped = getByIndexTyped.MakeGenericMethod(configType, keyType).Invoke(azcel, new[] { indexName, value }) as System.Collections.IEnumerable;
                AssertContains(listTyped, first, $"{configType.Name}.{indexName}.NoAlloc");
            }

            var sampleField = def.Fields.FirstOrDefault(f => !f.Options.ContainsKey("skip"));
            if (sampleField != null)
            {
                var tryGetValueMethod = FindGenericMethod(typeof(ConfigBase), "TryGetValue", 1, 2);
                var args = new object[] { sampleField.Name, null };
                var okValue = (bool)tryGetValueMethod.MakeGenericMethod(typeof(object)).Invoke(first, args);
                Assert.IsTrue(okValue, $"ConfigBase.TryGetValue 失败: {configType.Name}.{sampleField.Name}");
            }

            var getTableByType = FindGenericMethod(typeof(AzcelSystem), "GetTable", 1, 0);
            var tableByType = getTableByType.MakeGenericMethod(table.GetType()).Invoke(azcel, null);
            Assert.NotNull(tableByType);

            var tryGetMissing = (bool)tryGetMethod.MakeGenericMethod(configType, keyType)
                .Invoke(azcel, new object[] { GetMissingKey(key), null });
            Assert.IsFalse(tryGetMissing);
        }

        private static System.Reflection.MethodInfo FindGenericMethod(Type type, string name, int genericArgs, int paramCount, Type firstParamType = null)
        {
            return type.GetMethods()
                .First(m =>
                    m.Name == name &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == genericArgs &&
                    m.GetParameters().Length == paramCount &&
                    (firstParamType == null || m.GetParameters()[0].ParameterType == firstParamType));
        }

        private static System.Reflection.MemberInfo FindMember(Type type, string name)
        {
            return type.GetMember(name, System.Reflection.BindingFlags.Instance |
                                        System.Reflection.BindingFlags.Public |
                                        System.Reflection.BindingFlags.NonPublic)
                .FirstOrDefault(m => m.MemberType == System.Reflection.MemberTypes.Field ||
                                     m.MemberType == System.Reflection.MemberTypes.Property);
        }

        private static Type GetMemberType(System.Reflection.MemberInfo member)
        {
            if (member is System.Reflection.FieldInfo field)
                return field.FieldType;
            if (member is System.Reflection.PropertyInfo prop)
                return prop.PropertyType;
            return null;
        }

        private static object GetMemberValue(object obj, System.Reflection.MemberInfo member)
        {
            if (member is System.Reflection.FieldInfo field)
                return field.GetValue(obj);
            if (member is System.Reflection.PropertyInfo prop)
                return prop.GetValue(obj);
            return null;
        }

        private static object GetIdValue(object config)
        {
            if (config == null)
                return null;
            var type = config.GetType();
            var member = FindMember(type, "Id");
            return member != null ? GetMemberValue(config, member) : null;
        }

        private static Dictionary<string, Dictionary<string, int>> BuildEnumValueMap(ConvertContext context)
        {
            var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var enumDef in context.Enums)
            {
                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var val in enumDef.Values)
                    map[val.Name] = val.Value;
                result[enumDef.Name] = map;
            }
            return result;
        }

        private static string NormalizeEnumValue(string type, string value, string arraySep, string objectSep,
            Dictionary<string, Dictionary<string, int>> enumValueMap)
        {
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(value))
                return value ?? "";

            arraySep ??= AzcelSettings.Instance?.arraySeparator ?? "|";
            objectSep ??= AzcelSettings.Instance?.objectSeparator ?? ",";

            if (type.EndsWith("[]", StringComparison.Ordinal))
            {
                var elementType = type[..^2];
                var parts = value.Split(arraySep[0]);
                for (int i = 0; i < parts.Length; i++)
                    parts[i] = NormalizeEnumValue(elementType, parts[i], arraySep, objectSep, enumValueMap);
                return string.Join(arraySep, parts);
            }

            if (type.StartsWith("#", StringComparison.Ordinal))
            {
                if (int.TryParse(value, out _))
                    return value;

                var enumName = type[1..];
                if (enumValueMap.TryGetValue(enumName, out var map) && map.TryGetValue(value, out var mapped))
                    return mapped.ToString();
            }

            return value ?? "";
        }

        private static object ParseValue(string type, string value, string arraySep, string objectSep,
            Dictionary<string, Dictionary<string, int>> enumValueMap)
        {
            if (string.IsNullOrEmpty(type))
                return value ?? "";

            arraySep ??= AzcelSettings.Instance?.arraySeparator ?? "|";
            objectSep ??= AzcelSettings.Instance?.objectSeparator ?? ",";

            var parser = TypeRegistry.Get(type);
            if (parser == null)
                return value ?? "";

            var separator = type.EndsWith("[]", StringComparison.Ordinal) ? arraySep : objectSep;
            return parser.Parse(value ?? "", separator);
        }

        private static object GetConfigByKey(AzcelSystem azcel, IConfigTable table, object keyObj)
        {
            if (azcel == null || table == null)
                return null;

            var method = FindGenericMethod(typeof(AzcelSystem), "GetConfig", 2, 1);
            var generic = method.MakeGenericMethod(table.ConfigType, table.KeyType);
            return generic.Invoke(azcel, new[] { keyObj });
        }

        private static bool AreValuesEqual(FieldDefinition field, object expected, object actual)
        {
            if (expected == null && actual == null)
                return true;

            if (field != null && field.IsTableRef)
            {
                if (IsDefaultOrEmpty(expected) && actual == null)
                    return true;

                var expectedKey = ConvertToComparable(expected);
                var actualKey = ConvertToComparable(actual);
                return Equals(expectedKey, actualKey);
            }

            if (field != null && field.IsEnumRef)
            {
                var expectedKey = ConvertToComparable(expected);
                var actualKey = ConvertToComparable(actual);
                return Equals(expectedKey, actualKey);
            }

            var expectedArray = expected as Array;
            var actualArray = actual as Array;
            if (expectedArray == null && expected is System.Collections.IList listExpected)
                expectedArray = listExpected.Cast<object>().ToArray();
            if (actualArray == null && actual is System.Collections.IList listActual)
                actualArray = listActual.Cast<object>().ToArray();
            if (expectedArray != null || actualArray != null)
            {
                if (expectedArray == null || actualArray == null)
                    return false;
                return CompareArrays(expectedArray, actualArray);
            }

            if (expected is float ef && actual is float af)
                return Math.Abs(ef - af) < 0.0001f;
            if (expected is double ed && actual is double ad)
                return Math.Abs(ed - ad) < 0.0001d;

            if (expected is string es && actual == null)
                return es == string.Empty;

            return Equals(ConvertToComparable(expected), ConvertToComparable(actual));
        }

        private static bool IsDefaultOrEmpty(object value)
        {
            if (value == null)
                return true;
            if (value is string s)
                return string.IsNullOrEmpty(s);
            var type = value.GetType();
            if (type.IsValueType)
            {
                var def = Activator.CreateInstance(type);
                return Equals(def, value);
            }
            return false;
        }

        private static bool CompareArrays(Array expected, Array actual)
        {
            if (expected == null && actual == null)
                return true;
            if (expected == null || actual == null)
                return false;
            if (expected.Length != actual.Length)
                return false;

            for (int i = 0; i < expected.Length; i++)
            {
                var left = ConvertToComparable(expected.GetValue(i));
                var right = ConvertToComparable(actual.GetValue(i));
                if (!Equals(left, right))
                    return false;
            }

            return true;
        }

        private static object ConvertToComparable(object value)
        {
            if (value == null)
                return null;

            if (value is ConfigBase config)
                return GetIdValue(config);

            var type = value.GetType();
            if (type.IsEnum)
                return Convert.ToInt32(value);

            return value;
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return "null";
            if (value is Array array)
            {
                var parts = new string[array.Length];
                for (int i = 0; i < array.Length; i++)
                    parts[i] = array.GetValue(i)?.ToString() ?? "null";
                return $"[{string.Join(",", parts)}]";
            }
            return value.ToString();
        }

        private static object GetMissingKey(object key)
        {
            if (key == null)
                return null;
            var type = key.GetType();
            if (type == typeof(int))
                return (int)key + 1000000;
            if (type == typeof(long))
                return (long)key + 1000000;
            if (type == typeof(string))
                return key + "__missing__";
            if (type.IsEnum)
                return Activator.CreateInstance(type);
            return key;
        }

        private static List<(string name, System.Reflection.MemberInfo member)> GetIndexMembers(Type type)
        {
            var members = type.GetMembers(System.Reflection.BindingFlags.Instance |
                                          System.Reflection.BindingFlags.Public |
                                          System.Reflection.BindingFlags.NonPublic);
            var result = new List<(string, System.Reflection.MemberInfo)>();
            foreach (var member in members)
            {
                if (member.MemberType != System.Reflection.MemberTypes.Field &&
                    member.MemberType != System.Reflection.MemberTypes.Property)
                    continue;

                var attr = member.GetCustomAttribute<ConfigIndexAttribute>();
                if (attr == null)
                    continue;

                var name = string.IsNullOrEmpty(attr.Name) ? member.Name : attr.Name;
                result.Add((name, member));
            }

            return result;
        }

        private static bool IsCompatibleFieldType(FieldDefinition field, Type memberType,
            Dictionary<string, IConfigTable> tableMap)
        {
            if (field.IsTableRef)
            {
                if (tableMap.TryGetValue(field.RefTableName, out var refTable))
                {
                    if (memberType == refTable.ConfigType || memberType == refTable.KeyType)
                        return true;
                    if (memberType.IsArray)
                    {
                        var elem = memberType.GetElementType();
                        if (elem == refTable.ConfigType || elem == refTable.KeyType)
                            return true;
                    }
                }

                return memberType == typeof(int) || memberType == typeof(long);
            }

            if (field.IsEnumRef)
                return memberType.IsEnum || memberType == typeof(int);

            var parser = TypeRegistry.Get(field.Type);
            var expected = ResolveTypeByName(parser?.CSharpTypeName ?? field.Type);
            if (expected == null)
                return true;

            if (expected.IsAssignableFrom(memberType) || memberType.IsAssignableFrom(expected))
                return true;

            if (expected.IsArray && memberType.IsArray)
                return expected.GetElementType() == memberType.GetElementType();

            return false;
        }

        private static Type ResolveTypeByName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            if (typeName.EndsWith("[]"))
            {
                var element = ResolveTypeByName(typeName[..^2]);
                return element?.MakeArrayType();
            }

            switch (typeName)
            {
                case "int": return typeof(int);
                case "long": return typeof(long);
                case "float": return typeof(float);
                case "double": return typeof(double);
                case "bool": return typeof(bool);
                case "string": return typeof(string);
            }

            var direct = Type.GetType(typeName);
            if (direct != null)
                return direct;

            var matchNameOnly = !typeName.Contains(".");
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var found = asm.GetType(typeName);
                if (found != null)
                    return found;

                if (!matchNameOnly)
                    continue;

                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                if (types == null)
                    continue;

                foreach (var t in types)
                {
                    if (t != null && string.Equals(t.Name, typeName, StringComparison.Ordinal))
                        return t;
                }
            }

            return null;
        }

        private static void AssertNotEmpty(object list)
        {
            Assert.NotNull(list);
            if (list is System.Collections.ICollection collection)
                Assert.IsTrue(collection.Count > 0);
        }

        private static void AssertContains(System.Collections.IEnumerable list, object expected, string label)
        {
            if (list == null)
                Assert.Fail($"索引结果为空: {label}");

            foreach (var item in list)
            {
                if (ReferenceEquals(item, expected))
                    return;
            }

            Assert.Fail($"索引结果未包含目标项: {label}");
        }
    }

    internal static class AzcelSampleEnvironment
    {
        private const string WorkbookName = "AzcelTestWorkbook";

        public static string ExcelDirectory
        {
            get
            {
                var guids = AssetDatabase.FindAssets(WorkbookName);
                if (guids == null || guids.Length == 0)
                    Assert.Fail($"未找到测试工作簿：{WorkbookName}.xlsx，请先导入 Azcel Tests Sample");

                var scriptGuid = AssetDatabase.FindAssets(nameof(AzcelFullFlowTests) + " t:MonoScript").FirstOrDefault();
                var scriptPath = string.IsNullOrEmpty(scriptGuid) ? null : AssetDatabase.GUIDToAssetPath(scriptGuid);
                var testsRoot = "";
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    var editorDir = Path.GetDirectoryName(scriptPath);
                    var rootDir = string.IsNullOrEmpty(editorDir) ? null : Directory.GetParent(editorDir)?.FullName;
                    if (!string.IsNullOrEmpty(rootDir))
                        testsRoot = rootDir.Replace("\\", "/");
                }

                var paths = guids.Select(AssetDatabase.GUIDToAssetPath).ToList();
                var path = "";
                if (!string.IsNullOrEmpty(testsRoot))
                    path = paths.FirstOrDefault(p => p.Replace("\\", "/").StartsWith(testsRoot, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(path))
                    path = paths[0];

                var dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir))
                    Assert.Fail($"测试工作簿路径无效：{path}");
                return dir.Replace("\\", "/");
            }
        }
    }
}
