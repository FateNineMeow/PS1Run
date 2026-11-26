using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using Ps1Run.App.Localization;
using Ps1Run.Core.Dispatch;
using Ps1Run.Core.Editors;
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
        private readonly string _configWarningText;
        private bool _rememberChoice;
        private bool _rememberForScript;

        public PickerViewModel(PickRequest request)
        {
            request ??= new PickRequest(string.Empty);
            ScriptPath = request.ScriptPath ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(ScriptPath)
                ? L("Picker.NoScript")
                : Path.GetFileName(ScriptPath);
            _powerShell = request.PowerShell;
            _configWarningText = BuildConfigWarning(request.Settings);
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

        /// <summary>执行程序/编辑器配置缺失或路径失效时的提醒；无提醒时为空。</summary>
        public string ConfigWarningText => _configWarningText;

        public bool HasConfigWarning => _configWarningText.Length > 0;

        /// <summary>语言文件缺少版本号（或版本较旧）时的运行期提醒。</summary>
        public bool HasPackWarning => LocalizationManager.Instance.IsPackOutdated;

        public string PackWarningText => HasPackWarning ? L("Picker.PackWarning") : string.Empty;

        /// <summary>全局记住：修改 RunMode，以后所有脚本都不再询问。</summary>
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
                if (value && _rememberForScript)
                {
                    _rememberForScript = false;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RememberForScript)));
                }
            }
        }

        /// <summary>仅此脚本：按当前判断标准写入 rule.txt。</summary>
        public bool RememberForScript
        {
            get => _rememberForScript;
            set
            {
                if (_rememberForScript == value)
                {
                    return;
                }

                _rememberForScript = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RememberForScript)));
                if (value && _rememberChoice)
                {
                    _rememberChoice = false;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RememberChoice)));
                }
            }
        }

        public PickResult ToResult(LaunchAction action)
        {
            // 取消不记住任何偏好（wiki 05 §6）
            var confirmed = action != LaunchAction.Cancel;
            return new PickResult(
                action,
                confirmed && RememberChoice,
                confirmed && RememberForScript);
        }

        /// <summary>
        /// 只提示"配置写了但程序不在"以及"没有配置也找不到程序"的情况；
        /// 正常自动探测到解释器和编辑器时保持安静，避免每次弹窗都刷红字。
        /// </summary>
        private static string BuildConfigWarning(AppSettings? settings)
        {
            if (settings is null)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(settings.CustomPs5Path)
                && !File.Exists(settings.CustomPs5Path!))
            {
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    L("Picker.ConfigPs5Missing"),
                    settings.CustomPs5Path));
            }

            if (!string.IsNullOrWhiteSpace(settings.CustomPs7Path)
                && !File.Exists(settings.CustomPs7Path!))
            {
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    L("Picker.ConfigPs7Missing"),
                    settings.CustomPs7Path));
            }

            if (!string.IsNullOrWhiteSpace(settings.CustomEditorPath))
            {
                if (!File.Exists(settings.CustomEditorPath!))
                {
                    lines.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        L("Picker.ConfigEditorMissing"),
                        settings.CustomEditorPath));
                }
            }
            else if (new EditorLocator().Resolve() is null)
            {
                lines.Add(L("Picker.EditorMissing"));
            }

            return string.Join(Environment.NewLine, lines);
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
