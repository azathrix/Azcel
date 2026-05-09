using System.Linq;
using Azathrix.Framework.Core.Pipeline;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azcel.Editor
{
    /// <summary>
    /// 引用解析阶段 - 验证表引用和枚举引用
    /// </summary>
    [Register]
    [PhaseId("ReferenceResolve")]
    public class ReferenceResolvePhase : IConvertPhase
    {
        public int Order => 300;

        public UniTask ExecuteAsync(ConvertContext context)
        {
            foreach (var table in context.Tables)
            {
                foreach (var field in table.Fields)
                {
                    // 验证表引用
                    if (field.IsTableRef)
                    {
                        var refTable = context.Tables.FirstOrDefault(t => t.Name == field.RefTableName);
                        if (refTable == null)
                        {
                            context.AddError($"表 {table.Name} 的字段 {field.Name} 引用的表 {field.RefTableName} 不存在");
                        }
                        else
                        {
                            field.RefKeyType = refTable.KeyType;
                        }
                    }

                    // 验证枚举引用
                    if (field.IsEnumRef)
                    {
                        var refEnum = context.Enums.FirstOrDefault(e => e.Name == field.RefEnumName);
                        if (refEnum == null)
                        {
                            context.AddError($"表 {table.Name} 的字段 {field.Name} 引用的枚举 {field.RefEnumName} 不存在");
                        }
                    }
                }
            }

            foreach (var global in context.Globals)
            {
                foreach (var value in global.Values)
                {
                    if (value == null || string.IsNullOrEmpty(value.Type) || !value.Type.StartsWith("@"))
                        continue;

                    var refTableName = value.Type[1..];
                    var refTable = context.Tables.FirstOrDefault(t => t.Name == refTableName);
                    if (refTable == null)
                    {
                        context.AddError($"全局 {global.Name} 的键 {value.Key} 引用的表 {refTableName} 不存在");
                    }
                    else
                    {
                        value.RefKeyType = refTable.KeyType;
                    }
                }
            }

            Log.Info("[ReferenceResolve] 引用验证完成");
            return UniTask.CompletedTask;
        }
    }
}
