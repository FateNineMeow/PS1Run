using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Ps1Run.Core.Localization
{
    /// <summary>语言下拉框的一项。</summary>
    public sealed record LanguageOption(string Code, string DisplayName);

    /// <summary>
    /// lang 目录的语言包管理（wiki 02 F7.x）：
    /// 扫描 *.json 得到可选语言；缺省时生成 zh-cn.json；解析单个语言包。
    /// </summary>
    public static class LanguageCatalog
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>语言包中记录版本号的保留键。</summary>
        public const string VersionKey = DefaultStrings.PackVersionKey;

        /// <summary>当前程序期望的语言包版本。</summary>
        public const int CurrentVersion = DefaultStrings.PackVersion;

        /// <summary>
        /// 确保 lang 目录存在，且 zh-cn.json 存在并带当前版本号：
        /// 缺失 → 按内置文案生成；缺少版本号或版本较旧 → 按当前内置文案重建（即"应用新版 zh-cn.json"）。
        /// 其他语言包不做任何改写，只在使用时读取。
        /// </summary>
        public static void EnsureDefaultFiles(string languageDirectory)
        {
            if (string.IsNullOrEmpty(languageDirectory))
            {
                return;
            }

            Directory.CreateDirectory(languageDirectory);

            var defaultFile = Path.Combine(languageDirectory, DefaultStrings.DefaultLanguageCode + ".json");
            if (!File.Exists(defaultFile))
            {
                WriteDefaultFile(defaultFile);
                return;
            }

            var version = ReadVersion(defaultFile);
            if (version is not null && version.Value >= CurrentVersion)
            {
                return;
            }

            WriteDefaultFile(defaultFile);
            Infrastructure.DiagnosticsLog.Info(
                "语言包 zh-cn.json 已升级到 v" + CurrentVersion.ToString(CultureInfo.InvariantCulture)
                + (version is null ? "（原文件无版本号）" : "（原版本 v" + version.Value.ToString(CultureInfo.InvariantCulture) + "）")
                + "：" + defaultFile);
        }

        /// <summary>按内置中文写入语言文件（含版本号）。</summary>
        public static void WriteDefaultFile(string filePath)
        {
            File.WriteAllText(filePath, DefaultStrings.ToJson(), Utf8NoBom);
        }

        /// <summary>读取语言包版本号；文件缺失、损坏或无版本号时返回 null。</summary>
        public static int? ReadVersion(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return null;
                }

                var parsed = Infrastructure.SimpleJson.ParseObject(File.ReadAllText(filePath, Encoding.UTF8));
                return GetVersion(parsed);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>扫描 lang 目录下的语言包；目录不存在时返回仅含 zh-cn 的列表。</summary>
        public static IReadOnlyList<LanguageOption> Discover(string languageDirectory)
        {
            var options = new List<LanguageOption>();

            try
            {
                if (!string.IsNullOrEmpty(languageDirectory) && Directory.Exists(languageDirectory))
                {
                    foreach (var file in Directory.GetFiles(languageDirectory, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    {
                        var code = Path.GetFileNameWithoutExtension(file);
                        if (string.IsNullOrWhiteSpace(code))
                        {
                            continue;
                        }

                        code = code.Trim().ToLowerInvariant();
                        if (options.Any(o => string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        options.Add(new LanguageOption(code, DefaultStrings.Describe(code)));
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            if (options.Count == 0)
            {
                options.Add(new LanguageOption(
                    DefaultStrings.DefaultLanguageCode,
                    DefaultStrings.Describe(DefaultStrings.DefaultLanguageCode)));
            }

            // zh-cn 固定排第一，其余按代码排序
            return options
                .OrderBy(o => string.Equals(o.Code, DefaultStrings.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(o => o.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>读取语言包（不关心版本号）；失败时通过 error 返回原因，不抛异常。</summary>
        public static bool TryLoad(string filePath, out Dictionary<string, string>? entries, out string? error)
        {
            return TryLoad(filePath, out entries, out _, out error);
        }

        /// <summary>
        /// 读取语言包并返回其版本号（缺失为 null）；版本号保留键会从条目中剔除，
        /// 因此不会作为界面文案被引用。
        /// </summary>
        public static bool TryLoad(
            string filePath,
            out Dictionary<string, string>? entries,
            out int? version,
            out string? error)
        {
            entries = null;
            version = null;
            error = null;

            try
            {
                if (!File.Exists(filePath))
                {
                    error = "文件不存在：" + filePath;
                    return false;
                }

                var parsed = Infrastructure.SimpleJson.ParseObject(File.ReadAllText(filePath, Encoding.UTF8));
                if (parsed.Count == 0)
                {
                    error = "语言包为空：" + filePath;
                    return false;
                }

                version = GetVersion(parsed);
                parsed.Remove(VersionKey);

                if (parsed.Count == 0)
                {
                    error = "语言包为空：" + filePath;
                    return false;
                }

                entries = parsed;
                return true;
            }
            catch (FormatException ex)
            {
                error = "语言包格式非法：" + filePath + " → " + ex.Message;
                return false;
            }
            catch (IOException ex)
            {
                error = "语言包读取失败：" + filePath + " → " + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = "语言包读取失败：" + filePath + " → " + ex.Message;
                return false;
            }
        }

        /// <summary>从已解析的键值中取版本号；不存在或非整数时返回 null。</summary>
        private static int? GetVersion(Dictionary<string, string> values)
        {
            if (values.TryGetValue(VersionKey, out var raw))
            {
                var text = raw.Trim().Trim('"');
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        /// <summary>规范化语言代码：空值回退 zh-cn，其余小写。</summary>
        public static string Normalize(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return DefaultStrings.DefaultLanguageCode;
            }

            return code!.Trim().ToLowerInvariant();
        }
    }
}
