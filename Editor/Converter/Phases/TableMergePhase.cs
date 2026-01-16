using System.Collections.Generic;
using System.Linq;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azcel.Editor
{
    /// <summary>
    /// 表合并阶段 - 合并同名表
    /// </summary>
    [PhaseId("TableMerge")]
    [Register]
    public class TableMergePhase : IConvertPhase
    {
        public int Order => 200;

        public UniTask ExecuteAsync(ConvertContext context)
        {
            // 按表名分组
            var groups = context.Tables.GroupBy(t => t.Name).ToList();
            var mergedTables = new List<TableDefinition>();

            foreach (var group in groups)
            {
                var tables = group.ToList();
                if (tables.Count == 1)
                {
                    mergedTables.Add(tables[0]);
                    continue;
                }

                // 合并多个同名表
                var merged = tables[0];
                for (int i = 1; i < tables.Count; i++)
                {
                    var other = tables[i];

                    // 合并行数据
                    foreach (var row in other.Rows)
                    {
                        merged.Rows.Add(row);
                    }

                    Log.Info($"[TableMerge] 合并表 {merged.Name}: {other.ExcelPath}");
                }

                mergedTables.Add(merged);
            }

            context.Tables.Clear();
            context.Tables.AddRange(mergedTables);

            Log.Info($"[TableMerge] 合并完成: {context.Tables.Count} 表");
            return UniTask.CompletedTask;
        }
    }
}
