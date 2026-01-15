using Azathrix.Framework.Core.Pipeline;

namespace Azcel.Editor
{
    /// <summary>
    /// 转换钩子接口
    /// </summary>
    public interface IConvertHook : IHook<ConvertContext>
    {
    }

    /// <summary>
    /// 阶段执行前钩子
    /// </summary>
    public interface IBeforeConvertPhaseHook : IBeforePhaseHook<ConvertContext>
    {
    }

    /// <summary>
    /// 阶段执行后钩子
    /// </summary>
    public interface IAfterConvertPhaseHook : IAfterPhaseHook<ConvertContext>
    {
    }
}
