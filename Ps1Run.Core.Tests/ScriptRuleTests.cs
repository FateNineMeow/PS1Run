using System;
using System.IO;
using System.Text;
using Ps1Run.Core.Dispatch;
using Ps1Run.Core.Rules;
using Ps1Run.Core.Settings;

namespace Ps1Run.Core.Tests
{
    internal static class ScriptRuleTests
    {
        public static void RunAll()
        {
            StoresFiveColumns();
            UpsertAndRemove();
            SkipsBrokenLines();
            MatchesByPathCaseInsensitive();
            ShortCircuitsOnSizeAndTime();
            MatchesWithHashEnabled();
            IgnoresDisabledCriteria();
            ValidatesPathAndExistence();
        }

        private static string NewTempDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ps1run-rule-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void StoresFiveColumns()
        {
            var ruleFile = Path.Combine(NewTempDirectory(), "rule.txt");
            var fs = new FakeFileSystem();
            var path = @"C:\scripts\one.ps1";
            fs.Add(path, Encoding.UTF8.GetBytes("Write-Host 1"),
                new DateTime(2026, 11, 26, 3, 0, 0, DateTimeKind.Utc));

            var rule = ScriptRuleFingerprint.Create(path, LaunchAction.Run, AppSettings.Default, fs);
            ScriptRuleStore.Save(ruleFile, new[] { rule });

            var lines = File.ReadAllLines(ruleFile);
            Assert.Equal(2, lines.Length, "表头 + 一条规则");
            Assert.Equal(ScriptRuleStore.Header, lines[0], "表头应为 5 列");
            Assert.True(lines[1].EndsWith("|0", StringComparison.Ordinal), "HASH 默认关闭应写 0");

            var loaded = ScriptRuleStore.Load(ruleFile);
            Assert.Equal(1, loaded.Count, "应读回一条规则");
            Assert.Equal(path, loaded[0].Path, "路径应往返一致");
            Assert.Equal(LaunchAction.Run, loaded[0].Action, "动作应往返一致");
            Assert.Equal(rule.Size, loaded[0].Size, "大小应往返一致");
        }

        private static void UpsertAndRemove()
        {
            var ruleFile = Path.Combine(NewTempDirectory(), "rule.txt");
            var fs = new FakeFileSystem();
            var path = @"C:\scripts\two.ps1";
            fs.Add(path, Encoding.UTF8.GetBytes("Write-Host 2"));

            var run = ScriptRuleFingerprint.Create(path, LaunchAction.Run, AppSettings.Default, fs);
            var edit = ScriptRuleFingerprint.Create(path, LaunchAction.Edit, AppSettings.Default, fs);
            ScriptRuleStore.Upsert(run, ruleFile);
            ScriptRuleStore.Upsert(edit, ruleFile);

            var rules = ScriptRuleStore.Load(ruleFile);
            Assert.Equal(1, rules.Count, "同一路径只保留一条规则");
            Assert.Equal(LaunchAction.Edit, rules[0].Action, "后写入的规则应覆盖旧规则");
            Assert.True(ScriptRuleStore.Remove(path, ruleFile), "删除应成功");
            Assert.Equal(0, ScriptRuleStore.Load(ruleFile).Count, "删除后应无规则");
            Assert.True(!ScriptRuleStore.Remove(path, ruleFile), "重复删除返回 false");
        }

        private static void SkipsBrokenLines()
        {
            var ruleFile = Path.Combine(NewTempDirectory(), "rule.txt");
            var path = @"C:\scripts\three.ps1";
            File.WriteAllText(ruleFile,
                ScriptRuleStore.Header + "\r\n"
                + path + "|Run|12|0|0\r\n"
                + "bad|line|without|enough|columns|extra\r\n",
                new UTF8Encoding(false));

            var rules = ScriptRuleStore.Load(ruleFile);
            Assert.Equal(1, rules.Count, "坏行应被跳过");
            Assert.Equal(path, rules[0].Path, "合法行应保留");
        }

        private static void MatchesByPathCaseInsensitive()
        {
            var ruleFile = Path.Combine(NewTempDirectory(), "rule.txt");
            var fs = new FakeFileSystem();
            var path = @"C:\scripts\Four.ps1";
            fs.Add(path, Encoding.UTF8.GetBytes("Write-Host 4"));

            var rule = ScriptRuleFingerprint.Create(path, LaunchAction.Edit, AppSettings.Default, fs);
            ScriptRuleStore.Save(ruleFile, new[] { rule });

            var matcher = new ScriptRuleMatcher(fs, ruleFile);
            Assert.True(matcher.TryMatch(@"c:\SCRIPTS\four.ps1", AppSettings.Default, out var matched, out var reason),
                "路径比较应不区分大小写");
            Assert.Equal(LaunchAction.Edit, matched!.Action, "命中动作应正确");
            Assert.True(reason!.Contains("文件路径"), "命中原因应包含文件路径");
        }

        private static void ShortCircuitsOnSizeAndTime()
        {
            var ruleFile = Path.Combine(NewTempDirectory(), "rule.txt");
            var fs = new FakeFileSystem();
            var path = @"C:\scripts\five.ps1";
            var original = Encoding.UTF8.GetBytes("Write-Host 5");
            var modified = new DateTime(2026, 11, 26, 3, 0, 0, DateTimeKind.Utc);
            fs.Add(path, original, modified);

            var rule = ScriptRuleFingerprint.Create(path, LaunchAction.Run, AppSettings.Default, fs);
            ScriptRuleStore.Save(ruleFile, new[] { rule });
            var matcher = new ScriptRuleMatcher(fs, ruleFile);

            Assert.True(matcher.TryMatch(path, AppSettings.Default, out _, out _), "原始文件应命中");

            // 仅大小变化
            fs.Add(path, Encoding.UTF8.GetBytes("Write-Host 5 -- longer"), modified);
            Assert.True(!matcher.TryMatch(path, AppSettings.Default, out _, out _), "大小不符不应命中");

            // 大小恢复、修改时间变化
            fs.Add(path, original, modified.AddMinutes(1));
            Assert.True(!matcher.TryMatch(path, AppSettings.Default, out _, out _), "修改时间不符不应命中");
        }

        private static void MatchesWithHashEnabled()
        {
            var ruleFile = Path.Combine(NewTempDirectory(), "rule.txt");
            var fs = new FakeFileSystem();
            var path = @"C:\scripts\six.ps1";
            var modified = new DateTime(2026, 11, 26, 4, 0, 0, DateTimeKind.Utc);
            fs.Add(path, Encoding.UTF8.GetBytes("Write-Host 6"), modified);

            var settings = AppSettings.Default with { RuleMatchHash = true };
            var rule = ScriptRuleFingerprint.Create(path, LaunchAction.Edit, settings, fs);
            Assert.True(rule.Hash.Length == 64, "HASH 应为 SHA-256");
            ScriptRuleStore.Save(ruleFile, new[] { rule });

            var matcher = new ScriptRuleMatcher(fs, ruleFile);
            Assert.True(matcher.TryMatch(path, settings, out _, out var reason), "HASH 一致应命中");
            Assert.True(reason!.Contains("HASH"), "命中原因应含 HASH");

            fs.Add(path, Encoding.UTF8.GetBytes("Write-Host 6 changed"), modified);
            Assert.True(!matcher.TryMatch(path, settings, out _, out _), "HASH 不符不应命中");
        }

        private static void IgnoresDisabledCriteria()
        {
            var ruleFile = Path.Combine(NewTempDirectory(), "rule.txt");
            var fs = new FakeFileSystem();
            var path = @"C:\scripts\seven.ps1";
            fs.Add(path, Encoding.UTF8.GetBytes("old"), new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            var settings = AppSettings.Default with
            {
                RuleMatchSize = false,
                RuleMatchModifiedTime = false,
                RuleMatchHash = false,
            };
            var rule = ScriptRuleFingerprint.Create(path, LaunchAction.Run, settings, fs);
            Assert.Equal(0, rule.Size, "关闭大小时指纹写 0");
            Assert.Equal("0", rule.ModifiedTimeUtc, "关闭修改时间时指纹写 0");
            ScriptRuleStore.Save(ruleFile, new[] { rule });

            fs.Add(path, Encoding.UTF8.GetBytes("completely different content"),
                new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var matcher = new ScriptRuleMatcher(fs, ruleFile);
            Assert.True(matcher.TryMatch(path, settings, out _, out _), "关闭全部标准后仅按路径命中");
        }

        private static void ValidatesPathAndExistence()
        {
            var fs = new FakeFileSystem();
            Assert.True(!ScriptRuleFingerprint.IsPowerShellScript(@"C:\scripts\not-ps1.txt"), "非 .ps1 扩展名应被拒绝");
            Assert.True(ScriptRuleFingerprint.IsPowerShellScript(@"C:\scripts\OK.PS1"), "扩展名比较应不区分大小写");

            var threw = false;
            try
            {
                ScriptRuleFingerprint.Create(@"C:\scripts\missing.ps1", LaunchAction.Run, AppSettings.Default, fs);
            }
            catch (FileNotFoundException)
            {
                threw = true;
            }

            Assert.True(threw, "文件不存在时应拒绝生成指纹");

            threw = false;
            try
            {
                ScriptRuleFingerprint.Create(@"C:\scripts\bad.txt", LaunchAction.Run, AppSettings.Default, fs);
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            Assert.True(threw, "非 .ps1 文件应拒绝生成规则");
        }
    }
}
