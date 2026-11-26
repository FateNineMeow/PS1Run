using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Ps1Run.Core.Infrastructure
{
    /// <summary>日志级别（wiki 02 F6.1）。</summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
    }

    /// <summary>
    /// 轻量日志（wiki 02 F6.1）。
    /// 位置：&lt;软件根目录&gt;\log\yyyyMMdd.log，按天滚动；
    /// 保留天数由 config.lang 的 LogDays 决定（默认 7 天），每天首次运行时自动清理。
    /// 每条日志带级别、会话 ID 与进程 ID，便于区分多次双击/多进程启动。
    /// 日志失败一律吞掉，绝不影响主流程。
    /// </summary>
    public static class DiagnosticsLog
    {
        private const int DefaultKeepDays = 7;
        private const int DefaultRecentLines = 200;
        private static readonly object SyncRoot = new object();
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly string SessionId =
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture).Substring(0, 8).ToUpperInvariant();

        /// <summary>
        /// 仅用于测试注入临时目录；生产代码不要设置。
        /// </summary>
        public static string? LogDirectoryOverride { get; set; }

        public static string LogDirectory =>
            LogDirectoryOverride ?? Configuration.AppPaths.LogDirectory;

        public static string CurrentLogFile =>
            Path.Combine(
                LogDirectory,
                DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");

        /// <summary>
        /// 启动一次会话：记录进程上下文；若当天日志尚不存在（= 每天第一次运行），
        /// 顺带清理早于保留天数的日志。
        /// 必须在 App.OnStartup 最早调用，保证后续所有日志都能归到同一会话。
        /// </summary>
        public static void BeginSession(string component, string arguments, int keepDays = DefaultKeepDays)
        {
            var firstRunToday = !File.Exists(CurrentLogFile);
            var processId = SafeProcessId();
            Info(
                "会话开始：component=" + component
                + " session=" + SessionId
                + " pid=" + processId.ToString(CultureInfo.InvariantCulture)
                + " exe=" + SafeEnvironmentProcessPath()
                + " args=[" + Sanitize(arguments ?? string.Empty) + "]" 
                + " keepDays=" + keepDays.ToString(CultureInfo.InvariantCulture));

            if (!firstRunToday)
            {
                return;
            }

            var removed = Prune(keepDays);
            Info(
                "每日清理：保留 " + keepDays.ToString(CultureInfo.InvariantCulture)
                + " 天，删除 " + removed.ToString(CultureInfo.InvariantCulture)
                + " 个过期日志文件，目录=" + LogDirectory);
        }

        public static void EndSession(int exitCode)
        {
            Info(
                "会话结束：session=" + SessionId
                + " exitCode=" + exitCode.ToString(CultureInfo.InvariantCulture));
        }

        public static void Debug(string message) => Write(LogLevel.Debug, message);

        public static void Info(string message) => Write(LogLevel.Info, message);

        public static void Warn(string message) => Write(LogLevel.Warn, message);

        public static void Error(string message) => Write(LogLevel.Error, message);

        public static void Error(string message, Exception exception)
        {
            if (exception is null)
            {
                Error(message);
                return;
            }

            Write(LogLevel.Error, message + " | 异常=" + DescribeException(exception));
        }

        public static void Write(LogLevel level, string message)
        {
            var line = string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] [s:{2}] [p:{3}] {4}",
                DateTime.Now,
                level.ToString().ToUpperInvariant(),
                SessionId,
                SafeProcessId().ToString(CultureInfo.InvariantCulture),
                Sanitize(message ?? string.Empty));

            try
            {
                lock (SyncRoot)
                {
                    Directory.CreateDirectory(LogDirectory);
                    File.AppendAllText(CurrentLogFile, line + Environment.NewLine, Utf8NoBom);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        /// <summary>保留原有的 category 写法（兼容早期调用）。</summary>
        public static void Write(string category, string message)
        {
            var level = category.ToUpperInvariant() switch
            {
                "ERROR" => LogLevel.Error,
                "WARN" => LogLevel.Warn,
                "DEBUG" => LogLevel.Debug,
                _ => LogLevel.Info,
            };
            Write(level, message);
        }

        /// <summary>
        /// 读取当日日志最后 N 行；日志不存在或读取失败时返回提示文本，不抛异常。
        /// </summary>
        public static string ReadRecent(int maxLines = DefaultRecentLines)
        {
            if (maxLines <= 0)
            {
                return string.Empty;
            }

            try
            {
                lock (SyncRoot)
                {
                    if (!File.Exists(CurrentLogFile))
                    {
                        return "（今日暂无日志：" + CurrentLogFile + "）";
                    }

                    var lines = File.ReadAllLines(CurrentLogFile, Encoding.UTF8);
                    var start = Math.Max(0, lines.Length - maxLines);
                    var selected = lines.Skip(start).ToArray();
                    return "日志文件：" + CurrentLogFile + Environment.NewLine
                        + "会话 ID：" + SessionId + Environment.NewLine
                        + string.Join(Environment.NewLine, selected);
                }
            }
            catch (IOException ex)
            {
                return "（读取日志失败：" + ex.Message + "）";
            }
            catch (UnauthorizedAccessException ex)
            {
                return "（读取日志失败：" + ex.Message + "）";
            }
        }

        /// <summary>删除超过保留天数的旧日志，返回实际删除的文件数。</summary>
        public static int Prune(int keepDays = DefaultKeepDays)
        {
            var removed = 0;
            if (keepDays <= 0)
            {
                return removed;
            }

            try
            {
                lock (SyncRoot)
                {
                    if (!Directory.Exists(LogDirectory))
                    {
                        return removed;
                    }

                    var threshold = DateTime.Now.Date.AddDays(-keepDays);
                    foreach (var file in Directory.GetFiles(LogDirectory, "*.log"))
                    {
                        if (File.GetLastWriteTime(file) < threshold)
                        {
                            File.Delete(file);
                            removed++;
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return removed;
        }

        private static string Sanitize(string value) =>
            value.Replace("\r", "\\r").Replace("\n", "\\n");

        private static string DescribeException(Exception exception) =>
            exception.GetType().FullName + ": " + Sanitize(exception.Message);

        private static int SafeProcessId()
        {
            try
            {
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                return process.Id;
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }

        private static string SafeEnvironmentProcessPath()
        {
            try
            {
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                return process.MainModule?.FileName ?? "<未知>";
            }
            catch (InvalidOperationException)
            {
                return "<未知>";
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return "<未知>";
            }
        }
    }
}
