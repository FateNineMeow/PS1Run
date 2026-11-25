using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.App.Services
{
    /// <summary>
    /// 选择窗定位：鼠标所在显示器的居中位置（wiki 05 §5、02 F2.4）。
    /// Win32 返回的是物理像素，WPF 的 Left/Top 是 DIP，需要按当前 DPI 换算。
    /// </summary>
    public static class PickerPlacement
    {
        private const double FallbackWidth = 460;
        private const double FallbackHeight = 260;

        /// <summary>
        /// 在窗口首次显示前先按主显示器工作区居中，避免从 (0,0) 闪现到目标位置。
        /// SystemParameters.WorkArea 已是 DIP，无需再做 DPI 换算。
        /// </summary>
        public static void CenterOnPrimaryMonitor(Window window)
        {
            if (window is null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            var work = SystemParameters.WorkArea;
            var width = window.ActualWidth > 0 ? window.ActualWidth : SafeWidth(window.Width);
            var height = window.ActualHeight > 0 ? window.ActualHeight : SafeHeight(window.Height);

            window.Left = work.Left + Math.Max(0, (work.Width - width) / 2);
            window.Top = work.Top + Math.Max(0, (work.Height - height) / 2);
        }

        public static void CenterOnCursorMonitor(Window window)
        {
            if (window is null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (!NativeMethods.GetCursorPos(out var point))
            {
                return;
            }

            var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO
            {
                cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO)),
            };

            if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                return;
            }

            double scaleX = 1.0;
            double scaleY = 1.0;
            var source = PresentationSource.FromVisual(window);
            if (source?.CompositionTarget is not null)
            {
                var fromDevice = source.CompositionTarget.TransformFromDevice;
                scaleX = fromDevice.M11;
                scaleY = fromDevice.M22;
            }

            var workLeft = info.rcWork.Left * scaleX;
            var workTop = info.rcWork.Top * scaleY;
            var workWidth = (info.rcWork.Right - info.rcWork.Left) * scaleX;
            var workHeight = (info.rcWork.Bottom - info.rcWork.Top) * scaleY;

            var width = window.ActualWidth > 0 ? window.ActualWidth : SafeWidth(window.Width);
            var height = window.ActualHeight > 0 ? window.ActualHeight : SafeHeight(window.Height);

            window.Left = workLeft + Math.Max(0, (workWidth - width) / 2);
            window.Top = workTop + Math.Max(0, (workHeight - height) / 2);
        }

        private static double SafeWidth(double value)
        {
            return double.IsNaN(value) || value <= 0 ? FallbackWidth : value;
        }

        private static double SafeHeight(double value)
        {
            return double.IsNaN(value) || value <= 0 ? FallbackHeight : value;
        }
    }
}
