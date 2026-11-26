using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Ps1Run.Core.Configuration;
using Ps1Run.Core.Infrastructure;
using Ps1Run.Core.Localization;

namespace Ps1Run.App.Localization
{
    /// <summary>
    /// 运行时文案容器（wiki 02 F7.x）。
    /// 语言包来自 &lt;软件根目录&gt;\lang\&lt;code&gt;.lang；缺键回退内置中文，再回退键名，
    /// 保证任何情况下界面都不会空白。切换语言时刷新所有 <see cref="Translation"/>，
    /// 已绑定到界面的文字会立即更新，无需重启。
    /// </summary>
    public sealed class LocalizationManager : INotifyPropertyChanged
    {
        private readonly List<WeakReference<Translation>> _tracked = new List<WeakReference<Translation>>();
        private readonly object _syncRoot = new object();
        private Dictionary<string, string>? _entries;

        private LocalizationManager()
        {
        }

        public static LocalizationManager Instance { get; } = new LocalizationManager();

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>当前语言代码（如 zh-cn）。</summary>
        public string CurrentLanguage { get; private set; } = DefaultStrings.DefaultLanguageCode;

        /// <summary>当前语言包的版本号；语言文件缺失或无版本号时为 null。</summary>
        public int? LoadedPackVersion { get; private set; }

        /// <summary>程序期望的语言包版本。</summary>
        public int ExpectedPackVersion => LanguageCatalog.CurrentVersion;

        /// <summary>
        /// 当前语言包是否缺少版本号或低于程序期望版本。
        /// 只影响提示，不影响加载：语言包照常使用，缺键回退内置中文。
        /// </summary>
        public bool IsPackOutdated =>
            LoadedPackVersion is null || LoadedPackVersion.Value < LanguageCatalog.CurrentVersion;

        /// <summary>当前语言包的完整路径。</summary>
        public string PackFile => LanguageFilePath(CurrentLanguage);

        /// <summary>语法糖：Loc["Button.Save"]。</summary>
        public string this[string key] => Get(key);

        public static string Loc(string key) => Instance.Get(key);

        /// <summary>取文案：语言包 → 内置中文 → 键名。</summary>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            var entries = _entries;
            if (entries is not null && entries.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (DefaultStrings.ZhCn.TryGetValue(key, out var fallback) && !string.IsNullOrEmpty(fallback))
            {
                return fallback;
            }

            return key;
        }

        /// <summary>扫描当前 lang 目录，返回下拉框可选语言。</summary>
        public IReadOnlyList<LanguageOption> AvailableLanguages()
        {
            try
            {
                LanguageCatalog.EnsureDefaultFiles(AppPaths.LanguageDirectory);
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Warn("生成默认语言包失败：" + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Warn("生成默认语言包失败：" + ex.Message);
            }

            return LanguageCatalog.Discover(AppPaths.LanguageDirectory);
        }

        public string LanguageFilePath(string code) =>
            LanguageCatalog.GetFilePath(AppPaths.LanguageDirectory, code);

        /// <summary>加载并应用语言包；文件缺失或损坏时回退内置中文（不阻断启动）。</summary>
        public void Load(string? code)
        {
            var normalized = LanguageCatalog.Normalize(code);
            Dictionary<string, string>? entries = null;
            int? packVersion = null;

            try
            {
                LanguageCatalog.EnsureDefaultFiles(AppPaths.LanguageDirectory);
                var path = LanguageFilePath(normalized);
                if (LanguageCatalog.TryLoad(path, out var loaded, out var version, out var error) && loaded is not null)
                {
                    entries = loaded;
                    packVersion = version;
                }
                else
                {
                    DiagnosticsLog.Warn("语言包加载失败，回退内置中文：" + (error ?? "未知原因"));
                }
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Warn("语言包加载失败，回退内置中文：" + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Warn("语言包加载失败，回退内置中文：" + ex.Message);
            }

            _entries = entries;
            LoadedPackVersion = packVersion;
            CurrentLanguage = normalized;

            if (IsPackOutdated)
            {
                DiagnosticsLog.Warn(
                    "语言包缺少版本号或版本较旧：file=" + LanguageFilePath(normalized)
                    + " version=" + (packVersion?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<无>")
                    + " expected=" + LanguageCatalog.CurrentVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "（仍按原逻辑加载，缺键回退中文）");
            }

            RefreshTranslations();
            DiagnosticsLog.Info(
                "已应用语言：" + normalized
                + "（条目 " + (entries?.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "，文件 " + LanguageFilePath(normalized) + "）");
        }

        internal void Track(Translation translation)
        {
            lock (_syncRoot)
            {
                _tracked.Add(new WeakReference<Translation>(translation));
            }
        }

        private void RefreshTranslations()
        {
            List<Translation> alive = new List<Translation>();
            lock (_syncRoot)
            {
                for (var i = _tracked.Count - 1; i >= 0; i--)
                {
                    if (_tracked[i].TryGetTarget(out var target))
                    {
                        alive.Add(target);
                    }
                    else
                    {
                        _tracked.RemoveAt(i);
                    }
                }
            }

            foreach (var translation in alive)
            {
                translation.Refresh();
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    /// <summary>
    /// 一条被界面绑定的文案。语言切换时由 <see cref="LocalizationManager"/> 主动刷新，
    /// 因此不依赖绑定路径语法（键名里的点号不会出问题）。
    /// </summary>
    public sealed class Translation : DependencyObject
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value),
            typeof(string),
            typeof(Translation),
            new PropertyMetadata(string.Empty));

        public Translation(string key)
        {
            Key = key ?? string.Empty;
            Value = LocalizationManager.Instance.Get(Key);
            LocalizationManager.Instance.Track(this);
        }

        public string Key { get; }

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            private set => SetValue(ValueProperty, value);
        }

        internal void Refresh() => Value = LocalizationManager.Instance.Get(Key);
    }
}
