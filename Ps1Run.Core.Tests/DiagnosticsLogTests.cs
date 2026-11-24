using System;
using System.IO;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.Core.Tests
{
    internal static class DiagnosticsLogTests
    {
        public static void RunAll()
        {
            WritesLevelSessionAndMessage();
            ReadsRecentLines();
            PrunesExpiredAndKeepsRecent();
            PrunesOnlyOnFirstRunOfDay();
        }

        private static string NewTempLogDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ps1run-logs-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void WritesLevelSessionAndMessage()
        {
            var dir = NewTempLogDirectory();
            var previous = DiagnosticsLog.LogDirectoryOverride;
            try
            {
                DiagnosticsLog.LogDirectoryOverride = dir;
                DiagnosticsLog.Info("测试信息：中文与路径 C:\\测试\\脚本.ps1");
                DiagnosticsLog.Warn("测试警告");
                DiagnosticsLog.Error("测试错误", new InvalidOperationException("模拟异常"));

                var text = File.ReadAllText(DiagnosticsLog.CurrentLogFile);
                Assert.True(text.Contains("[INFO]"), "应写入 INFO 级别");
                Assert.True(text.Contains("[WARN]"), "应写入 WARN 级别");
                Assert.True(text.Contains("[ERROR]"), "应写入 ERROR 级别");
                Assert.True(text.Contains("[s:"), "应写入会话 ID");
                Assert.True(text.Contains("[p:"), "应写入进程 ID");
                Assert.True(text.Contains("InvalidOperationException"), "异常类型应写入日志");
                Assert.True(text.Contains("模拟异常"), "异常消息应写入日志");
            }
            finally
            {
                DiagnosticsLog.LogDirectoryOverride = previous;
            }
        }

        private static void ReadsRecentLines()
        {
            var dir = NewTempLogDirectory();
            var previous = DiagnosticsLog.LogDirectoryOverride;
            try
            {
                DiagnosticsLog.LogDirectoryOverride = dir;
                for (var i = 0; i < 12; i++)
                {
                    DiagnosticsLog.Info("line-" + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture));
                }

                var recent = DiagnosticsLog.ReadRecent(3);
                Assert.True(recent.Contains("line-09"), "应包含倒数第 3 行");
                Assert.True(recent.Contains("line-10"), "应包含倒数第 2 行");
                Assert.True(recent.Contains("line-11"), "应包含最后一行");
                Assert.True(!recent.Contains("line-00"), "不应包含超出上限的旧行");
            }
            finally
            {
                DiagnosticsLog.LogDirectoryOverride = previous;
            }
        }

        private static void PrunesExpiredAndKeepsRecent()
        {
            var dir = NewTempLogDirectory();
            var previous = DiagnosticsLog.LogDirectoryOverride;
            try
            {
                DiagnosticsLog.LogDirectoryOverride = dir;
                var old = Path.Combine(dir, "20200101.log");
                var recent = Path.Combine(dir, "29991231.log");
                File.WriteAllText(old, "old");
                File.WriteAllText(recent, "recent");
                File.SetLastWriteTime(old, DateTime.Now.AddDays(-30));
                File.SetLastWriteTime(recent, DateTime.Now);

                DiagnosticsLog.Prune(7);

                Assert.True(!File.Exists(old), "过期日志应被删除");
                Assert.True(File.Exists(recent), "近期日志应保留");
            }
            finally
            {
                DiagnosticsLog.LogDirectoryOverride = previous;
            }
        }
        private static void PrunesOnlyOnFirstRunOfDay()
        {
            var dir = NewTempLogDirectory();
            var previous = DiagnosticsLog.LogDirectoryOverride;
            try
            {
                DiagnosticsLog.LogDirectoryOverride = dir;

                var old = Path.Combine(dir, "20200101.log");
                File.WriteAllText(old, "old");
                File.SetLastWriteTime(old, DateTime.Now.AddDays(-30));

                // 当天第一次运行：写今日日志 + 清理过期日志
                DiagnosticsLog.BeginSession("tests", string.Empty, 7);
                Assert.True(!File.Exists(old), "每天首次运行应清理过期日志");
                Assert.True(File.Exists(DiagnosticsLog.CurrentLogFile), "当天日志应被创建");

                // 同一天再启动一次：不再重复清理
                File.WriteAllText(old, "old-again");
                File.SetLastWriteTime(old, DateTime.Now.AddDays(-30));
                DiagnosticsLog.BeginSession("tests", string.Empty, 7);
                Assert.True(File.Exists(old), "同一天后续运行不应再次清理");

                var logText = File.ReadAllText(DiagnosticsLog.CurrentLogFile);
                Assert.True(logText.Contains("每日清理"), "应记录每日清理结果");
            }
            finally
            {
                DiagnosticsLog.LogDirectoryOverride = previous;
            }
        }
    }
}
