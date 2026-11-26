using System;
using System.IO;
using Ps1Run.Core.Configuration;

namespace Ps1Run.Core.Tests
{
    internal static class ConfigStoreTests
    {
        public static void RunAll()
        {
            CreatesDefaultFileOnFirstLoad();
            RoundTripsValues();
            NormalizesOutOfRange();
            RecoversFromCorruptedFile();
        }

        private static string NewTempDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ps1run-config-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void CreatesDefaultFileOnFirstLoad()
        {
            var dir = NewTempDirectory();
            var path = Path.Combine(dir, "config.cfg");
            var store = new ConfigStore(path);

            var config = store.Load();

            Assert.Equal(7, config.LogDays, "默认日志天数为 7");
            Assert.Equal(AppTheme.Auto, config.Theme, "默认主题为 Auto");
            Assert.Equal("zh-cn", config.Language, "默认语言为 zh-cn");
            Assert.True(File.Exists(path), "首次读取应生成 config.cfg");

            var text = File.ReadAllText(path);
            Assert.True(text.Contains("Version: 2,"), "生成的配置应含版本号 2");
            Assert.True(text.Contains("LogDays: 7,"), "生成的文本应包含 LogDays");
            Assert.True(text.Contains("Theme: \"Auto\","), "生成的文本应包含 Theme");
            Assert.True(text.Contains("Language: \"zh-cn\","), "生成的文本应包含 Language");
            Assert.True(text.Contains("RuleMatchSize: 1,"), "大小标准默认开启");
            Assert.True(text.Contains("RuleMatchHash: 0,"), "HASH 标准默认关闭");
        }

        private static void RoundTripsValues()
        {
            var dir = NewTempDirectory();
            var path = Path.Combine(dir, "config.cfg");
            var store = new ConfigStore(path);

            store.Save(new AppConfig { LogDays = 30, Theme = AppTheme.Dark, Language = "en-us" });
            var loaded = new ConfigStore(path).Load();

            Assert.Equal(30, loaded.LogDays, "LogDays 应往返一致");
            Assert.Equal(AppTheme.Dark, loaded.Theme, "Theme 应往返一致");
            Assert.Equal("en-us", loaded.Language, "Language 应往返一致");
        }

        private static void NormalizesOutOfRange()
        {
            var dir = NewTempDirectory();
            var path = Path.Combine(dir, "config.cfg");
            var store = new ConfigStore(path);

            store.Save(new AppConfig { LogDays = 0, Theme = AppTheme.Light, Language = "  EN-US  " });
            var loaded = store.Load();

            Assert.Equal(7, loaded.LogDays, "越界 LogDays 应收敛为默认 7");
            Assert.Equal("en-us", loaded.Language, "语言代码应去空格并小写");
        }

        private static void RecoversFromCorruptedFile()
        {
            var dir = NewTempDirectory();
            var path = Path.Combine(dir, "config.cfg");
            File.WriteAllText(path, "这不是键值文本");

            var config = new ConfigStore(path).Load();

            Assert.Equal(7, config.LogDays, "损坏时应回退默认值");
            var backups = Directory.GetFiles(dir, "config.cfg.bad.*");
            Assert.Equal(1, backups.Length, "损坏文件应被备份");
        }
    }
}
