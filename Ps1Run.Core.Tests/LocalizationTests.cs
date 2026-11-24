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
            DiscoversAvailableLanguages();
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

            var file = Path.Combine(dir, "zh-cn.json");
            Assert.True(File.Exists(file), "应生成 zh-cn.json");

            Assert.True(
                LanguageCatalog.TryLoad(file, out var entries, out var error),
                "生成的 zh-cn.json 应能被解析：" + error);
            Assert.NotNull(entries, "解析结果不应为空");
            Assert.Equal("保存设置", entries!["Button.Save"], "Button.Save 文案应一致");
            Assert.Equal(DefaultStrings.ZhCn.Count, entries.Count, "生成的键数量应与内置文案一致");
        }

        private static void DiscoversAvailableLanguages()
        {
            var dir = NewTempDirectory();
            File.WriteAllText(Path.Combine(dir, "en-us.json"), "{ \"Button.Save\": \"Save settings\" }", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(dir, "zh-cn.json"), "{}", new UTF8Encoding(false));

            var options = LanguageCatalog.Discover(dir);

            Assert.Equal(2, options.Count, "应发现两个语言包");
            Assert.Equal("zh-cn", options[0].Code, "zh-cn 应排第一");
            Assert.Equal("简体中文", options[0].DisplayName, "zh-cn 显示名");
            Assert.Equal("English", options[1].DisplayName, "en-us 显示名");
        }

        private static void ParsesEscapesAndUnicode()
        {
            var json = "{ \"a\": \"中\\u6587\", \"b\": \"quote\\\"x\", \"n\": 42 }";
            var map = SimpleJson.ParseObject(json);

            Assert.Equal("中文", map["a"], "\\u 转义应被解码");
            Assert.Equal("quote\"x", map["b"], "引号转义应被解码");
            Assert.Equal("42", map["n"], "数字应保留字面量");

            var quoted = SimpleJson.Quote("换行\n与\"引号\"");
            var roundTrip = SimpleJson.ParseObject("{ \"k\": " + quoted + " }");
            Assert.Equal("换行\n与\"引号\"", roundTrip["k"], "Quote 与解析应往返一致");
        }

        private static void RejectsBrokenLanguageFile()
        {
            var dir = NewTempDirectory();
            var file = Path.Combine(dir, "bad.json");
            File.WriteAllText(file, "{ broken", new UTF8Encoding(false));

            Assert.True(
                !LanguageCatalog.TryLoad(file, out var entries, out var error),
                "损坏的语言包应返回失败");
            Assert.True(entries is null, "失败时不应返回条目");
            Assert.True(!string.IsNullOrEmpty(error), "失败时应带原因");
        }
    }
}
