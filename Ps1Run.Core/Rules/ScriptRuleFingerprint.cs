using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Ps1Run.Core.Dispatch;
using Ps1Run.Core.Infrastructure;
using Ps1Run.Core.Settings;

namespace Ps1Run.Core.Rules
{
    /// <summary>
    /// 按当前全局判断标准计算规则指纹。路径始终参与，其余项关闭时写 "0"；
    /// HASH 默认关闭，开启时需读取整个脚本，可能减缓启动速度。
    /// </summary>
    public static class ScriptRuleFingerprint
    {
        public static ScriptRule Create(
            string scriptPath,
            LaunchAction action,
            AppSettings settings,
            IFileSystem? fileSystem = null)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                throw new ArgumentException("脚本路径不能为空", nameof(scriptPath));
            }

            if (action == LaunchAction.Cancel)
            {
                throw new ArgumentException("规则动作只能是 Run 或 Edit", nameof(action));
            }

            if (!IsPowerShellScript(scriptPath))
            {
                throw new ArgumentException("仅支持 .ps1 文件", nameof(scriptPath));
            }

            var fs = fileSystem ?? PhysicalFileSystem.Instance;
            var fullPath = Normalize(scriptPath);
            if (!fs.FileExists(fullPath))
            {
                throw new FileNotFoundException("文件不存在，无法生成指纹：" + fullPath, fullPath);
            }

            return new ScriptRule
            {
                Path = fullPath,
                Action = action,
                Size = settings.RuleMatchSize ? fs.GetFileLength(fullPath) : 0,
                ModifiedTimeUtc = settings.RuleMatchModifiedTime
                    ? fs.GetLastWriteTimeUtc(fullPath).ToString("O", CultureInfo.InvariantCulture)
                    : "0",
                Hash = settings.RuleMatchHash ? ComputeHash(fs, fullPath) : "0",
            };
        }

        /// <summary>全局标准变化后重写一条规则的指纹列（保留原动作）。</summary>
        public static ScriptRule Refresh(ScriptRule rule, AppSettings settings, IFileSystem? fileSystem = null)
        {
            if (rule is null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            var fs = fileSystem ?? PhysicalFileSystem.Instance;
            return new ScriptRule
            {
                Path = rule.Path,
                Action = rule.Action,
                Size = settings.RuleMatchSize && fs.FileExists(rule.Path) ? fs.GetFileLength(rule.Path) : 0,
                ModifiedTimeUtc = settings.RuleMatchModifiedTime && fs.FileExists(rule.Path)
                    ? fs.GetLastWriteTimeUtc(rule.Path).ToString("O", CultureInfo.InvariantCulture)
                    : "0",
                Hash = settings.RuleMatchHash && fs.FileExists(rule.Path) ? ComputeHash(fs, rule.Path) : "0",
            };
        }

        /// <summary>规范成全路径；失败时返回去空格后的原值。</summary>
        public static string Normalize(string path)
        {
            var text = (path ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return text;
            }

            try
            {
                return Path.GetFullPath(text);
            }
            catch (ArgumentException)
            {
                return text;
            }
            catch (NotSupportedException)
            {
                return text;
            }
            catch (PathTooLongException)
            {
                return text;
            }
        }

        public static bool IsPowerShellScript(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetExtension(path.Trim()), ".ps1", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static string ComputeHash(IFileSystem fileSystem, string path)
        {
            using var stream = fileSystem.OpenRead(path);
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(stream);
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
