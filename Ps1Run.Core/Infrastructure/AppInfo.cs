using System;
using System.Reflection;

namespace Ps1Run.Core.Infrastructure
{
    /// <summary>
    /// 程序版本信息（来自程序集版本，由 Directory.Build.props 的 Version 派生）。
    /// 版本号缺失（程序集未带版本、或全为 0）时 <see cref="HasVersion"/> 为 false，
    /// 界面据此给出提醒，避免"看起来正常但无法判断版本"的状态。
    /// </summary>
    public static class AppInfo
    {
        private static readonly Version? RawVersion = ResolveVersion();

        /// <summary>程序集版本；无法解析时为 null。</summary>
        public static Version? AssemblyVersion => RawVersion;

        /// <summary>是否存在有效版本号（0.0.0.0 视为缺失）。</summary>
        public static bool HasVersion =>
            RawVersion is not null
            && (RawVersion.Major > 0 || RawVersion.Minor > 0 || RawVersion.Build > 0 || RawVersion.Revision > 0);

        /// <summary>界面显示用版本号（去掉末尾的 .0，如 0.1.0.0 → 0.1.0）；缺失时为空串。</summary>
        public static string DisplayVersion => HasVersion ? Format(RawVersion!) : string.Empty;

        private static string Format(Version version)
        {
            var text = version.ToString();
            while (text.EndsWith(".0", StringComparison.Ordinal) && text.IndexOf('.') != text.LastIndexOf('.'))
            {
                text = text.Substring(0, text.Length - 2);
            }

            return text;
        }

        private static Version? ResolveVersion()
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly;
                return assembly.GetName().Version;
            }
            catch (Exception)
            {
                // 反射受限或程序集信息损坏时按"无版本号"处理
                return null;
            }
        }

        /// <summary>版本号文本（缺失时显示占位符），供日志使用。</summary>
        public static string Describe() =>
            HasVersion ? DisplayVersion : "<无版本号>";
    }
}
