#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// net48 缺少 init / record 所需的编译器标记类型（wiki 09 §5.1）。
    /// App 侧也要用 with 表达式改写 AppSettings，因此同样需要垫片。
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
#endif
