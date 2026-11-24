using System;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.Core.Associations
{
    /// <summary>
    /// 通知资源管理器刷新关联（wiki 06 §2）。
    /// 不刷新时，注册表已改但图标/双击行为可能仍是旧缓存。
    /// </summary>
    public static class ShellNotify
    {
        public static void NotifyAssociationsChanged()
        {
            try
            {
                NativeMethods.SHChangeNotify(
                    NativeMethods.SHCNE_ASSOCCHANGED,
                    NativeMethods.SHCNF_IDLIST,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }
            catch (DllNotFoundException)
            {
                // 极端环境（非完整桌面）下忽略
            }
            catch (EntryPointNotFoundException)
            {
            }
        }
    }
}
