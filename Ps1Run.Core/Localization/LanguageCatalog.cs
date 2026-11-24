using System;
using System.Collections.Generic;
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

        /// <summary>确保 lang 目录存在，且至少有一个 zh-cn.json（缺失即按内置文案重建）。</summary>
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
                File.WriteAllText(defaultFile, DefaultStrings.ToJson(), Utf8NoBom);
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

        /// <summary>读取语言包；失败时通过 error 返回原因，不抛异常。</summary>
        public static bool TryLoad(string filePath, out Dictionary<string, string>? entries, out string? error)
        {
            entries = null;
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
