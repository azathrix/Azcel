using System.Linq;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azcel.Editor
{
    /// <summary>
    /// 表继承阶段 - 处理表继承
    /// </summary>
    [PhaseId("Inheritance")]
    public class InheritancePhase : IConvertPhase
    {
        public int Order => 250;

        public UniTask ExecuteAsync(ConvertContext context)
        {
            foreach (var table in context.Tables)
            {
                if (string.IsNullOrEmpty(table.ParentTable))
                    continue;

                var parent = context.Tables.FirstOrDefault(t => t.Name == table.ParentTable);
                if (parent == null)
                {
                    context.AddWarning($"表 {table.Name} 的父表 {table.ParentTable} 不存在");
                    continue;
                }

                // 继承父表字段（插入到开头）
                var inheritedFields = parent.Fields
                    .Where(f => !table.Fields.Any(tf => tf.Name == f.Name))
                    .Select(f => new FieldDefinition
                    {
                        Name = f.Name,
                        Type = f.Type,
                        Comment = f.Comment,
                        IsKey = f.IsKey,
                        IsIndex = f.IsIndex,
                        IsTableRef = f.IsTableRef,
                        RefTableName = f.RefTableName,
                        IsEnumRef = f.IsEnumRef,
                        RefEnumName = f.RefEnumName
                    })
                    .ToList();

                table.Fields.InsertRange(0, inheritedFields);

                // 继承主键配置
                if (table.KeyField == "Id" && parent.KeyField != "Id")
                {
                    table.KeyField = parent.KeyField;
                    table.KeyType = parent.KeyType;
                }

                Log.Info($"[Inheritance] 表 {table.Name} 继承自 {parent.Name}, 继承 {inheritedFields.Count} 个字段");
            }

            return UniTask.CompletedTask;
        }
    }
}
