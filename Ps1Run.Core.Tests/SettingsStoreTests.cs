using System;
using System.IO;
using Ps1Run.Core.Configuration;
using Ps1Run.Core.Settings;

namespace Ps1Run.Core.Tests
{
    internal static class SettingsStoreTests
    {
        public static void RunAll()
        {
            RoundTrip();
            SharesSingleConfigFile();
            RuleCriteriaRoundTrip();
            CorruptedFileFallsBackAndBacksUp();
            IgnoresLegacyJsonFiles();
        }

        private static string NewTempPath()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ps1run-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "config.lang");
        }

        private static void RoundTrip()
        {
            var path = NewTempPath();
            var original = new AppSettings
            {
                RunMode = RunMode.Edit,
                PowerShellPolicy = PowerShellPolicy.Ps7,
                CustomPs5Path = @"C:\Program Files\PowerShell\powershell.exe",
                CustomPs7Path = @"D:\Tools\pwsh ""preview"".exe",
                CustomEditorPath = @"D:\Soft\Code\VS Code\Code.exe",
            };

            var store = new SettingsStore(path);
            store.Save(original);
            var loaded = store.Load();

            Assert.Equal(RunMode.Edit, loaded.RunMode, "RunMode 往返");
            Assert.Equal(PowerShellPolicy.Ps7, loaded.PowerShellPolicy, "PowerShellPolicy 往返");
            Assert.Equal(original.CustomPs7Path, loaded.CustomPs7Path, "含引号路径往返");
            Assert.Equal(original.CustomEditorPath, loaded.CustomEditorPath, "编辑器路径往返");
            Assert.True(File.ReadAllText(path).Contains("RunMode: \"Edit\","), "设置应以键值文本保存");
        }

        private static void SharesSingleConfigFile()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ps1run-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "config.lang");

            // 先写运行设置，再写程序配置：两者共用一个文件，互不覆盖
            new SettingsStore(path).Save(new AppSettings
            {
                RunMode = RunMode.Edit,
                PowerShellPolicy = PowerShellPolicy.Ps5,
                CustomEditorPath = @"D:\Soft\Code\Code.exe",
            });

            new ConfigStore(path).Save(new AppConfig
            {
                LogDays = 14,
                Theme = AppTheme.Dark,
                Language = "en-us",
            });

            var settings = new SettingsStore(path).Load();
            var config = new ConfigStore(path).Load();

            Assert.Equal(RunMode.Edit, settings.RunMode, "运行设置应保留在同一文件里");
            Assert.Equal(PowerShellPolicy.Ps5, settings.PowerShellPolicy, "解释器策略应保留");
            Assert.Equal(@"D:\Soft\Code\Code.exe", settings.CustomEditorPath, "编辑器路径应保留");
            Assert.Equal(14, config.LogDays, "日志天数应保留");
            Assert.Equal(AppTheme.Dark, config.Theme, "主题应保留");
            Assert.Equal("en-us", config.Language, "语言应保留");

            var text = File.ReadAllText(path);
            Assert.True(text.Contains("Version: 2,"), "合并后的文件应带版本号 2");
            Assert.True(text.Contains("RunMode: \"Edit\","), "版本号与运行设置应在同一份文件里");
        }

        private static void RuleCriteriaRoundTrip()
        {
            var path = NewTempPath();
            var settings = new SettingsStore(path).Load();
            Assert.True(settings.RuleMatchSize, "大小默认开启");
            Assert.True(settings.RuleMatchModifiedTime, "修改时间默认开启");
            Assert.True(!settings.RuleMatchHash, "HASH 默认关闭");

            var updated = settings with { RuleMatchSize = false, RuleMatchHash = true };
            new SettingsStore(path).Save(updated);

            var reloaded = new SettingsStore(path).Load();
            Assert.True(!reloaded.RuleMatchSize, "大小开关应往返");
            Assert.True(reloaded.RuleMatchHash, "HASH 开关应往返");

            var text = File.ReadAllText(path);
            Assert.True(text.Contains("RuleMatchSize: 0,"), "关闭应写 0");
            Assert.True(text.Contains("RuleMatchHash: 1,"), "开启应写 1");
        }

        private static void CorruptedFileFallsBackAndBacksUp()
        {
            var path = NewTempPath();
            File.WriteAllText(path, "this is not key-value text");
            var loaded = new SettingsStore(path).Load();

            Assert.Equal(RunMode.Ask, loaded.RunMode, "损坏时回退默认值");
            var backups = Directory.GetFiles(Path.GetDirectoryName(path)!, "config.lang.bad.*");
            Assert.True(backups.Length == 1, "损坏文件应被备份一次");
        }

        private static void IgnoresLegacyJsonFiles()
        {
            var dir = Path.GetDirectoryName(NewTempPath())!;
            var legacy = Path.Combine(dir, "settings.json");
            var config = Path.Combine(dir, "config.lang");
            File.WriteAllText(legacy, "{\"RunMode\":\"Edit\"}");

            var state = ConfigDocument.Load(config);

            Assert.Equal(RunMode.Ask, state.Settings.RunMode, "旧 settings.json 不应被读取或合并");
            Assert.True(File.Exists(legacy), "旧 settings.json 不应被自动删除");
        }
    }
}
