using System;
using System.ComponentModel;
using Ps1Run.App.Views;
using Ps1Run.Core.Dispatch;

namespace Ps1Run.App.Services
{
    /// <summary>
    /// Core 的 IActionPicker 的 WPF 实现（wiki 03 §4：UI 依赖留在 App）。
    /// 返回 null 表示当前环境无法创建窗口（无交互桌面），由 Core 转成退出码 4。
    /// </summary>
    public sealed class WpfActionPicker : IActionPicker
    {
        public PickResult? Pick(PickRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            try
            {
                var window = new PickerWindow(request);
                var confirmed = window.ShowDialog();
                if (confirmed == true && window.Result is not null)
                {
                    return window.Result;
                }

                // 关窗按钮 / Esc / 未选择：统一视为取消，且不记住任何设置
                return PickResult.Cancel;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (Win32Exception)
            {
                return null;
            }
        }
    }
}
