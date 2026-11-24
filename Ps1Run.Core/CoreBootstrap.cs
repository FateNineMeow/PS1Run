using System.Text;
using System.Threading;

namespace Ps1Run.Core
{
    /// <summary>
    /// 进程级一次性初始化。
    /// net11 不含 GBK(936)，必须注册代码页提供程序；net48 内置，无需处理。
    /// （wiki 07 §4.1、09 §4.5 实测：net11 下 GetEncoding(936) 会抛 NotSupportedException）
    /// </summary>
    public static class CoreBootstrap
    {
        private static int _initialized;

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 1)
            {
                return;
            }

#if !NETFRAMEWORK
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
        }
    }
}
