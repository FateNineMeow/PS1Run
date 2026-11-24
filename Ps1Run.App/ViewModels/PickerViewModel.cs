using System.ComponentModel;
using System.IO;
using Ps1Run.App.Localization;
using Ps1Run.Core.Dispatch;
using Ps1Run.Core.PowerShell;
using Ps1Run.Core.Settings;

namespace Ps1Run.App.ViewModels
{
    /// <summary>
    /// 选择窗视图模型（wiki 05 §5、02 F2.4 / F2.5）。
    /// 是否记住选择由窗口读取，取消时一律不写设置。
    /// 解释器标识来自 Core 的只读预判（PowerShellPreview），不改变实际分发结果。
    /// </summary>
    public sealed class PickerViewModel : INotifyPropertyChanged
    {
        private readonly PowerShellPreview? _powerShell;
        private bool _rememberChoice;

        public PickerViewModel(PickRequest request)
        {
            request ??= new PickRequest(string.Empty);
            ScriptPath = request.ScriptPath ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(ScriptPath)
                ? L("Picker.NoScript")
                : Path.GetFileName(ScriptPath);
            _powerShell = request.PowerShell;
        }

        public PickerViewModel(string scriptPath)
            : this(new PickRequest(scriptPath))
        {
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string DisplayName { get; }

        public string ScriptPath { get; }

        /// <summary>右侧版本标签：PS5 / PS7；无预判信息时显示占位符。</summary>
        public string PowerShellBadgeText
        {
            get
            {
                if (_powerShell is null)
                {
                    return "—";
                }

                return _powerShell.Edition == PowerShellEdition.Ps5 ? "PS5" : "PS7";
            }
        }

        /// <summary>主标识：执行时将使用的解释器版本。</summary>
        public string PowerShellTitle
        {
            get
            {
                if (_powerShell is null)
                {
                    return L("Picker.UnknownTitle");
                }

                return _powerShell.Edition == PowerShellEdition.Ps5
                    ? L("Picker.Ps5Title")
                    : L("Picker.Ps7Title");
            }
        }

        /// <summary>副标识：可执行文件名 + 选择依据（固定策略 / 自动判定的编码）。</summary>
        public string PowerShellDetail
        {
            get
            {
                if (_powerShell is null)
                {
                    return L("Picker.NoInfo");
                }

                var reason = _powerShell.Policy switch
                {
                    PowerShellPolicy.Ps5 => L("Picker.PolicyPs5"),
                    PowerShellPolicy.Ps7 => L("Picker.PolicyPs7"),
                    _ => L("Picker.AutoReason") + DescribeEncoding(_powerShell.Encoding),
                };

                if (!_powerShell.IsAvailable)
                {
                    return reason + " · " + L("Picker.MissingExe");
                }

                return Path.GetFileName(_powerShell.ExecutablePath!) + " · " + reason;
            }
        }

        /// <summary>完整路径，供 ToolTip 展示。</summary>
        public string PowerShellPath => _powerShell?.ExecutablePath ?? string.Empty;

        /// <summary>降级或缺失时的提醒；无提醒时为空。</summary>
        public string PowerShellWarning => _powerShell?.Note ?? string.Empty;

        public bool HasPowerShellWarning => !string.IsNullOrWhiteSpace(PowerShellWarning);

        public bool RememberChoice
        {
            get => _rememberChoice;
            set
            {
                if (_rememberChoice == value)
                {
                    return;
                }

                _rememberChoice = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RememberChoice)));
            }
        }

        public PickResult ToResult(LaunchAction action)
        {
            // 取消不记住任何偏好（wiki 05 §6）
            return new PickResult(action, action != LaunchAction.Cancel && RememberChoice);
        }

        private static string DescribeEncoding(ScriptEncodingKind? kind)
        {
            return kind switch
            {
                ScriptEncodingKind.Utf8Bom => L("Encoding.Utf8Bom"),
                ScriptEncodingKind.Utf8NoBom => L("Encoding.Utf8NoBom"),
                ScriptEncodingKind.Utf16Le => L("Encoding.Utf16Le"),
                ScriptEncodingKind.Utf16Be => L("Encoding.Utf16Be"),
                ScriptEncodingKind.Ansi => L("Encoding.Ansi"),
                ScriptEncodingKind.Empty => L("Encoding.Empty"),
                ScriptEncodingKind.Unknown => L("Encoding.Unknown"),
                _ => L("Encoding.Unknown"),
            };
        }

        private static string L(string key) => LocalizationManager.Instance.Get(key);
    }
}
