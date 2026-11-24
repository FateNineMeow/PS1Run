using System;
using System.Collections.Generic;
using System.Text;

namespace Ps1Run.Core.Infrastructure
{
    /// <summary>
    /// 极简 JSON 对象读写（只支持「扁平对象、值为字符串或字面量」）。
    /// 用于 config.json 与 lang\*.json：保持 net48 零额外依赖，行为与 System.Text.Json 对齐
    /// （缺失用默认值、损坏即抛 FormatException 由调用方备份重建）。
    /// </summary>
    public static class SimpleJson
    {
        /// <summary>解析扁平 JSON 对象；字符串已反转义，数字/布尔保留原始字面量。</summary>
        public static Dictionary<string, string> ParseObject(string? json)
        {
            if (json is null)
            {
                throw new FormatException("JSON 内容为空");
            }

            var text = json.Trim().TrimStart('\uFEFF');
            if (text.Length == 0)
            {
                throw new FormatException("JSON 内容为空");
            }

            var index = 0;
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            SkipWhitespace(text, ref index);
            Expect(text, ref index, '{');
            SkipWhitespace(text, ref index);

            if (Peek(text, index) == '}')
            {
                return result;
            }

            while (true)
            {
                SkipWhitespace(text, ref index);
                if (Peek(text, index) != '"')
                {
                    throw new FormatException("JSON 键必须是字符串（位置 " + index + "）");
                }

                var key = ReadString(text, ref index);
                SkipWhitespace(text, ref index);
                Expect(text, ref index, ':');
                SkipWhitespace(text, ref index);

                string value;
                var current = Peek(text, index);
                if (current == '"')
                {
                    value = ReadString(text, ref index);
                }
                else if (current == '{' || current == '[')
                {
                    // 扁平结构不支持嵌套；跳过后按原始文本保留，调用方一般会忽略
                    value = ReadBalanced(text, ref index);
                }
                else
                {
                    value = ReadLiteral(text, ref index);
                }

                result[key] = value;
                SkipWhitespace(text, ref index);
                var separator = Next(text, ref index);
                if (separator == ',')
                {
                    continue;
                }

                if (separator == '}')
                {
                    break;
                }

                throw new FormatException("JSON 对象格式非法（位置 " + index + "）");
            }

            return result;
        }

        /// <summary>把字符串编码成 JSON 字面量（含引号）。</summary>
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
                                builder.Append("\\u").Append(((int)ch).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
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

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }
        }

        private static char Peek(string text, int index) => index < text.Length ? text[index] : '\0';

        private static char Next(string text, ref int index)
        {
            if (index >= text.Length)
            {
                throw new FormatException("JSON 提前结束");
            }

            return text[index++];
        }

        private static void Expect(string text, ref int index, char expected)
        {
            var actual = Next(text, ref index);
            if (actual != expected)
            {
                throw new FormatException("JSON 期望 '" + expected + "'，实际 '" + actual + "'");
            }
        }

        private static string ReadLiteral(string text, ref int index)
        {
            var start = index;
            while (index < text.Length && text[index] != ',' && text[index] != '}' && text[index] != ']')
            {
                index++;
            }

            var literal = text.Substring(start, index - start).Trim();
            if (literal.Length == 0)
            {
                throw new FormatException("JSON 值缺失（位置 " + start + "）");
            }

            return literal;
        }

        private static string ReadBalanced(string text, ref int index)
        {
            var start = index;
            var depth = 0;
            while (index < text.Length)
            {
                var ch = text[index];
                if (ch == '{' || ch == '[')
                {
                    depth++;
                }
                else if (ch == '}' || ch == ']')
                {
                    depth--;
                }

                index++;
                if (depth == 0)
                {
                    return text.Substring(start, index - start);
                }
            }

            throw new FormatException("JSON 嵌套结构未闭合");
        }

        private static string ReadString(string text, ref int index)
        {
            Expect(text, ref index, '"');
            var builder = new StringBuilder();
            while (true)
            {
                if (index >= text.Length)
                {
                    throw new FormatException("JSON 字符串未闭合");
                }

                var ch = text[index++];
                if (ch == '"')
                {
                    return builder.ToString();
                }

                if (ch != '\\')
                {
                    builder.Append(ch);
                    continue;
                }

                if (index >= text.Length)
                {
                    throw new FormatException("JSON 转义序列未完成");
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
                            throw new FormatException("JSON \\u 转义不完整");
                        }

                        var hex = text.Substring(index, 4);
                        if (!int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var code))
                        {
                            throw new FormatException("JSON \\u 转义非法：" + hex);
                        }

                        builder.Append((char)code);
                        index += 4;
                        break;
                    default:
                        throw new FormatException("JSON 未知转义：\\" + escape);
                }
            }
        }
    }
}
