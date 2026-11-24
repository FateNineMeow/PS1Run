using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Win32;
using Ps1Run.App.Localization;
using Ps1Run.App.Services;
using Ps1Run.Core.Associations;
using Ps1Run.Core.Configuration;
using Ps1Run.Core.Editors;
using Ps1Run.Core.Infrastructure;
using Ps1Run.Core.Localization;
using Ps1Run.Core.PowerShell;
using Ps1Run.Core.Settings;

namespace Ps1Run.App.ViewModels
{
    /// <summary>
    /// 主界面视图模型（wiki 02 F2.7 / F5.4 / F7.x）。
    /// 设置分两份：settings.json（运行方式 / 解释器 / 编辑器）与
    /// config.json（日志天数 / 主题 / 语言）。
    /// </summary>
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private readonly SettingsStore _store;
        private readonly ConfigStore _configStore;
        private readonly AssociationService _associations;
        private readonly IReadOnlyList<LanguageOption> _languages;

        private AppSettings _settings;
        private AppConfig _config;
        private int _runModeIndex;
        private int _policyIndex;
        private int _themeIndex;
        private int _languageIndex;
        private string _customEditorPath;
        private string _customPs5Path;
        private string _customPs7Path;
        private string _logDaysText;
        private string _statusText = string.Empty;
        private string? _detectionText;
        private bool _suppressAutoSave;

        public MainViewModel(SettingsStore store, AppSettings settings, ConfigStore configStore, AppConfig config)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
            _config = AppConfig.Normalize(config);
            _associations = new AssociationService();

            // 语言列表来自 lang 目录的 *.json（启动时已确保 zh-cn.json 存在）
            _languages = LocalizationManager.Instance.AvailableLanguages();

            _suppressAutoSave = true;
            _runModeIndex = (int)_settings.RunMode;
            _policyIndex = (int)_settings.PowerShellPolicy;
            _themeIndex = (int)_config.Theme;
            _languageIndex = Math.Max(0, IndexOfLanguage(_config.Language));
            _customEditorPath = _settings.CustomEditorPath ?? string.Empty;
            _customPs5Path = _settings.CustomPs5Path ?? string.Empty;
            _customPs7Path = _settings.CustomPs7Path ?? string.Empty;
            _logDaysText = _config.LogDays.ToString(CultureInfo.InvariantCulture);
            _suppressAutoSave = false;

            // 语言切换后，视图模型自己拼的文案（探测结果、状态快照）也要跟着刷新
            LocalizationManager.Instance.PropertyChanged += OnLocalizationChanged;

            RefreshStatus();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ExecutablePath => _associations.ExecutablePath;

        public string SettingsFilePath => _store.FilePath;

        public string ConfigFilePath => _configStore.FilePath;

        public string LogDirectory => DiagnosticsLog.LogDirectory;

        public string LanguageDirectory => AppPaths.LanguageDirectory;

        public string CurrentLogFile => DiagnosticsLog.CurrentLogFile;

        public IReadOnlyList<LanguageOption> Languages => _languages;

        /// <summary>0=执行，1=编辑，2=选择（与 RunMode 枚举顺序一致）。</summary>
        public int RunModeIndex
        {
            get => _runModeIndex;
            set
            {
                if (value < 0 || value > 2 || _runModeIndex == value)
                {
                    return;
                }

                _runModeIndex = value;
                _settings = _settings with { RunMode = (RunMode)value };
                AutoSave();
                OnPropertyChanged();
            }
        }

        /// <summary>0=PS5，1=PS7，2=自动（与 PowerShellPolicy 枚举顺序一致）。</summary>
        public int PolicyIndex
        {
            get => _policyIndex;
            set
            {
                if (value < 0 || value > 2 || _policyIndex == value)
                {
                    return;
                }

                _policyIndex = value;
                _settings = _settings with { PowerShellPolicy = (PowerShellPolicy)value };
                AutoSave();
                OnPropertyChanged();
            }
        }

        /// <summary>0=自动，1=浅色，2=深色（与 AppTheme 枚举顺序一致），切换立即生效。</summary>
        public int ThemeIndex
        {
            get => _themeIndex;
            set
            {
                if (value < 0 || value > 2 || _themeIndex == value)
                {
                    return;
                }

                _themeIndex = value;
                UpdateConfig(_config with { Theme = (AppTheme)value });
                ThemeManager.Apply((AppTheme)value);
                OnPropertyChanged();
            }
        }

        /// <summary>语言下拉框索引，切换后立即重新加载 lang\&lt;code&gt;.json。</summary>
        public int LanguageIndex
        {
            get => _languageIndex;
            set
            {
                if (value < 0 || value >= _languages.Count || _languageIndex == value)
                {
                    return;
                }

                _languageIndex = value;
                var code = _languages[value].Code;
                UpdateConfig(_config with { Language = code });
                LocalizationManager.Instance.Load(code);
                RefreshStatus();
                OnPropertyChanged();
            }
        }

        /// <summary>日志保留天数（1–365）；输入过程中只在合法时落盘。</summary>
        public string LogDaysText
        {
            get => _logDaysText;
            set
            {
                var text = (value ?? string.Empty).Trim();
                if (_logDaysText == text)
                {
                    return;
                }

                _logDaysText = text;
                OnPropertyChanged();

                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days)
                    || days < AppConfig.MinLogDays
                    || days > AppConfig.MaxLogDays)
                {
                    StatusText = L("Main.LogDays.Invalid");
                    return;
                }

                if (days == _config.LogDays)
                {
                    return;
                }

                UpdateConfig(_config with { LogDays = days });
                StatusText = L("Main.LogDays.Saved") + days.ToString(CultureInfo.InvariantCulture) + L("Main.LogDays.Unit");
            }
        }

        public string CustomEditorPath
        {
            get => _customEditorPath;
            set
            {
                var normalized = value ?? string.Empty;
                if (_customEditorPath == normalized)
                {
                    return;
                }

                _customEditorPath = normalized;
                _settings = _settings with
                {
                    CustomEditorPath = normalized.Length == 0 ? null : normalized,
                };
                AutoSave();
                OnPropertyChanged();
            }
        }

        public string CustomPs5Path
        {
            get => _customPs5Path;
            set
            {
                var normalized = value ?? string.Empty;
                if (_customPs5Path == normalized)
                {
                    return;
                }

                _customPs5Path = normalized;
                _settings = _settings with { CustomPs5Path = normalized.Length == 0 ? null : normalized };
                AutoSave();
                OnPropertyChanged();
            }
        }

        public string CustomPs7Path
        {
            get => _customPs7Path;
            set
            {
                var normalized = value ?? string.Empty;
                if (_customPs7Path == normalized)
                {
                    return;
                }

                _customPs7Path = normalized;
                _settings = _settings with { CustomPs7Path = normalized.Length == 0 ? null : normalized };
                AutoSave();
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (_statusText == value)
                {
                    return;
                }

                _statusText = value;
                OnPropertyChanged();
            }
        }

        /// <summary>一键探测的简短结果（命中路径 + 命中来源），显示在探测行。</summary>
        public string DetectionText => _detectionText ?? L("Main.Detection.Idle");

        public void Save()
        {
            try
            {
                _store.Save(_settings);
                _configStore.Save(_config);
                DiagnosticsLog.Info("设置已保存：" + _store.FilePath + " / " + _configStore.FilePath);
                StatusText = L("Main.Saved") + _store.FilePath + Environment.NewLine
                    + L("Status.ConfigFile") + _configStore.FilePath + Environment.NewLine + Environment.NewLine
                    + BuildStatus();
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Error("保存设置失败：" + ex.Message, ex);
                StatusText = L("Main.SaveFailed") + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Error("保存设置失败：" + ex.Message, ex);
                StatusText = L("Main.SaveFailed") + ex.Message;
            }
        }

        public void RegisterAssociation()
        {
            var result = _associations.Register();
            DiagnosticsLog.Info("用户级关联注册：" + (result.Success ? "成功" : "失败") + " " + result.Message);
            RefreshStatus(result.Message);
        }

        public void UnregisterAssociation()
        {
            var result = _associations.Unregister();
            DiagnosticsLog.Info("用户级关联注销：" + (result.Success ? "成功" : "失败") + " " + result.Message);
            RefreshStatus(result.Message);
        }

        public void RefreshStatus(string? prefix = null)
        {
            var body = BuildStatus();
            StatusText = string.IsNullOrEmpty(prefix)
                ? body
                : prefix + Environment.NewLine + Environment.NewLine + body;

            OnPropertyChanged(nameof(ExecutablePath));
        }

        /// <summary>
        /// 一键探测：按 wiki 07 §3（PS5/PS7 探测顺序）与 08 §4（编辑器回退链）
        /// 识别解释器与编辑器，命中结果回填设置并自动保存；未命中时保留用户原配置。
        /// </summary>
        public void AutoDetect()
        {
            var ps5 = new PowerShellLocator().Locate(PowerShellEdition.Ps5);
            var ps7 = new PowerShellLocator().Locate(PowerShellEdition.Ps7);
            var editor = new EditorLocator().Resolve();

            var report = new StringBuilder();
            _suppressAutoSave = true;
            try
            {
                if (ps5 is null)
                {
                    report.Append(L("Main.Detection.Ps5Label")).Append("：").AppendLine(L("Main.Detection.NotFound"));
                }
                else
                {
                    CustomPs5Path = ps5.ExecutablePath;
                    report.Append(L("Main.Detection.Ps5Label")).Append("：").Append(ps5.ExecutablePath)
                        .Append("（").Append(ps5.ProbeSource).AppendLine("）");
                }

                if (ps7 is null)
                {
                    report.Append(L("Main.Detection.Ps7Label")).Append("：").AppendLine(L("Main.Detection.NotFound"));
                }
                else
                {
                    CustomPs7Path = ps7.ExecutablePath;
                    report.Append(L("Main.Detection.Ps7Label")).Append("：").Append(ps7.ExecutablePath)
                        .Append("（").Append(ps7.ProbeSource).AppendLine("）");
                }

                if (editor is null)
                {
                    report.Append(L("Main.Detection.EditorLabel")).Append("：").AppendLine(L("Main.Detection.NotFound"));
                }
                else
                {
                    CustomEditorPath = editor.ExecutablePath;
                    report.Append(L("Main.Detection.EditorLabel")).Append("：").Append(editor.DisplayName)
                        .Append(" — ").Append(editor.ExecutablePath)
                        .Append("（").Append(editor.Source).AppendLine("）");
                }
            }
            finally
            {
                _suppressAutoSave = false;
            }

            try
            {
                _store.Save(_settings);
                report.AppendLine(L("Main.Detection.Saved"));
            }
            catch (IOException ex)
            {
                report.AppendLine(L("Main.SaveFailed") + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                report.AppendLine(L("Main.SaveFailed") + ex.Message);
            }

            _detectionText = report.ToString().TrimEnd();
            OnPropertyChanged(nameof(DetectionText));
            StatusText = L("Main.Detection.Done") + Environment.NewLine + Environment.NewLine
                + DetectionText + Environment.NewLine + Environment.NewLine + BuildStatus();

            DiagnosticsLog.Info(
                "一键探测：PS5=" + (ps5?.ExecutablePath ?? "<未找到>")
                + " PS7=" + (ps7?.ExecutablePath ?? "<未找到>")
                + " 编辑器=" + (editor?.ExecutablePath ?? "<未找到>"));
        }

        public void PickEditor()
        {
            var dialog = new OpenFileDialog
            {
                Title = L("Main.PickEditorTitle"),
                Filter = L("Main.Filter.Exe") + "|*.exe|" + L("Main.Filter.All") + "|*.*",
                CheckFileExists = true,
            };

            if (dialog.ShowDialog() == true)
            {
                CustomEditorPath = dialog.FileName;
                DiagnosticsLog.Info("用户选择编辑器：" + dialog.FileName);
                StatusText = L("Main.PickEditorSelected") + dialog.FileName + L("Main.AutoSavedSuffix");
            }
            else
            {
                DiagnosticsLog.Info("用户取消选择编辑器。");
            }
        }

        public void PickPowerShell(PowerShellEdition edition)
        {
            var expected = edition == PowerShellEdition.Ps5 ? "powershell.exe" : "pwsh.exe";
            var dialog = new OpenFileDialog
            {
                Title = L("Main.PickPowerShellTitle") + expected,
                Filter = L("Main.Filter.PowerShell") + "|" + expected + ";powershell.exe;pwsh.exe|"
                    + L("Main.Filter.All") + "|*.*",
                CheckFileExists = true,
            };

            if (dialog.ShowDialog() != true)
            {
                DiagnosticsLog.Info("用户取消选择 " + expected + "。");
                return;
            }

            if (edition == PowerShellEdition.Ps5)
            {
                CustomPs5Path = dialog.FileName;
            }
            else
            {
                CustomPs7Path = dialog.FileName;
            }

            DiagnosticsLog.Info("用户指定 " + expected + "：" + dialog.FileName);
            StatusText = L("Main.PickPowerShellSelected") + expected + "：" + dialog.FileName + L("Main.AutoSavedSuffix");
        }

        public void RegisterAssociationMachine()
        {
            if (ElevationHelper.TryRestartElevated("--register-machine"))
            {
                DiagnosticsLog.Info("已请求管理员权限：机器级注册。");
                StatusText = L("Main.Elevation.Register");
            }
            else
            {
                DiagnosticsLog.Warn("未能获取管理员权限：机器级注册（UAC 被取消或被拒绝）。");
                StatusText = L("Main.Elevation.Denied");
            }
        }

        public void UnregisterAssociationMachine()
        {
            if (ElevationHelper.TryRestartElevated("--unregister-machine"))
            {
                DiagnosticsLog.Info("已请求管理员权限：机器级注销。");
                StatusText = L("Main.Elevation.Unregister");
            }
            else
            {
                DiagnosticsLog.Warn("未能获取管理员权限：机器级注销（UAC 被取消或被拒绝）。");
                StatusText = L("Main.Elevation.Denied");
            }
        }

        public void ShowOpenWithDialog()
        {
            var result = _associations.ShowOpenWithDialog();
            RefreshStatus(result.Message);
        }

        public void OpenDefaultAppsSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:defaultapps")
                {
                    UseShellExecute = true,
                });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                StatusText = L("Main.OpenDefaultAppsFailed") + ex.Message;
            }
        }

        /// <summary>在资源管理器中打开日志目录（F6.4）。</summary>
        public void OpenLogFolder()
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                Process.Start(new ProcessStartInfo("explorer.exe")
                {
                    Arguments = "\"" + LogDirectory + "\"",
                    UseShellExecute = true,
                });
                StatusText = L("Main.LogFolderOpened") + LogDirectory;
                DiagnosticsLog.Info("打开日志目录：" + LogDirectory);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                DiagnosticsLog.Error("打开日志目录失败：" + ex.Message, ex);
                StatusText = L("Main.OpenLogFolderFailed") + ex.Message;
            }
        }

        /// <summary>用系统关联的编辑器打开当日日志（F6.5）。</summary>
        public void OpenCurrentLog()
        {
            try
            {
                DiagnosticsLog.Info("查看当日日志：" + CurrentLogFile);
                Process.Start(new ProcessStartInfo(CurrentLogFile)
                {
                    UseShellExecute = true,
                });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                DiagnosticsLog.Error("打开当日日志失败：" + ex.Message, ex);
                StatusText = L("Main.OpenLogFailed") + ex.Message;
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Error("打开当日日志失败：" + ex.Message, ex);
                StatusText = L("Main.OpenLogFailed") + ex.Message;
            }
        }

        /// <summary>用默认程序打开 config.json，方便直接改 LogDays / Theme / Language。</summary>
        public void OpenConfigFile()
        {
            try
            {
                DiagnosticsLog.Info("打开配置文件：" + _configStore.FilePath);
                Process.Start(new ProcessStartInfo(_configStore.FilePath)
                {
                    UseShellExecute = true,
                });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                DiagnosticsLog.Error("打开配置文件失败：" + ex.Message, ex);
                StatusText = L("Main.OpenConfigFailed") + ex.Message;
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Error("打开配置文件失败：" + ex.Message, ex);
                StatusText = L("Main.OpenConfigFailed") + ex.Message;
            }
        }

        /// <summary>立即按当前保留天数清理一次过期日志。</summary>
        public void PruneLogsNow()
        {
            var removed = DiagnosticsLog.Prune(_config.LogDays);
            StatusText = L("Main.PruneResult") + removed.ToString(CultureInfo.InvariantCulture) + L("Main.PruneUnit")
                + Environment.NewLine + L("Status.LogDirectory") + LogDirectory;
            DiagnosticsLog.Info(
                "手动清理日志：删除 " + removed.ToString(CultureInfo.InvariantCulture)
                + " 个，保留天数=" + _config.LogDays.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>取最近日志文本，供"复制最近日志"按钮使用（F6.5）。</summary>
        public string BuildRecentLogReport() => DiagnosticsLog.ReadRecent(150);

        private void UpdateConfig(AppConfig config)
        {
            _config = AppConfig.Normalize(config);

            try
            {
                _configStore.Save(_config);
                DiagnosticsLog.Info(
                    "config.json 已保存：file=" + _configStore.FilePath
                    + " logDays=" + _config.LogDays.ToString(CultureInfo.InvariantCulture)
                    + " theme=" + _config.Theme
                    + " language=" + _config.Language);
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Error("保存 config.json 失败：" + ex.Message, ex);
                StatusText = L("Main.SaveFailed") + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Error("保存 config.json 失败：" + ex.Message, ex);
                StatusText = L("Main.SaveFailed") + ex.Message;
            }
        }

        private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 语言变化后，视图模型计算出来的文字（探测结果 / 状态快照）需要重新取
            OnPropertyChanged(string.Empty);
        }

        private int IndexOfLanguage(string code)
        {
            for (var i = 0; i < _languages.Count; i++)
            {
                if (string.Equals(_languages[i].Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }

        private void AutoSave()
        {
            if (_suppressAutoSave)
            {
                return;
            }

            try
            {
                _store.Save(_settings);
            }
            catch (IOException)
            {
                // 自动保存失败不阻断界面交互，手动"保存设置"可再次尝试
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private string BuildStatus()
        {
            var status = _associations.GetStatus();
            var builder = new StringBuilder();

            builder.Append(L("Status.Association"))
                .Append(status.ProgIdRegistered ? L("Status.Registered") : L("Status.NotRegistered"))
                .Append(L("Status.Verbs")).Append("open=").Append(Flag(status.OpenVerbRegistered))
                .Append(" run=").Append(Flag(status.RunVerbRegistered))
                .Append(" edit=").Append(Flag(status.EditVerbRegistered))
                .AppendLine();
            builder.Append(L("Status.OpenWithProgids"))
                .Append(status.ExtensionAdvertised ? L("Status.Added") : L("Status.NotAdded"))
                .AppendLine();
            builder.Append(L("Status.ExecutablePath")).Append(status.ExecutablePath).AppendLine();
            builder.Append(L("Status.UserChoice")).Append(UserChoiceGuard.Describe(status.UserChoice)).AppendLine();

            var locator = new PowerShellLocator();
            var ps5 = locator.Locate(PowerShellEdition.Ps5, _settings.CustomPs5Path);
            var ps7 = locator.Locate(PowerShellEdition.Ps7, _settings.CustomPs7Path);
            builder.Append(L("Status.Ps5")).Append(DescribePowerShell(ps5)).AppendLine();
            builder.Append(L("Status.Ps7")).Append(DescribePowerShell(ps7)).AppendLine();

            var editor = new EditorLocator().Resolve(_settings.CustomEditorPath);
            builder.Append(L("Status.Editor"));
            builder.Append(editor is null
                ? L("Status.EditorMissing")
                : editor.DisplayName + "（" + editor.Source + "）");
            builder.AppendLine();
            builder.Append(L("Status.SettingsFile")).Append(_store.FilePath).AppendLine();
            builder.Append(L("Status.ConfigFile")).Append(_configStore.FilePath).AppendLine();
            builder.Append(L("Status.LogDirectory")).Append(LogDirectory).AppendLine();
            builder.Append(L("Status.CurrentLog")).Append(CurrentLogFile);

            return builder.ToString();
        }

        private static string DescribePowerShell(PowerShellTarget? target)
        {
            return target is null
                ? L("Status.NotFound")
                : target.ExecutablePath + "（" + target.ProbeSource + "）";
        }

        private static string Flag(bool value) => value ? "√" : "×";

        private static string L(string key) => LocalizationManager.Instance.Get(key);

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
