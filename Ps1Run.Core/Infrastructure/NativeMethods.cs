using System;
using System.Runtime.InteropServices;

namespace Ps1Run.Core.Infrastructure
{
    /// <summary>
    /// Win32 互操作（wiki 06 §4、02 F1.7 / F2.4）。
    /// 骨架阶段只声明本次需要的最小集合，均使用 DllImport 以兼容 net48 与 net11。
    /// </summary>
    public static class NativeMethods
    {
        // ---- SHOpenWithDialog ----
        public const uint OAIF_ALLOW_REGISTRATION = 0x00000001;
        public const uint OAIF_REGISTER_EXT = 0x00000002;

        // ---- SHChangeNotify ----
        public const int SHCNE_ASSOCCHANGED = 0x08000000;
        public const uint SHCNF_IDLIST = 0x0000;

        // ---- MonitorFromPoint ----
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OPENASINFO
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? pcszFile;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string? pcszClass;

            public uint oaifInFlags;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHOpenWithDialog(IntPtr hwndParent, ref OPENASINFO poainfo);

        [DllImport("shell32.dll")]
        public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    }
}
