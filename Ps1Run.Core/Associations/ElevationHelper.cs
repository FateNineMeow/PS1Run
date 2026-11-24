using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Ps1Run.Core.Associations
{
    /// <summary>
    /// UAC 提升（wiki 02 F1.9，可选功能）。
    /// 本机实测 HKLM\Software\Classes 写入被拒，机器级注册必须提升权限。
    /// </summary>
    public static class ElevationHelper
    {
        /// <summary>
        /// 以管理员身份重新启动当前程序。
        /// 返回 false 表示用户取消了 UAC 或被系统拒绝。
        /// </summary>
        public static bool TryRestartElevated(string arguments, string? executablePath = null)
        {
            var exe = executablePath;
            if (string.IsNullOrWhiteSpace(exe))
            {
                var process = Process.GetCurrentProcess();
                exe = process.MainModule?.FileName;
            }

            if (string.IsNullOrWhiteSpace(exe))
            {
                return false;
            }

            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true,
                    Verb = "runas",
                };

                Process.Start(info);
                return true;
            }
            catch (Win32Exception)
            {
                // 1223 = 用户在 UAC 对话框点了“否”
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
