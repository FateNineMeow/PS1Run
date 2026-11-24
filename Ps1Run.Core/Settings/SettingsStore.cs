using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ps1Run.Core.Settings
{
    /// <summary>
    /// 设置持久化：%APPDATA%\Ps1Run\settings.json。
    /// 为保持 net48 零额外依赖，这里实现固定 schema 的轻量 JSON 读写，
    /// 行为与 System.Text.Json 等价：缺失用默认值、损坏备份重建、原子写入。
    /// </summary>
    public sealed class SettingsStore
    {
        private const string DefaultFileName = "settings.json";

        public SettingsStore(string? filePath = null)
        {
            FilePath = filePath ?? Path.Combine(DefaultDirectory(), DefaultFileName);
        }

        public string FilePath { get; }

        public static string DefaultDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Ps1Run");
        }

        public AppSettings Load()
        {
            if (!File.Exists(FilePath))
            {
                return AppSettings.Default;
            }

            try
            {
                var json = File.ReadAllText(FilePath, Encoding.UTF8);
                var values = ParseSimpleJson(json);
                return new AppSettings
                {
                    RunMode = ParseEnum(values, "RunMode", AppSettings.Default.RunMode),
                    PowerShellPolicy = ParseEnum(values, "PowerShellPolicy", AppSettings.Default.PowerShellPolicy),
                    CustomPs5Path = GetString(values, "CustomPs5Path"),
                    CustomPs7Path = GetString(values, "CustomPs7Path"),
                    CustomEditorPath = GetString(values, "CustomEditorPath"),
                };
            }
            catch (IOException ex)
            {
                BackupCorrupted(ex.Message);
                return AppSettings.Default;
            }
            catch (UnauthorizedAccessException ex)
            {
                BackupCorrupted(ex.Message);
                return AppSettings.Default;
            }
            catch (FormatException ex)
            {
                BackupCorrupted(ex.Message);
                return AppSettings.Default;
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.Append("  \"RunMode\": \"").Append(Escape(settings.RunMode.ToString())).AppendLine("\",");
            builder.Append("  \"PowerShellPolicy\": \"").Append(Escape(settings.PowerShellPolicy.ToString())).AppendLine("\",");
            builder.Append("  \"CustomPs5Path\": \"").Append(Escape(settings.CustomPs5Path ?? string.Empty)).AppendLine("\",");
            builder.Append("  \"CustomPs7Path\": \"").Append(Escape(settings.CustomPs7Path ?? string.Empty)).AppendLine("\",");
            builder.Append("  \"CustomEditorPath\": \"").Append(Escape(settings.CustomEditorPath ?? string.Empty)).AppendLine("\"");
            builder.AppendLine("}");

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 原子写入：先写临时文件，再替换（wiki 02 F5.3）
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, builder.ToString(), new UTF8Encoding(false));

            if (File.Exists(FilePath))
            {
                File.Replace(temp, FilePath, null);
            }
            else
            {
                File.Move(temp, FilePath);
            }
        }

        private void BackupCorrupted(string reason)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }

                var stamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                var backup = FilePath + ".bad." + stamp;
                File.Copy(FilePath, backup, overwrite: false);
                Infrastructure.DiagnosticsLog.Warn(
                    "设置文件损坏，已备份并重建：file=" + FilePath
                    + " backup=" + backup + " reason=" + reason);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static Dictionary<string, string> ParseSimpleJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new FormatException("设置文件为空");
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in json.Split('\n'))
            {
                var line = rawLine.Trim().TrimEnd('\r').Trim();
                if (line.Length == 0 || line == "{" || line == "}")
                {
                    continue;
                }

                if (line.EndsWith(",", StringComparison.Ordinal))
                {
                    line = line.Substring(0, line.Length - 1).Trim();
                }

                var colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    throw new FormatException("设置文件行格式非法：" + line);
                }

                var key = line.Substring(0, colon).Trim().Trim('"');
                var value = line.Substring(colon + 1).Trim();
                if (!value.StartsWith("\"", StringComparison.Ordinal) || !value.EndsWith("\"", StringComparison.Ordinal))
                {
                    throw new FormatException("设置项不是字符串：" + key);
                }

                result[key] = Unescape(value.Substring(1, value.Length - 2));
            }

            if (!result.ContainsKey("RunMode") && !result.ContainsKey("PowerShellPolicy"))
            {
                throw new FormatException("设置文件缺少关键字段");
            }

            return result;
        }

        private static string Escape(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string Unescape(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\' || i + 1 >= value.Length)
                {
                    builder.Append(value[i]);
                    continue;
                }

                i++;
                switch (value[i])
                {
                    case '\\': builder.Append('\\'); break;
                    case '"': builder.Append('"'); break;
                    case 'r': builder.Append('\r'); break;
                    case 'n': builder.Append('\n'); break;
                    case 't': builder.Append('\t'); break;
                    default: builder.Append(value[i]); break;
                }
            }

            return builder.ToString();
        }

        private static T ParseEnum<T>(Dictionary<string, string> values, string key, T fallback)
            where T : struct
        {
            if (values.TryGetValue(key, out var raw) && Enum.TryParse<T>(raw, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static string? GetString(Dictionary<string, string> values, string key)
        {
            return values.TryGetValue(key, out var raw) && raw.Length > 0 ? raw : null;
        }
    }
}
