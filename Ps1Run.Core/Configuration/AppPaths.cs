using System;
using System.IO;

namespace Ps1Run.Core.Configuration
{
    /// <summary>
    /// 程序根目录与四个约定路径：config.lang（程序配置 + 运行设置合并为一份）、rule.txt、lang\、log\（wiki 02 F5.x / F6.x）。
    /// 默认取 exe 所在目录（绿色软件形态）；该目录不可写时（典型：装在 Program Files 且无提权）
    /// 三者一起退化到 %APPDATA%\Ps1Run，保证它们始终落在同一个可写根下。
    /// </summary>
    public static class AppPaths
    {
        private const string AppFolderName = "Ps1Run";
        private static readonly object SyncRoot = new object();
        private static string? _root;
        private static bool _fallback;

        /// <summary>软件根目录（带结尾分隔符，可直接拼接）。</summary>
        public static string RootDirectory
        {
            get
            {
                lock (SyncRoot)
                {
                    return _root ??= ResolveRoot();
                }
            }
        }

        /// <summary>是否已退化到 %APPDATA% 根目录。</summary>
        public static bool IsFallback
        {
            get
            {
                lock (SyncRoot)
                {
                    var unused = RootDirectory;
                    return _fallback;
                }
            }
        }

        /// <summary>唯一配置文件：程序配置（日志 / 主题 / 语言）与运行设置（运行方式 / 解释器 / 编辑器 / 规则标准）同文件。</summary>
        public static string ConfigFile => Path.Combine(RootDirectory, "config.lang");

        /// <summary>脚本规则文件（5 列文本：路径|动作|大小|修改时间|HASH）。</summary>
        public static string RuleFile => Path.Combine(RootDirectory, "rule.txt");

        public static string LanguageDirectory => Path.Combine(RootDirectory, "lang");

        public static string LogDirectory => Path.Combine(RootDirectory, "log");

        /// <summary>仅用于测试注入临时根目录；生产代码不要设置。</summary>
        public static void OverrideRoot(string? root)
        {
            lock (SyncRoot)
            {
                _root = string.IsNullOrEmpty(root) ? null : root;
                _fallback = false;
            }
        }

        private static string ResolveRoot()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (CanWrite(baseDirectory))
            {
                return baseDirectory;
            }

            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppFolderName);

            // 先赋值再写日志：日志目录本身依赖 RootDirectory，否则会递归
            _root = fallback;
            _fallback = true;
            Infrastructure.DiagnosticsLog.Warn(
                "程序目录不可写，配置/日志/语言文件改用：" + fallback + "（原目录：" + baseDirectory + "）");
            return fallback;
        }

        private static bool CanWrite(string directory)
        {
            try
            {
                var probe = Path.Combine(directory, ".ps1run-write-probe.tmp");
                File.WriteAllText(probe, "probe");
                File.Delete(probe);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }
    }
}
