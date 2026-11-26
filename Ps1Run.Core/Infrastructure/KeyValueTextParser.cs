using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ps1Run.Core.Infrastructure
{
    /// <summary>
    /// 配置与语言包共用的纯文本键值解析器（替代原 SimpleJson，运行时零 JSON 依赖）。
    /// 格式：<c>Key: "value",</c> 或 <c>Key: 42,</c>；行尾逗号可有可无，
    /// 支持 \\、\"、\n、\r、\t、\b、\f、\uXXXX 转义；空行与 # / // 开头的行忽略。
    /// 字符串值必须带双引号；数字 / 枚举等字面量不带引号，按原始文本保留。
    /// </summary>
    public static class KeyValueTextParser
    {
        /// <summary>解析文本内容；格式非法时抛 FormatException，由调用方备份并重建。</summary>
        public static Dictionary<string, string> Parse(string? text)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (text is null)
            {
                return result;
            }

            var content = text.TrimStart('\uFEFF');
            using var reader = new StringReader(content);
            string? line;
            var lineNumber = 0;
            while ((line = reader.ReadLine()) is not null)
            {
                lineNumber++;
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal)
                    || trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var colon = trimmed.IndexOf(':');
                if (colon <= 0)
                {
                    throw new FormatException("第 " + lineNumber.ToString(CultureInfo.InvariantCulture) + " 行缺少 ':'");
                }

                var key = trimmed.Substring(0, colon).Trim();
                if (key.Length == 0)
                {
                    throw new FormatException("第 " + lineNumber.ToString(CultureInfo.InvariantCulture) + " 行键名为空");
                }

                var rawValue = trimmed.Substring(colon + 1).Trim();
                if (rawValue.EndsWith(",", StringComparison.Ordinal))
                {
                    rawValue = rawValue.Substring(0, rawValue.Length - 1).TrimEnd();
                }

                string value;
                if (rawValue.StartsWith("\"", StringComparison.Ordinal))
                {
                    value = ReadQuoted(rawValue, lineNumber);
                }
                else
                {
                    if (rawValue.Length == 0)
                    {
                        throw new FormatException("第 " + lineNumber.ToString(CultureInfo.InvariantCulture) + " 行值缺失");
                    }

                    value = rawValue;
                }

                result[key] = value;
            }

            return result;
        }

        /// <summary>把字符串编码为带双引号的文本字面量。</summary>
        public static string Quote(string? value)
        {
            var builder = new StringBuilder((value?.Length ?? 0) + 2);
            builder.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                foreach (var ch in value!)
                {
                    switch (ch)
                    {
                        case '"': builder.Append("\\\""); break;
                        case '\\': builder.Append("\\\\"); break;
                        case '\b': builder.Append("\\b"); break;
                        case '\f': builder.Append("\\f"); break;
                        case '\n': builder.Append("\\n"); break;
                        case '\r': builder.Append("\\r"); break;
                        case '\t': builder.Append("\\t"); break;
                        default:
                            if (ch < ' ')
                            {
                                builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                builder.Append(ch);
                            }

                            break;
                    }
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        /// <summary>生成一行键值文本（含行尾逗号与换行）。数字字段请传未加引号的字面量。</summary>
        public static string FormatLine(string key, string value) => key + ": " + value + ",\r\n";

        private static string ReadQuoted(string text, int lineNumber)
        {
            var builder = new StringBuilder();
            var index = 1;
            while (index < text.Length)
            {
                var ch = text[index++];
                if (ch == '"')
                {
                    if (index != text.Length)
                    {
                        throw new FormatException(
                            "第 " + lineNumber.ToString(CultureInfo.InvariantCulture) + " 行引号后有多余内容");
                    }

                    return builder.ToString();
                }

                if (ch != '\\')
                {
                    builder.Append(ch);
                    continue;
                }

                if (index >= text.Length)
                {
                    throw new FormatException("第 " + lineNumber.ToString(CultureInfo.InvariantCulture) + " 行转义序列未完成");
                }

                var escape = text[index++];
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length)
                        {
                            throw new FormatException(
                                "第 " + lineNumber.ToString(CultureInfo.InvariantCulture) + " 行 \\u 转义不完整");
                        }

                        var hex = text.Substring(index, 4);
                        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        {
                            throw new FormatException(
                                "第 " + lineNumber.ToString(CultureInfo.InvariantCulture) + " 行 \\u 转义非法：" + hex);
                        }

                        builder.Append((char)code);
                        index += 4;
                        break;
                    default:
                        throw new FormatException(
                            "第 " + lineNumber.ToString(CultureInfo.InvariantCulture) + " 行未知转义：\\" + escape);
                }
            }

            throw new FormatException("第 " + lineNumber.ToString(CultureInfo.InvariantCulture) + " 行字符串未闭合");
        }
    }
}
