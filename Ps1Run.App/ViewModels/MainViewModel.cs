using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
using Ps1Run.Core.Rules;
using Ps1Run.Core.Settings;

namespace Ps1Run.App.ViewModels
{
    /// <summary>
    /// 主界面视图模型（wiki 02 F2.7 / F5.4 / F7.x）。
    /// 设置只有一份：软件根目录 config.cfg（程序配置 + 运行设置 + 版本号）。
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
        private AssociationStatus? _associationStatus;
        private bool _suppressAutoSave;
        private readonly ObservableCollection<ScriptRule> _rules = new ObservableCollection<ScriptRule>();
        private ScriptRule? _selectedRule;
        private string _ruleSearchText = string.Empty;
        private string _ruleStatusText = string.Empty;
        private readonly string _ruleFilePath;

        public MainViewModel(SettingsStore store, AppSettings settings, ConfigStore configStore, AppConfig config)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
            _config = AppConfig.Normalize(config);
            _associations = new AssociationService();
            _ruleFilePath = AppPaths.RuleFile;
            RefreshRules(silent: true);

            // 语言列表来自 lang 目录的 *.lang（启动时已确保 zh-cn.lang 存在）
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

        /// <summary>唯一的配置文件路径（程序配置与运行设置都在这里）。</summary>
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

        /// <summary>脚本规则判断标准：文件路径始终匹配，以下三项来自 config.cfg。</summary>
        public bool RuleMatchSize
        {
            get => _settings.RuleMatchSize;
            set
            {
                if (_settings.RuleMatchSize == value)
                {
                    return;
                }

                SetRuleCriteria(value, nameof(RuleMatchSize), s => s with { RuleMatchSize = value });
            }
        }

        public bool RuleMatchModifiedTime
        {
            get => _settings.RuleMatchModifiedTime;
            set
            {
                if (_settings.RuleMatchModifiedTime == value)
                {
                    return;
                }

                SetRuleCriteria(value, nameof(RuleMatchModifiedTime), s => s with { RuleMatchModifiedTime = value });
            }
        }

        public bool RuleMatchHash
        {
            get => _settings.RuleMatchHash;
            set
            {
                if (_settings.RuleMatchHash == value)
                {
                    return;
                }

                SetRuleCriteria(value, nameof(RuleMatchHash), s => s with { RuleMatchHash = value });
            }
        }

        public AppSettings CurrentSettings => _settings;

        /// <summary>按路径关键字过滤后的规则列表。</summary>
        public IEnumerable<ScriptRule> FilteredRules
        {
            get
            {
                var keyword = _ruleSearchText.Trim();
                if (keyword.Length == 0)
                {
                    return _rules;
                }

                return _rules.Where(r => r.Path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        public ScriptRule? SelectedRule
        {
            get => _selectedRule;
            set
            {
                if (_selectedRule == value)
                {
                    return;
                }

                _selectedRule = value;
                OnPropertyChanged();
            }
        }

        public string RuleSearchText
        {
            get => _ruleSearchText;
            set
            {
                var text = value ?? string.Empty;
                if (_ruleSearchText == text)
                {
                    return;
                }

                _ruleSearchText = text;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredRules));
            }
        }

        /// <summary>规则列表底部的条数 / 操作提示。</summary>
        public string RuleStatusText => string.IsNullOrEmpty(_ruleStatusText)
            ? string.Format(CultureInfo.CurrentCulture, L("Rule.Count"), _rules.Count.ToString(CultureInfo.InvariantCulture))
            : _ruleStatusText;

        public string RuleFilePath => _ruleFilePath;

        /// <summary>当前内存中的全部规则（含未保存修改）。</summary>
        public IReadOnlyList<ScriptRule> AllRules => _rules.ToList();

        /// <summary>规则列表是否有尚未写入 rule.txt 的增删改。</summary>
        public bool HasUnsavedRules { get; private set; }

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
                OnPropertyChanged(nameof(BottomHint));
            }
        }

        /// <summary>底部状态条文字：有操作反馈时优先显示，否则显示"更改即时生效"。</summary>
        public string BottomHint => string.IsNullOrEmpty(_statusText) ? L("Hint.AutoSaved") : _statusText;

        /// <summary>一键探测的简短结果（命中路径 + 命中来源），显示在探测行。</summary>
        public string DetectionText => _detectionText ?? L("Main.Detection.Idle");

        /// <summary>
        /// 关联状态徽标的三态：None / Partial / Full。
        /// 用字符串而不是枚举，便于 XAML 里直接写 DataTrigger 的 Value。
        /// </summary>
        public string AssociationBadgeState
        {
            get
            {
                if (_associationStatus is null || !_associationStatus.ProgIdRegistered)
                {
                    return "None";
                }

                return _associationStatus.FullyRegistered ? "Full" : "Partial";
            }
        }

        /// <summary>标题栏徽标文字：已注册 / 部分注册 / 未注册。</summary>
        public string AssociationBadgeText
        {
            get
            {
                switch (AssociationBadgeState)
                {
                    case "Full":
                        return L("Badge.Registered");
                    case "Partial":
                        return L("Badge.Partial");
                    default:
                        return L("Badge.NotRegistered");
                }
            }
        }

        /// <summary>关联页状态卡片：三个动词是否齐备。</summary>
        public string AssociationVerbsText
        {
            get
            {
                var status = _associationStatus;
                if (status is null)
                {
                    return "—";
                }

                return "open " + Flag(status.OpenVerbRegistered)
                    + "   run " + Flag(status.RunVerbRegistered)
                    + "   edit " + Flag(status.EditVerbRegistered);
            }
        }

        /// <summary>关联页状态卡片：扩展声明是否已加入 OpenWithProgids。</summary>
        public string AssociationExtensionText
        {
            get
            {
                var status = _associationStatus;
                if (status is null)
                {
                    return "—";
                }

                return status.ExtensionAdvertised ? L("Status.Added") : L("Status.NotAdded");
            }
        }

        /// <summary>关联页状态卡片：UserChoice 现状（可能压过 ProgId 关联，文案跟随语言包）。</summary>
        public string AssociationUserChoiceText
        {
            get
            {
                if (_associationStatus is null)
                {
                    return "—";
                }

                var info = _associationStatus.UserChoice;
                if (info.ReadFailed)
                {
                    return L("Status.UserChoice.ReadFailed");
                }

                if (!info.HasOverride)
                {
                    return L("Status.UserChoice.None");
                }

                if (info.PointsToUs)
                {
                    return L("Status.UserChoice.PointsToUs");
                }

                return string.Format(
                    CultureInfo.CurrentCulture,
                    L("Status.UserChoice.Overridden"),
                    info.ProgId ?? string.Empty);
            }
        }

        /// <summary>UserChoice 指向别的 ProgId 时，卡片上给出橙色提醒。</summary>
        public bool AssociationUserChoiceOverridden =>
            _associationStatus is not null
            && _associationStatus.UserChoice.HasOverride
            && !_associationStatus.UserChoice.PointsToUs;

        /// <summary>判断标准变化：立即写 config.cfg，并按新标准重写全部规则指纹。</summary>
        private void SetRuleCriteria(bool value, string propertyName, Func<AppSettings, AppSettings> update)
        {
            _settings = update(_settings);
            AutoSave();
            OnPropertyChanged(propertyName);

            var refreshed = _rules.Select(r => ScriptRuleFingerprint.Refresh(r, _settings)).ToList();
            _rules.Clear();
            foreach (var rule in refreshed)
            {
                _rules.Add(rule);
            }

            try
            {
                ScriptRuleStore.Save(_ruleFilePath, _rules);
                _ruleStatusText = L("Rule.CriteriaSaved");
                StatusText = _ruleStatusText;
            }
            catch (IOException ex)
            {
                _ruleStatusText = L("Rule.SaveFailed") + ex.Message;
                StatusText = _ruleStatusText;
            }
            catch (UnauthorizedAccessException ex)
            {
                _ruleStatusText = L("Rule.SaveFailed") + ex.Message;
                StatusText = _ruleStatusText;
            }

            HasUnsavedRules = false;
            OnPropertyChanged(nameof(HasUnsavedRules));
            OnPropertyChanged(nameof(FilteredRules));
            OnPropertyChanged(nameof(RuleStatusText));
        }

        /// <summary>从 rule.txt 重新载入规则（丢弃当前未保存的增删改）。</summary>
        public void RefreshRules(bool silent = false)
        {
            try
            {
                var loaded = ScriptRuleStore.Load(_ruleFilePath);
                _rules.Clear();
                foreach (var rule in loaded)
                {
                    _rules.Add(rule);
                }

                _ruleStatusText = string.Empty;
                if (!silent)
                {
                    StatusText = L("Rule.Refreshed") + _ruleFilePath;
                }
            }
            catch (IOException ex)
            {
                _ruleStatusText = L("Rule.SaveFailed") + ex.Message;
                StatusText = _ruleStatusText;
            }
            catch (UnauthorizedAccessException ex)
            {
                _ruleStatusText = L("Rule.SaveFailed") + ex.Message;
                StatusText = _ruleStatusText;
            }

            SelectedRule = null;
            HasUnsavedRules = false;
            OnPropertyChanged(nameof(HasUnsavedRules));
            OnPropertyChanged(nameof(FilteredRules));
            OnPropertyChanged(nameof(RuleStatusText));
        }

        /// <summary>新增或覆盖一条规则（尚未写盘，点保存才落盘）。</summary>
        public void AddRule(ScriptRule rule)
        {
            if (rule is null)
            {
                return;
            }

            var existing = _rules.FirstOrDefault(r =>
                string.Equals(r.Path, rule.Path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                _rules.Remove(existing);
            }

            _rules.Add(rule);
            SelectedRule = rule;
            _ruleStatusText = string.Empty;
            HasUnsavedRules = true;
            OnPropertyChanged(nameof(HasUnsavedRules));
            OnPropertyChanged(nameof(FilteredRules));
            OnPropertyChanged(nameof(RuleStatusText));
        }

        /// <summary>编辑已有规则：路径可能被改动，按原路径定位替换。</summary>
        public void UpdateRule(ScriptRule original, ScriptRule updated)
        {
            if (original is null || updated is null)
            {
                return;
            }

            var index = -1;
            for (var i = 0; i < _rules.Count; i++)
            {
                if (ReferenceEquals(_rules[i], original)
                    || string.Equals(_rules[i].Path, original.Path, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            if (index >= 0)
            {
                _rules[index] = updated;
            }
            else
            {
                _rules.Add(updated);
            }

            SelectedRule = updated;
            _ruleStatusText = string.Empty;
            HasUnsavedRules = true;
            OnPropertyChanged(nameof(HasUnsavedRules));
            OnPropertyChanged(nameof(FilteredRules));
            OnPropertyChanged(nameof(RuleStatusText));
        }

        public void DeleteRule(ScriptRule? rule)
        {
            if (rule is null)
            {
                return;
            }

            if (_rules.Remove(rule))
            {
                SelectedRule = null;
                _ruleStatusText = L("Rule.Deleted") + rule.Path;
                StatusText = _ruleStatusText;
                HasUnsavedRules = true;
                OnPropertyChanged(nameof(HasUnsavedRules));
                OnPropertyChanged(nameof(FilteredRules));
                OnPropertyChanged(nameof(RuleStatusText));
            }
        }

        /// <summary>把当前规则列表整体写入 rule.txt。</summary>
        public void SaveRules()
        {
            try
            {
                ScriptRuleStore.Save(_ruleFilePath, _rules);
                _ruleStatusText = L("Rule.Saved") + _ruleFilePath;
                StatusText = _ruleStatusText;
                DiagnosticsLog.Info("规则已保存：" + _ruleFilePath + "（" + _rules.Count + " 条）");
                HasUnsavedRules = false;
                OnPropertyChanged(nameof(HasUnsavedRules));
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Error("保存规则失败：" + ex.Message, ex);
                _ruleStatusText = L("Rule.SaveFailed") + ex.Message;
                StatusText = _ruleStatusText;
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Error("保存规则失败：" + ex.Message, ex);
                _ruleStatusText = L("Rule.SaveFailed") + ex.Message;
                StatusText = _ruleStatusText;
            }

            OnPropertyChanged(nameof(RuleStatusText));
        }

        public void Save()
        {
            try
            {
                _store.Save(_settings);
                _configStore.Save(_config);
                DiagnosticsLog.Info("配置已保存：" + _configStore.FilePath);
                StatusText = L("Main.Saved") + _configStore.FilePath;
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
            // 关联状态读一次，供徽标与关联页卡片共用
            _associationStatus = _associations.GetStatus();

            if (!string.IsNullOrEmpty(prefix))
            {
                StatusText = prefix!;
            }

            OnPropertyChanged(nameof(ExecutablePath));
            OnPropertyChanged(nameof(AssociationBadgeState));
            OnPropertyChanged(nameof(AssociationBadgeText));
            OnPropertyChanged(nameof(AssociationVerbsText));
            OnPropertyChanged(nameof(AssociationExtensionText));
            OnPropertyChanged(nameof(AssociationUserChoiceText));
            OnPropertyChanged(nameof(AssociationUserChoiceOverridden));
        }

        /// <summary>
        /// 一键识别：按 wiki 07 §3（PS5/PS7 探测顺序）与 08 §4（编辑器回退链）
        /// 识别解释器与编辑器，命中结果回填设置并自动保存。
        /// 只补空缺——已配置且文件仍存在的 PS / 编辑器保持原样，不被探测结果覆盖；
        /// 未命中且原配置为空（或已失效）时保留原值，不做破坏性写入。
        /// </summary>
        public void AutoDetect()
        {
            var locator = new PowerShellLocator();
            var editorLocator = new EditorLocator();

            // 原配置仍有效 → 一键识别不碰它
            var keptPs5Path = ExistingPath(_settings.CustomPs5Path);
            var keptPs7Path = ExistingPath(_settings.CustomPs7Path);
            var keptEditorPath = ExistingPath(_settings.CustomEditorPath);

            var ps5 = keptPs5Path is null ? locator.Locate(PowerShellEdition.Ps5) : null;
            var ps7 = keptPs7Path is null ? locator.Locate(PowerShellEdition.Ps7) : null;
            var editor = keptEditorPath is null ? editorLocator.Resolve() : null;

            var report = new StringBuilder();
            _suppressAutoSave = true;
            try
            {
                if (keptPs5Path is not null)
                {
                    AppendKept(report, L("Main.Detection.Ps5Label"), keptPs5Path);
                }
                else if (ps5 is null)
                {
                    report.Append(L("Main.Detection.Ps5Label")).Append("：").AppendLine(L("Main.Detection.NotFound"));
                }
                else
                {
                    CustomPs5Path = ps5.ExecutablePath;
                    report.Append(L("Main.Detection.Ps5Label")).Append("：").Append(ps5.ExecutablePath)
                        .Append("（").Append(ps5.ProbeSource).AppendLine("）");
                }

                if (keptPs7Path is not null)
                {
                    AppendKept(report, L("Main.Detection.Ps7Label"), keptPs7Path);
                }
                else if (ps7 is null)
                {
                    report.Append(L("Main.Detection.Ps7Label")).Append("：").AppendLine(L("Main.Detection.NotFound"));
                }
                else
                {
                    CustomPs7Path = ps7.ExecutablePath;
                    report.Append(L("Main.Detection.Ps7Label")).Append("：").Append(ps7.ExecutablePath)
                        .Append("（").Append(ps7.ProbeSource).AppendLine("）");
                }

                if (keptEditorPath is not null)
                {
                    AppendKept(report, L("Main.Detection.EditorLabel"), keptEditorPath);
                }
                else if (editor is null)
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
            StatusText = L("Main.Detection.Done");

            DiagnosticsLog.Info(
                "一键识别：" + DescribeDetection("PS5", keptPs5Path, ps5?.ExecutablePath)
                + " " + DescribeDetection("PS7", keptPs7Path, ps7?.ExecutablePath)
                + " " + DescribeDetection("编辑器", keptEditorPath, editor?.ExecutablePath));
        }

        /// <summary>探测结果行：原配置有效时写明"保留"，避免误以为被覆盖。</summary>
        private static void AppendKept(StringBuilder report, string label, string path) =>
            report.Append(label).Append("：").Append(path)
                .Append("（").Append(L("Main.Detection.Kept")).AppendLine("）");

        private static string DescribeDetection(string label, string? keptPath, string? detectedPath) =>
            label + "=" + (keptPath ?? detectedPath ?? "<未找到>") + (keptPath is null ? string.Empty : "（保留）");

        /// <summary>已配置且文件仍存在时返回原路径；空值、失效路径返回 null（允许重新探测）。</summary>
        private static string? ExistingPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return File.Exists(path) ? path : null;
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

        /// <summary>用默认程序打开 config.cfg，方便直接改 LogDays / Theme / Language。</summary>
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
                    "config.cfg 已保存：file=" + _configStore.FilePath
                    + " logDays=" + _config.LogDays.ToString(CultureInfo.InvariantCulture)
                    + " theme=" + _config.Theme
                    + " language=" + _config.Language);
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Error("保存 config.cfg 失败：" + ex.Message, ex);
                StatusText = L("Main.SaveFailed") + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Error("保存 config.cfg 失败：" + ex.Message, ex);
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

        /// <summary>程序版本号（来自程序集版本）；缺失时显示占位文案。</summary>
        public string AppVersionText => AppInfo.HasVersion ? AppInfo.DisplayVersion : L("Main.VersionMissing");

        /// <summary>当前语言包的版本号与文件名。</summary>
        public string LanguagePackVersionText
        {
            get
            {
                var loc = LocalizationManager.Instance;
                var file = Path.GetFileName(loc.PackFile);
                var version = loc.LoadedPackVersion;
                return version is null
                    ? L("Warning.NoVersion") + " · " + file
                    : "v" + version.Value.ToString(CultureInfo.InvariantCulture) + " · " + file;
            }
        }

        /// <summary>语言包缺少版本号（或低于程序期望版本）时，页签徽标与提醒卡片显示。</summary>
        public bool HasVersionWarning => LocalizationManager.Instance.IsPackOutdated;

        /// <summary>版本提醒正文（针对语言文件）；无提醒时为空。</summary>
        public string VersionWarningText
        {
            get
            {
                var loc = LocalizationManager.Instance;
                if (!loc.IsPackOutdated)
                {
                    return string.Empty;
                }

                return string.Format(
                    CultureInfo.CurrentCulture,
                    L("Warning.LanguagePackOutdated"),
                    Path.GetFileName(loc.PackFile),
                    loc.ExpectedPackVersion.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// 按内置中文重建 zh-cn.lang（等同于应用随程序发布的新版语言包）；
        /// 其他语言包不会被覆盖，仍需用户自行更新。
        /// </summary>
        public void RebuildLanguagePack()
        {
            var path = LocalizationManager.Instance.LanguageFilePath(DefaultStrings.DefaultLanguageCode);

            try
            {
                LanguageCatalog.WriteDefaultFile(path);
                DiagnosticsLog.Info("已重新生成语言包：" + path);
                LocalizationManager.Instance.Load(LocalizationManager.Instance.CurrentLanguage);
                StatusText = L("Main.LanguagePackRebuilt") + path;
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Error("重新生成语言包失败：" + ex.Message, ex);
                StatusText = L("Main.LanguagePackRebuildFailed") + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Error("重新生成语言包失败：" + ex.Message, ex);
                StatusText = L("Main.LanguagePackRebuildFailed") + ex.Message;
            }
        }

        private static string Flag(bool value) => value ? "√" : "×";

        private static string L(string key) => LocalizationManager.Instance.Get(key);

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
