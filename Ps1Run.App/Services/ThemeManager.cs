using System;
using System.Security;
using System.Windows;
using Microsoft.Win32;
using Ps1Run.Core.Configuration;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.App.Services
{
    /// <summary>
    /// 主题切换（wiki 02 F7.x）：把 App.Resources 里的主题字典换成 Light/Dark。
    /// Auto 时读注册表 AppsUseLightTheme 判断系统深浅色；切换后所有 DynamicResource 立即重绘。
    /// </summary>
    public static class ThemeManager
    {
        /// <summary>配置里的原始主题设置。</summary>
        public static AppTheme Current { get; private set; } = AppTheme.Auto;

        /// <summary>实际生效的是否为深色。</summary>
        public static bool IsDark { get; private set; }

        public static void Apply(AppTheme theme)
        {
            Current = theme;

            var dark = theme == AppTheme.Dark || (theme == AppTheme.Auto && IsSystemDark());
            IsDark = dark;

            var app = Application.Current;
            if (app is null)
            {
                return;
            }

            var uri = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
            var dictionaries = app.Resources.MergedDictionaries;

            for (var i = dictionaries.Count - 1; i >= 0; i--)
            {
                if (IsThemeDictionary(dictionaries[i]))
                {
                    dictionaries.RemoveAt(i);
                }
            }

            dictionaries.Insert(0, new ThemeDictionary { Source = uri });

            DiagnosticsLog.Info("已应用主题：设置=" + theme + " 实际=" + (dark ? "Dark" : "Light"));
        }

        /// <summary>识别主题字典：既包含运行时替换的，也包含 App.xaml 里合并的设计期默认值。</summary>
        private static bool IsThemeDictionary(ResourceDictionary dictionary)
        {
            if (dictionary is ThemeDictionary)
            {
                return true;
            }

            var source = dictionary.Source?.OriginalString;
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            return source!.EndsWith("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>读 HKCU 的 AppsUseLightTheme；读不到按浅色处理。</summary>
        public static bool IsSystemDark()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int value)
                {
                    return value == 0;
                }
            }
            catch (SecurityException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (System.IO.IOException)
            {
            }

            return false;
        }
    }

    /// <summary>用于识别「当前合并进来的主题字典」，换主题时只替换它。</summary>
    internal sealed class ThemeDictionary : ResourceDictionary
    {
    }
}
