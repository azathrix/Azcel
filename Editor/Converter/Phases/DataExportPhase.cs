using System.IO;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;
using UnityEditor;

namespace Azcel.Editor
{
    /// <summary>
    /// 数据导出阶段
    /// </summary>
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
            var outputPath = settings.dataOutputPath;

            // 确保输出目录存在
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            if (settings.dataFormat == DataFormat.Binary)
            {
                ExportBinary(context, outputPath);
            }
            else
            {
                ExportJson(context, outputPath);
            }

            AssetDatabase.Refresh();
            Log.Info($"[DataExport] 数据导出完成");
            return UniTask.CompletedTask;
        }

        private void ExportBinary(ConvertContext context, string outputPath)
        {
            foreach (var table in context.Tables)
            {
                var filePath = Path.Combine(outputPath, $"{table.Name}.bytes");
                using var stream = File.Create(filePath);
                using var writer = new BinaryWriter(stream);

                // 写入行数
                writer.Write(table.Rows.Count);

                // 写入每行数据
                foreach (var row in table.Rows)
                {
                    foreach (var field in table.Fields)
                    {
                        var value = row.Values.TryGetValue(field.Name, out var v) ? v : "";
                        WriteValue(writer, field.Type, value, table.ArraySeparator, table.ObjectSeparator);
                    }
                }

                Log.Info($"[DataExport] 导出二进制: {table.Name}");
            }

            // 导出全局配置
            foreach (var global in context.Globals)
            {
                var filePath = Path.Combine(outputPath, $"GlobalConfig{global.Name}.bytes");
                using var stream = File.Create(filePath);
                using var writer = new BinaryWriter(stream);

                writer.Write(global.Values.Count);
                foreach (var value in global.Values)
                {
                    writer.Write(value.Key);
                    writer.Write(value.Type);
                    writer.Write(value.Value);
                }

                Log.Info($"[DataExport] 导出二进制: GlobalConfig{global.Name}");
            }
        }

        private void ExportJson(ConvertContext context, string outputPath)
        {
            foreach (var table in context.Tables)
            {
                var filePath = Path.Combine(outputPath, $"{table.Name}.json");
                var json = UnityEngine.JsonUtility.ToJson(new { rows = table.Rows }, true);
                File.WriteAllText(filePath, json);
                Log.Info($"[DataExport] 导出JSON: {table.Name}");
            }

            foreach (var global in context.Globals)
            {
                var filePath = Path.Combine(outputPath, $"GlobalConfig{global.Name}.json");
                var json = UnityEngine.JsonUtility.ToJson(new { values = global.Values }, true);
                File.WriteAllText(filePath, json);
                Log.Info($"[DataExport] 导出JSON: GlobalConfig{global.Name}");
            }
        }

        private void WriteValue(BinaryWriter writer, string type, string value, string arraySep, string objectSep)
        {
            var parser = TypeRegistry.Get(type);
            if (parser == null)
            {
                writer.Write(value ?? "");
                return;
            }

            // 简化实现，实际应该根据类型写入
            switch (type.ToLower())
            {
                case "int":
                    writer.Write(int.TryParse(value, out var i) ? i : 0);
                    break;
                case "long":
                    writer.Write(long.TryParse(value, out var l) ? l : 0L);
                    break;
                case "float":
                    writer.Write(float.TryParse(value, out var f) ? f : 0f);
                    break;
                case "double":
                    writer.Write(double.TryParse(value, out var d) ? d : 0d);
                    break;
                case "bool":
                    writer.Write(value == "1" || value?.ToLower() == "true");
                    break;
                default:
                    writer.Write(value ?? "");
                    break;
            }
        }
    }
}
