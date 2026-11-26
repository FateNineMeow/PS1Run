using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Ps1Run.Core.Infrastructure;
using Ps1Run.Core.Settings;

namespace Ps1Run.Core.Rules
{
    /// <summary>
    /// 规则匹配：文件路径始终参与，按配置逐项短路检查大小 / 修改时间 / HASH。
    /// 规则文件损坏或指纹读取失败时"失败开放"——返回未命中，让选择窗照常弹出。
    /// </summary>
    public sealed class ScriptRuleMatcher
    {
        private readonly IFileSystem _fileSystem;
        private readonly string? _ruleFile;

        public ScriptRuleMatcher(IFileSystem? fileSystem = null, string? ruleFile = null)
        {
            _fileSystem = fileSystem ?? PhysicalFileSystem.Instance;
            _ruleFile = ruleFile;
        }

        /// <summary>
        /// 尝试为脚本找到命中的规则。reason 记录命中的判断标准，供日志使用。
        /// </summary>
        public bool TryMatch(string scriptPath, AppSettings settings, out ScriptRule? matched, out string? reason)
        {
            matched = null;
            reason = null;

            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                return false;
            }

            var fullPath = ScriptRuleFingerprint.Normalize(scriptPath);
            var rules = ScriptRuleStore.Load(_ruleFile);
            ScriptRule? candidate = null;
            foreach (var rule in rules)
            {
                if (string.Equals(rule.Path, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    candidate = rule;
                    break;
                }
            }

            if (candidate is null)
            {
                return false;
            }

            try
            {
                if (!_fileSystem.FileExists(fullPath))
                {
                    return false;
                }

                var used = new System.Collections.Generic.List<string> { "文件路径" };

                if (settings.RuleMatchSize && _fileSystem.GetFileLength(fullPath) != candidate.Size)
                {
                    return false;
                }

                if (settings.RuleMatchSize)
                {
                    used.Add("大小");
                }

                if (settings.RuleMatchModifiedTime
                    && !string.Equals(
                        _fileSystem.GetLastWriteTimeUtc(fullPath).ToString("O", CultureInfo.InvariantCulture),
                        candidate.ModifiedTimeUtc,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (settings.RuleMatchModifiedTime)
                {
                    used.Add("修改时间");
                }

                if (settings.RuleMatchHash)
                {
                    using var stream = _fileSystem.OpenRead(fullPath);
                    using var sha = SHA256.Create();
                    var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                    if (!string.Equals(hash, candidate.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    used.Add("HASH");
                }

                matched = candidate;
                reason = string.Join(" + ", used);
                return true;
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Warn("脚本规则指纹读取失败，按未命中处理：" + ex.Message);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Warn("脚本规则指纹读取失败，按未命中处理：" + ex.Message);
                return false;
            }
            catch (CryptographicException ex)
            {
                DiagnosticsLog.Warn("脚本规则 HASH 计算失败，按未命中处理：" + ex.Message);
                return false;
            }
        }
    }
}
