using Microsoft.Win32;
using Ps1Run.Core.Dispatch;
using Ps1Run.Core.PowerShell;

namespace Ps1Run.App.Services
{
    /// <summary>
    /// 执行程序缺失时的人工指定（F3.6）。
    /// Core 只定义 IExecutablePicker，文件对话框留在 App 层。
    /// </summary>
    public sealed class WpfExecutablePicker : IExecutablePicker
    {
        public string? PickPowerShell(PowerShellEdition edition)
        {
            var expected = edition == PowerShellEdition.Ps5 ? "powershell.exe" : "pwsh.exe";
            var dialog = new OpenFileDialog
            {
                Title = "未找到 " + (edition == PowerShellEdition.Ps5
                    ? "Windows PowerShell 5.1"
                    : "PowerShell 7+") + "，请指定 " + expected,
                Filter = "PowerShell 可执行文件|" + expected + ";powershell.exe;pwsh.exe|所有文件|*.*",
                CheckFileExists = true,
                DefaultExt = ".exe",
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
