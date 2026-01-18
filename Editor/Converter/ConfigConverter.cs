using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Azathrix.Framework.Core;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;
using UnityEditor;

namespace Azcel.Editor
{
    /// <summary>
    /// 配置转换器
    /// </summary>
    [Register]
    [PipelineId("Azcel.Converter")]
    [PipelineDisplayName("Azcel配置转换")]
    public class ConfigConverter : PipelineBase<IConvertPhase, ConvertContext>
    {
        private static bool _reloadScheduled;

        // public ConfigConverter()
        // {
        //     // 添加默认阶段
        //     AddPhase(new ExcelParsePhase());
        //     AddPhase(new TableMergePhase());
        //     AddPhase(new InheritancePhase());
        //     AddPhase(new ReferenceResolvePhase());
        //     AddPhase(new CodeGenPhase());
        //     AddPhase(new DataExportPhase());
        // }

        public override async UniTask ExecuteAsync(ConvertContext context)
        {
            Log.Info("[Azcel] 开始配置转换...");
            var totalWatch = Stopwatch.StartNew();

            PrepareTempWorkspace(context);
            if (context.Errors.Count > 0)
            {
                Log.Error("[Azcel] 预处理失败，已终止转换。");
                return;
            }

            await base.ExecuteAsync(context);

            if (context.Warnings.Count > 0)
            {
                Log.Warning($"[Azcel] 有 {context.Warnings.Count} 个警告:");
                foreach (var warning in context.Warnings)
                    Log.Warning($"  - {warning}");
            }
            
            if (context.Errors.Count > 0)
            {
                Log.Error($"[Azcel] 转换失败，有 {context.Errors.Count} 个错误:");
                foreach (var error in context.Errors)
                    Log.Error($"  - {error}");
            }
            else
            {
                ApplyTempOutputs(context);
                
                LogPerformanceReport(context, totalWatch.ElapsedMilliseconds);
                
                if (!context.SkipAssetRefresh)
                    AssetDatabase.Refresh();
                ScheduleEditorReload();
                Log.Info(
                    $"[Azcel] 转换完成! 表: {context.Tables.Count}, 枚举: {context.Enums.Count}, 全局配置: {context.Globals.Count}");
            }

            totalWatch.Stop();
        }

        private static void LogPerformanceReport(ConvertContext context, long totalMilliseconds)
        {
            if ((context?.PerfRecords?.Count ?? 0) == 0 && (context?.PhaseTotals?.Count ?? 0) == 0)
                return;


            LogConfigDetails(context);

            LogPhaseSummary(context, "CodeGen", "代码生成");
            LogPhaseSummary(context, "DataExport", "数据导出");
            LogDataSummary(context);


            var totalConfigs = context.Tables.Count + context.Enums.Count + context.Globals.Count;
            Log.Info($"[Azcel] 性能报告：总配置数量 {totalConfigs}，总耗时 {totalMilliseconds}ms", colorStyle: 3);
        }

        private static void LogPhaseSummary(ConvertContext context, string phase, string display)
        {
            var records = context.PerfRecords;
            var total = 0;
            var tableCount = 0;
            var enumCount = 0;
            var globalCount = 0;
            var totalRows = 0;
            var totalMs = 0L;
            ConvertContext.PerfRecord slowest = null;

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (!string.Equals(record.Phase, phase, StringComparison.OrdinalIgnoreCase))
                    continue;

                total++;
                totalRows += record.Rows;
                totalMs += record.Milliseconds;

                switch (record.Kind)
                {
                    case "Table":
                        tableCount++;
                        break;
                    case "Enum":
                        enumCount++;
                        break;
                    case "Global":
                        globalCount++;
                        break;
                }

                if (slowest == null || record.Milliseconds > slowest.Milliseconds)
                    slowest = record;
            }

            if (total == 0 && !context.PhaseTotals.ContainsKey(phase))
                return;

            if (context.PhaseTotals.TryGetValue(phase, out var phaseMs))
                totalMs = phaseMs;

            Log.Info(
                $"[Azcel] {display}：配置数 {total} (表{tableCount}/枚举{enumCount}/全局{globalCount})，总行数 {totalRows}，总耗时 {totalMs}ms",
                colorStyle: 4);

            if (slowest != null)
                Log.Info(
                    $"[Azcel] {display} 最慢项：{slowest.Kind}:{slowest.Name}，行数 {slowest.Rows}，耗时 {slowest.Milliseconds}ms",
                    colorStyle: 5);
        }

        private static void LogConfigDetails(ConvertContext context)
        {
            var records = context?.PerfRecords;
            if (records == null || records.Count == 0)
                return;

            var hasDataExport = false;
            for (int i = 0; i < records.Count; i++)
            {
                if (string.Equals(records[i].Phase, "DataExport", StringComparison.OrdinalIgnoreCase))
                {
                    hasDataExport = true;
                    break;
                }
            }

            var map = new Dictionary<string, ConvertContext.PerfRecord>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var isData = string.Equals(record.Phase, "DataExport", StringComparison.OrdinalIgnoreCase);
                var isEnum = string.Equals(record.Kind, "Enum", StringComparison.OrdinalIgnoreCase);
                if (hasDataExport)
                {
                    if (!isData && !isEnum)
                        continue;
                    if (!isData && isEnum &&
                        !string.Equals(record.Phase, "CodeGen", StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                else
                {
                    if (!string.Equals(record.Phase, "CodeGen", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var key = $"{record.Kind}:{record.Name}";
                if (!map.ContainsKey(key))
                    map[key] = record;
            }

            if (map.Count == 0)
                return;

            var list = new List<ConvertContext.PerfRecord>(map.Values);
            list.Sort((a, b) =>
            {
                var kind = string.Compare(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase);
                if (kind != 0)
                    return kind;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < list.Count; i++)
            {
                var record = list[i];
                var sizeText = "";
                if (context.DataSizes.TryGetValue(record.Name, out var bytes))
                    sizeText = $", {FormatBytes(bytes)}";
                Log.Info($"[Azcel] 配置 {record.Name} -> {record.Rows}条{sizeText}", colorStyle: 2);
            }
        }

        private static void LogDataSummary(ConvertContext context)
        {
            if (context?.DataSizes == null || context.DataSizes.Count == 0)
                return;

            long total = 0;
            foreach (var pair in context.DataSizes)
                total += pair.Value;

            Log.Info($"[Azcel] 数据总大小：{FormatBytes(total)}", colorStyle: 3);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024f:0.##} KB";
            if (bytes < 1024L * 1024 * 1024)
                return $"{bytes / 1024f / 1024f:0.##} MB";
            return $"{bytes / 1024f / 1024f / 1024f:0.##} GB";
        }

        private static void PrepareTempWorkspace(ConvertContext context)
        {
            var settings = AzcelSettings.Instance;
            var tempRoot = Path.Combine("Library", "AzcelTemp", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            var tempExcel = Path.Combine(tempRoot, "Excel");
            var tempCode = Path.Combine(tempRoot, "Code");
            var tempData = Path.Combine(tempRoot, "Data");

            Directory.CreateDirectory(tempExcel);
            Directory.CreateDirectory(tempCode);
            Directory.CreateDirectory(tempData);

            context.TempExcelPath = tempExcel;
            context.TempCodeOutputPath = tempCode;
            context.TempDataOutputPath = tempData;

            CleanupOldTempRoots(keepCount: 3);

            var copied = CopyExcelToTemp(settings.excelPaths, tempExcel, context);
            if (!copied)
            {
                context.AddError("[Azcel] Excel 复制失败，已终止转换。");
            }
        }

        private static void CleanupOldTempRoots(int keepCount)
        {
            if (keepCount < 0)
                keepCount = 0;

            var root = Path.Combine("Library", "AzcelTemp");
            if (!Directory.Exists(root))
                return;

            var dirs = Directory.GetDirectories(root);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(dirs);

            for (int i = keepCount; i < dirs.Length; i++)
            {
                try
                {
                    Directory.Delete(dirs[i], true);
                }
                catch
                {
                    // 忽略清理失败
                }
            }
        }

        private static bool CopyExcelToTemp(IEnumerable<string> excelRoots, string tempExcelRoot,
            ConvertContext context)
        {
            var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var anyCopied = false;

            foreach (var root in excelRoots)
            {
                if (!Directory.Exists(root))
                {
                    context.AddWarning($"Excel目录不存在: {root}");
                    continue;
                }

                var files = Directory.GetFiles(root, "*.xlsx", SearchOption.AllDirectories);
                var xlsFiles = Directory.GetFiles(root, "*.xls", SearchOption.AllDirectories);
                foreach (var file in files)
                    anyCopied |= CopyOne(file, tempExcelRoot, nameCounts, context);
                foreach (var file in xlsFiles)
                    anyCopied |= CopyOne(file, tempExcelRoot, nameCounts, context);
            }

            return anyCopied && context.Errors.Count == 0;
        }

        private static bool CopyOne(string file, string tempRoot, Dictionary<string, int> nameCounts,
            ConvertContext context)
        {
            var fileName = Path.GetFileName(file);
            if (string.IsNullOrEmpty(fileName))
                return false;

            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            if (!nameCounts.TryGetValue(fileName, out var count))
                count = 0;

            string targetName;
            do
            {
                targetName = count == 0 ? fileName : $"{name}_{count}{ext}";
                count++;
            } while (File.Exists(Path.Combine(tempRoot, targetName)));

            nameCounts[fileName] = count;
            var targetPath = Path.Combine(tempRoot, targetName);

            try
            {
                File.Copy(file, targetPath, true);
                if (context != null)
                {
                    var tempFull = Path.GetFullPath(targetPath);
                    var originFull = Path.GetFullPath(file);
                    context.TempExcelPathMap[tempFull] = originFull;
                }
                return true;
            }
            catch (Exception e)
            {
                context.AddError($"复制Excel失败: {file} -> {targetPath}，{e.Message}");
                return false;
            }
        }

        private static void ApplyTempOutputs(ConvertContext context)
        {
            if (string.IsNullOrEmpty(context.TempCodeOutputPath) || string.IsNullOrEmpty(context.TempDataOutputPath))
                return;

            var settings = AzcelSettings.Instance;
            CopyDirectory(context.TempCodeOutputPath, settings.codeOutputPath);
            CopyDirectory(context.TempDataOutputPath, settings.dataOutputPath);

            var legacyRegister = Path.Combine(settings.codeOutputPath, "ConfigRegister.cs");
            if (File.Exists(legacyRegister))
                File.Delete(legacyRegister);
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir))
                return;

            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, file);
                var destPath = Path.Combine(targetDir, relative);
                var destFolder = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destFolder))
                    Directory.CreateDirectory(destFolder);
                File.Copy(file, destPath, true);
            }
        }

        private static void ScheduleEditorReload()
        {
            if (_reloadScheduled)
                return;

            _reloadScheduled = true;
            EditorApplication.delayCall += ReloadEditorData;
        }

        private static void ReloadEditorData()
        {
            _reloadScheduled = false;
            EditorApplication.delayCall -= ReloadEditorData;

            if (!AzathrixFramework.HasSystem<AzcelSystem>())
            {
                Log.Warning("[Azcel][Editor] AzcelSystem 未注册，跳过自动加载");
                return;
            }

            var azcel = AzathrixFramework.GetSystem<AzcelSystem>();
            if (azcel == null)
            {
                Log.Warning("[Azcel][Editor] AzcelSystem 未创建，跳过自动加载");
                return;
            }

            azcel.Clear();

            if (azcel.DataLoader == null)
                azcel.SetDataLoader(new ResourcesDataLoader());

            azcel.LoadTableRegistry();

            if (azcel.TableLoader == null)
                azcel.SetTableLoader(BinaryConfigTableLoader.Instance);

            var loader = azcel.DataLoader;
            foreach (var table in azcel.GetAllTables())
            {
                var data = loader.Load(table.ConfigName);
                if (data != null)
                    azcel.LoadTable(table, data);
                else
                    Log.Warning($"[Azcel][Editor] 表数据缺失: {table.ConfigName}");
            }
        }
    }
}
