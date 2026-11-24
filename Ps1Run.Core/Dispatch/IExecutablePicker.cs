using Ps1Run.Core.PowerShell;

namespace Ps1Run.Core.Dispatch
{
    /// <summary>
    /// 执行程序缺失时的人工选择入口（F3.6）。
    /// Core 不依赖 WPF，由 App 提供 OpenFileDialog 实现。
    /// 返回 null 表示用户取消。
    /// </summary>
    public interface IExecutablePicker
    {
        string? PickPowerShell(PowerShellEdition edition);
    }
}
