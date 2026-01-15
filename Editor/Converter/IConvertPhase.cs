using Azathrix.Framework.Core.Pipeline;
using Cysharp.Threading.Tasks;

namespace Azcel.Editor
{
    /// <summary>
    /// 转换阶段接口
    /// </summary>
    public interface IConvertPhase : IPhase<ConvertContext>
    {
    }
}
