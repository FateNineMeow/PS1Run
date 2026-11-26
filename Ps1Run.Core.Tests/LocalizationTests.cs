using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ps1Run.Core.Infrastructure;
using Ps1Run.Core.Localization;

namespace Ps1Run.Core.Tests
{
    internal static class LocalizationTests
    {
        public static void RunAll()
        {
            GeneratesDefaultLanguageFile();
            UpgradesOldDefaultPack();
            DetectsMissingPackVersion();
            DiscoversAvailableLanguages();
            IgnoresLegacyJson();
            ParsesEscapesAndUnicode();
            RejectsBrokenLanguageFile();
        }

        private static string NewTempDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ps1run-lang-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void GeneratesDefaultLanguageFile()
        {
            var dir = NewTempDirectory();
            LanguageCatalog.EnsureDefaultFiles(dir);

            var file = Path.Combine(dir, "zh-cn.lang");
            Assert.True(File.Exists(file), "应生成 zh-cn.lang");

            Assert.True(
                LanguageCatalog.TryLoad(file, out var entries, out var error),
                "生成的 zh-cn.lang 应能被解析：" + error);
            Assert.NotNull(entries, "解析结果不应为空");
            Assert.Equal("保存设置", entries!["Button.Save"], "Button.Save 文案应一致");
            Assert.Equal(DefaultStrings.ZhCn.Count, entries.Count, "生成的键数量应与内置文案一致");
            Assert.Equal(LanguageCatalog.CurrentVersion, LanguageCatalog.ReadVersion(file), "新文件版本应为 2");
        }

        private static void UpgradesOldDefaultPack()
        {
            var dir = NewTempDirectory();
            var file = Path.Combine(dir, "zh-cn.lang");
            File.WriteAllText(file, "Version: 1,\r\nButton.Save: \"旧文案\",\r\n", new UTF8Encoding(false));

            Assert.Equal(1, LanguageCatalog.ReadVersion(file), "旧语言包版本应被读出");

            LanguageCatalog.EnsureDefaultFiles(dir);

            Assert.Equal(LanguageCatalog.CurrentVersion, LanguageCatalog.ReadVersion(file), "旧语言包应被升级到当前版本");

            Assert.True(
                LanguageCatalog.TryLoad(file, out var entries, out var version, out var error),
                "升级后的 zh-cn.lang 应能被解析：" + error);
            Assert.Equal(LanguageCatalog.CurrentVersion, version, "加载时应带出版本号");
            Assert.True(!entries!.ContainsKey(DefaultStrings.PackVersionKey), "版本号不应作为文案条目暴露");
            Assert.Equal("保存设置", entries["Button.Save"], "升级后应使用内置文案");
        }

        private static void DetectsMissingPackVersion()
        {
            var dir = NewTempDirectory();
            var file = Path.Combine(dir, "en-us.lang");
            File.WriteAllText(file, "Button.Save: \"Save\"\r\n", new UTF8Encoding(false));

            Assert.True(LanguageCatalog.ReadVersion(file) is null, "无版本号应返回 null");

            // 其他语言包处理逻辑不变：照常加载，只是能识别出版本号缺失
            Assert.True(
                LanguageCatalog.TryLoad(file, out var entries, out var version, out var error),
                "旧语言包仍应能加载：" + error);
            Assert.True(version is null, "缺失版本号时版本为 null");
            Assert.Equal("Save", entries!["Button.Save"], "其他语言包文案应按原样加载");
        }

        private static void DiscoversAvailableLanguages()
        {
            var dir = NewTempDirectory();
            File.WriteAllText(Path.Combine(dir, "en-us.lang"), "Button.Save: \"Save settings\"\r\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(dir, "zh-cn.lang"), "Version: 2,\r\n", new UTF8Encoding(false));

            var options = LanguageCatalog.Discover(dir);

            Assert.Equal(2, options.Count, "应发现两个 .lang 语言包");
            Assert.Equal("zh-cn", options[0].Code, "zh-cn 应排第一");
            Assert.Equal("简体中文", options[0].DisplayName, "zh-cn 显示名");
            Assert.Equal("English", options[1].DisplayName, "en-us 显示名");
        }

        private static void IgnoresLegacyJson()
        {
            var dir = NewTempDirectory();
            File.WriteAllText(Path.Combine(dir, "zh-cn.json"), "{\"Button.Save\":\"旧格式\"}", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(dir, "ja-jp.json"), "{\"Button.Save\":\"旧格式\"}", new UTF8Encoding(false));

            var options = LanguageCatalog.Discover(dir);

            Assert.Equal(1, options.Count, "旧 .json 语言包不应进入下拉框");
            Assert.Equal("zh-cn", options[0].Code, "无 .lang 时仅回退 zh-cn 默认项");
            Assert.True(File.Exists(Path.Combine(dir, "zh-cn.json")), "旧 .json 文件不应被自动删除");
        }

        private static void ParsesEscapesAndUnicode()
        {
            var text = "a: \"中\\u6587\"\r\nb: \"quote\\\"x\"\r\nn: 42,\r\n";
            var map = KeyValueTextParser.Parse(text);

            Assert.Equal("中文", map["a"], "\\u 转义应被解码");
            Assert.Equal("quote\"x", map["b"], "引号转义应被解码");
            Assert.Equal("42", map["n"], "数字应保留字面量");

            var quoted = KeyValueTextParser.Quote("换行\n与\"引号\"");
            var roundTrip = KeyValueTextParser.Parse("k: " + quoted + ",");
            Assert.Equal("换行\n与\"引号\"", roundTrip["k"], "Quote 与解析应往返一致");
        }

        private static void RejectsBrokenLanguageFile()
        {
            var dir = NewTempDirectory();
            var file = Path.Combine(dir, "bad.lang");
            File.WriteAllText(file, "this line has no colon", new UTF8Encoding(false));

            Assert.True(
                !LanguageCatalog.TryLoad(file, out var entries, out var error),
                "损坏的语言包应返回失败");
            Assert.True(entries is null, "失败时不应返回条目");
            Assert.True(!string.IsNullOrEmpty(error), "失败时应带原因");
        }
    }
}
