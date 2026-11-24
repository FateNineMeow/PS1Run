#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// net48 缺少 IsExternalInit，record / init 所需的编译器垫片（wiki 09 §3.4 实测）。
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
#endif
