using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Ps1Run.Core.Configuration;
using Ps1Run.Core.Dispatch;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.Core.Rules
{
    /// <summary>
    /// rule.txt 读写：5 列文本、UTF-8 无 BOM、临时文件 + 原子替换、命名互斥体防并发写坏。
    /// 坏行跳过并写 WARN；文件损坏时按空规则处理（失败开放，正常弹窗）。
    /// </summary>
    public static class ScriptRuleStore
    {
        public const string Header = "文件路径|动作|文件大小|修改时间|文件HASH";

        private const string MutexName = "Ps1Run.RuleFile.Write";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string FilePath => AppPaths.RuleFile;

        /// <summary>读取全部规则；重复路径保留最后一条，按路径排序。</summary>
        public static IReadOnlyList<ScriptRule> Load(string? filePath = null)
        {
            var path = string.IsNullOrEmpty(filePath) ? FilePath : filePath!;
            var rules = new Dictionary<string, ScriptRule>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!File.Exists(path))
                {
                    return Array.Empty<ScriptRule>();
                }

                var lines = File.ReadAllLines(path, Encoding.UTF8);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.Length == 0 || string.Equals(line, Header, StringComparison.Ordinal)
                        || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var columns = line.Split('|');
                    if (columns.Length != 5
                        || string.IsNullOrWhiteSpace(columns[0])
                        || !TryParseAction(columns[1], out var action)
                        || !long.TryParse(columns[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
                    {
                        DiagnosticsLog.Warn(
                            "脚本规则第 " + (i + 1).ToString(CultureInfo.InvariantCulture) + " 行格式非法，已跳过：" + line);
                        continue;
                    }

                    var scriptPath = columns[0].Trim();
                    rules[scriptPath] = new ScriptRule
                    {
                        Path = scriptPath,
                        Action = action,
                        Size = size,
                        ModifiedTimeUtc = string.IsNullOrWhiteSpace(columns[3]) ? "0" : columns[3].Trim(),
                        Hash = string.IsNullOrWhiteSpace(columns[4]) ? "0" : columns[4].Trim().ToLowerInvariant(),
                    };
                }
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Warn("读取脚本规则失败（按无规则处理）：" + ex.Message);
                return Array.Empty<ScriptRule>();
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Warn("读取脚本规则失败（按无规则处理）：" + ex.Message);
                return Array.Empty<ScriptRule>();
            }

            return rules.Values
                .OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>写入全部规则（覆盖文件）；路径重复的只保留一条。</summary>
        public static void Save(string filePath, IEnumerable<ScriptRule> rules)
        {
            if (rules is null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var path = string.IsNullOrEmpty(filePath) ? FilePath : filePath!;
            var unique = new Dictionary<string, ScriptRule>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in rules)
            {
                if (!string.IsNullOrWhiteSpace(rule.Path))
                {
                    unique[rule.Path.Trim()] = rule;
                }
            }

            var builder = new StringBuilder();
            builder.Append(Header).Append("\r\n");
            foreach (var rule in unique.Values.OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append(rule.Path).Append('|')
                    .Append(rule.ActionToken).Append('|')
                    .Append(rule.Size.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(string.IsNullOrEmpty(rule.ModifiedTimeUtc) ? "0" : rule.ModifiedTimeUtc).Append('|')
                    .Append(string.IsNullOrEmpty(rule.Hash) ? "0" : rule.Hash.ToLowerInvariant())
                    .Append("\r\n");
            }

            WithMutex(() =>
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var temp = path + ".tmp";
                File.WriteAllText(temp, builder.ToString(), Utf8NoBom);
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
            });
        }

        /// <summary>按路径新增或覆盖一条规则。</summary>
        public static void Upsert(ScriptRule rule, string? filePath = null)
        {
            var path = string.IsNullOrEmpty(filePath) ? FilePath : filePath!;
            var rules = Load(path).Where(r => !string.Equals(r.Path, rule.Path, StringComparison.OrdinalIgnoreCase)).ToList();
            rules.Add(rule);
            Save(path, rules);
        }

        /// <summary>按路径删除一条规则；返回是否真的删掉。</summary>
        public static bool Remove(string scriptPath, string? filePath = null)
        {
            var path = string.IsNullOrEmpty(filePath) ? FilePath : filePath!;
            var rules = Load(path);
            var kept = rules.Where(r => !string.Equals(r.Path, scriptPath, StringComparison.OrdinalIgnoreCase)).ToList();
            if (kept.Count == rules.Count)
            {
                return false;
            }

            Save(path, kept);
            return true;
        }

        private static bool TryParseAction(string token, out LaunchAction action)
        {
            if (Enum.TryParse(token.Trim(), ignoreCase: true, out action)
                && (action == LaunchAction.Run || action == LaunchAction.Edit))
            {
                return true;
            }

            action = LaunchAction.Cancel;
            return false;
        }

        private static void WithMutex(Action action)
        {
            Mutex? mutex = null;
            var acquired = false;
            try
            {
                mutex = new Mutex(false, MutexName);
                try
                {
                    // AbandonedMutexException 也表示当前线程已获得所有权
                    mutex.WaitOne();
                    acquired = true;
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }
                catch (UnauthorizedAccessException)
                {
                    // 不同会话无法共享命名互斥体时退化为不加锁，仍可写盘
                    acquired = false;
                }
            }
            catch (UnauthorizedAccessException)
            {
                acquired = false;
            }

            try
            {
                action();
            }
            finally
            {
                if (acquired && mutex is not null)
                {
                    mutex.ReleaseMutex();
                }

                mutex?.Dispose();
            }
        }
    }
}
