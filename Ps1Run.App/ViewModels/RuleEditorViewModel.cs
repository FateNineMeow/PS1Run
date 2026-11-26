using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Ps1Run.App.Localization;
using Ps1Run.Core.Dispatch;
using Ps1Run.Core.Rules;
using Ps1Run.Core.Settings;

namespace Ps1Run.App.ViewModels
{
    /// <summary>独立规则编辑弹窗：支持选择 .ps1 文件或直接输入路径，按全局标准预览指纹。</summary>
    public sealed class RuleEditorViewModel : INotifyPropertyChanged
    {
        private readonly AppSettings _settings;
        private readonly IReadOnlyList<ScriptRule> _existing;
        private readonly ScriptRule? _editing;
        private string _path;
        private int _actionIndex;
        private string _error = string.Empty;

        public RuleEditorViewModel(
            AppSettings settings,
            IEnumerable<ScriptRule> existing,
            ScriptRule? editing = null)
        {
            _settings = settings ?? AppSettings.Default;
            _existing = existing?.ToList() ?? new List<ScriptRule>();
            _editing = editing;
            _path = editing?.Path ?? string.Empty;
            _actionIndex = editing?.Action == LaunchAction.Edit ? 1 : 0;
            RefreshPreview();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ScriptRule? ResultRule { get; private set; }

        public string WindowTitle => L(_editing is null ? "Rule.EditorTitle.Add" : "Rule.EditorTitle.Edit");

        public bool IsOverwrite
        {
            get
            {
                var normalized = ScriptRuleFingerprint.Normalize(_path);
                return _existing.Any(r =>
                    !ReferenceEquals(r, _editing)
                    && string.Equals(r.Path, normalized, StringComparison.OrdinalIgnoreCase));
            }
        }

        public string Path
        {
            get => _path;
            set
            {
                var normalized = value ?? string.Empty;
                if (_path == normalized)
                {
                    return;
                }

                _path = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOverwrite));
                RefreshPreview();
            }
        }

        public int ActionIndex
        {
            get => _actionIndex;
            set
            {
                if (_actionIndex == value)
                {
                    return;
                }

                _actionIndex = value;
                OnPropertyChanged();
            }
        }

        public string ErrorMessage => _error;

        public bool HasError => _error.Length > 0;

        public string CriteriaSummary
        {
            get
            {
                var parts = string.Empty;
                if (_settings.RuleMatchSize)
                {
                    parts += " + " + L("Label.RuleSize");
                }

                if (_settings.RuleMatchModifiedTime)
                {
                    parts += " + " + L("Label.RuleModifiedTime");
                }

                if (_settings.RuleMatchHash)
                {
                    parts += " + " + L("Label.RuleHash");
                }

                return string.Format(CultureInfo.CurrentCulture, L("Rule.CriteriaSummary"), parts, string.Empty, string.Empty);
            }
        }

        public string PreviewText { get; private set; } = string.Empty;

        public void PickFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = L("Rule.EditorTitle.Add"),
                Filter = L("Rule.Filter.Exe"),
                CheckFileExists = true,
                FileName = _path,
            };

            if (dialog.ShowDialog() == true)
            {
                Path = dialog.FileName;
            }
        }

        /// <summary>校验路径并生成规则；失败时通过 error 返回原因。</summary>
        public bool TryBuild(out ScriptRule? rule, out string error)
        {
            rule = null;
            error = string.Empty;

            if (!ScriptRuleFingerprint.IsPowerShellScript(_path))
            {
                error = L("Rule.InvalidExtension");
                SetError(error);
                return false;
            }

            if (!File.Exists(_path))
            {
                error = L("Rule.FileMissing");
                SetError(error);
                return false;
            }

            try
            {
                rule = ScriptRuleFingerprint.Create(
                    _path,
                    _actionIndex == 1 ? LaunchAction.Edit : LaunchAction.Run,
                    _settings);
                SetError(string.Empty);
                return true;
            }
            catch (IOException ex)
            {
                error = ex.Message;
                SetError(error);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = ex.Message;
                SetError(error);
                return false;
            }
        }

        public void Accept(ScriptRule rule) => ResultRule = rule;

        private void RefreshPreview()
        {
            if (!ScriptRuleFingerprint.IsPowerShellScript(_path) || !File.Exists(_path))
            {
                PreviewText = string.Empty;
                OnPropertyChanged(nameof(PreviewText));
                return;
            }

            try
            {
                var probe = ScriptRuleFingerprint.Create(
                    _path,
                    _actionIndex == 1 ? LaunchAction.Edit : LaunchAction.Run,
                    _settings);
                PreviewText = string.Format(
                    CultureInfo.CurrentCulture,
                    L("Rule.FingerprintPreview"),
                    probe.SizeText,
                    probe.ModifiedTimeUtc,
                    probe.HashText);
            }
            catch (IOException)
            {
                PreviewText = string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                PreviewText = string.Empty;
            }

            OnPropertyChanged(nameof(PreviewText));
        }

        private void SetError(string message)
        {
            _error = message;
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(HasError));
        }

        private static string L(string key) => LocalizationManager.Instance.Get(key);

        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
