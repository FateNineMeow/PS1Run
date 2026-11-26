using System;

namespace Ps1Run.Core.Configuration
{
    /// <summary>界面主题（需求：深色 / 浅色 / 跟随系统）。</summary>
    public enum AppTheme
    {
        Auto = 0,
        Light = 1,
        Dark = 2,
    }

    /// <summary>
    /// config.lang 中的程序配置部分：日志保留天数、主题、界面语言。
    /// 与 AppSettings（运行方式 / 解释器 / 编辑器 / 规则标准）同文件但由不同门面保存，互不覆盖。
    /// </summary>
    public sealed record AppConfig
    {
        public const int MinLogDays = 1;
        public const int MaxLogDays = 365;
        public const int DefaultLogDays = 7;
        public const string DefaultLanguage = "zh-cn";

        public int LogDays { get; init; } = DefaultLogDays;

        public AppTheme Theme { get; init; } = AppTheme.Auto;

        public string Language { get; init; } = DefaultLanguage;

        public static AppConfig Default { get; } = new AppConfig();

        /// <summary>把任意来源的配置收敛到合法范围，避免脏值破坏界面或清理逻辑。</summary>
        public static AppConfig Normalize(AppConfig? config)
        {
            var source = config ?? Default;

            var days = source.LogDays;
            if (days < MinLogDays || days > MaxLogDays)
            {
                days = DefaultLogDays;
            }

            var theme = Enum.IsDefined(typeof(AppTheme), source.Theme) ? source.Theme : AppTheme.Auto;

            var language = string.IsNullOrWhiteSpace(source.Language)
                ? DefaultLanguage
                : source.Language.Trim().ToLowerInvariant();

            return source with { LogDays = days, Theme = theme, Language = language };
        }
    }
}
