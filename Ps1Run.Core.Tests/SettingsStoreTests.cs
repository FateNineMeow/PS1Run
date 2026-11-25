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
            ImportsLegacySettingsAndDeletesIt();
            CorruptedFileFallsBackAndBacksUp();
        }

        private static string NewTempPath()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ps1run-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
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
            Assert.True(File.ReadAllText(path).StartsWith("{"), "文件应为 JSON 对象");
        }

        private static void SharesSingleConfigFile()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ps1run-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "config.json");

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
            Assert.True(text.Contains("\"Version\""), "合并后的文件应带版本号");
            Assert.True(text.Contains("\"RunMode\""), "版本号与运行设置应在同一份文件里");
        }

        private static void ImportsLegacySettingsAndDeletesIt()
        {
            var legacy = NewTempPath();
            var config = Path.Combine(Path.GetDirectoryName(legacy)!, "config.json");
            File.WriteAllText(legacy,
                "{\"RunMode\":\"Edit\",\"PowerShellPolicy\":\"Ps7\",\"CustomEditorPath\":\"D:\\\\Tools\\\\Code.exe\"}");

            var state = ConfigDocument.Load(config, importLegacySettings: true, legacySettingsFile: legacy);

            Assert.Equal(RunMode.Edit, state.Settings.RunMode, "首次读取应合并旧 RunMode");
            Assert.Equal(PowerShellPolicy.Ps7, state.Settings.PowerShellPolicy, "应合并旧解释器策略");
            Assert.Equal(@"D:\Tools\Code.exe", state.Settings.CustomEditorPath, "应合并旧编辑器路径");
            Assert.True(File.ReadAllText(config).Contains("\"Version\""), "合并后 config.json 应带版本号");
            Assert.True(!File.Exists(legacy), "合并成功后旧 settings.json 应被删除");

            // 再次加载不应重建旧文件
            ConfigDocument.Load(config, importLegacySettings: true, legacySettingsFile: legacy);
            Assert.True(!File.Exists(legacy), "后续加载不应重建旧设置文件");
        }

        private static void CorruptedFileFallsBackAndBacksUp()
        {
            var path = NewTempPath();
            File.WriteAllText(path, "this is not json");
            var loaded = new SettingsStore(path).Load();

            Assert.Equal(RunMode.Ask, loaded.RunMode, "损坏时回退默认值");
            var backups = Directory.GetFiles(Path.GetDirectoryName(path)!, "settings.json.bad.*");
            Assert.True(backups.Length == 1, "损坏文件应被备份一次");
        }
    }
}
