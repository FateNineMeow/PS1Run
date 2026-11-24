using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.Core.Configuration
{
    /// <summary>
    /// config.json 持久化（软件根目录，默认内容见 <see cref="AppConfig"/>）。
    /// 首次运行自动生成；文件损坏时备份为 config.json.bad.时间戳 并重建默认；
    /// 写入采用「临时文件 + 替换」，避免断电/崩溃留下半截文件。
    /// </summary>
    public sealed class ConfigStore
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public ConfigStore(string? filePath = null)
        {
            FilePath = filePath ?? AppPaths.ConfigFile;
        }

        public string FilePath { get; }

        /// <summary>读取配置；文件不存在时按默认值生成，保证用户能直接看到并编辑 schema。</summary>
        public AppConfig Load()
        {
            if (!File.Exists(FilePath))
            {
                var created = AppConfig.Default;
                TrySave(created);
                return created;
            }

            try
            {
                var json = File.ReadAllText(FilePath, Encoding.UTF8);
                var values = SimpleJson.ParseObject(json);

                var config = new AppConfig
                {
                    LogDays = GetInt(values, "LogDays", AppConfig.DefaultLogDays),
                    Theme = GetEnum(values, "Theme", AppConfig.Default.Theme),
                    Language = GetString(values, "Language", AppConfig.DefaultLanguage),
                };

                return AppConfig.Normalize(config);
            }
            catch (IOException ex)
            {
                BackupCorrupted(ex.Message);
                return AppConfig.Default;
            }
            catch (UnauthorizedAccessException ex)
            {
                BackupCorrupted(ex.Message);
                return AppConfig.Default;
            }
            catch (FormatException ex)
            {
                BackupCorrupted(ex.Message);
                return AppConfig.Default;
            }
        }

        public void Save(AppConfig config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var normalized = AppConfig.Normalize(config);
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.Append("  \"LogDays\": ").Append(normalized.LogDays.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("  \"Theme\": ").Append(SimpleJson.Quote(normalized.Theme.ToString())).AppendLine(",");
            builder.Append("  \"Language\": ").Append(SimpleJson.Quote(normalized.Language)).AppendLine();
            builder.AppendLine("}");

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, builder.ToString(), Utf8NoBom);

            if (File.Exists(FilePath))
            {
                File.Replace(temp, FilePath, null);
            }
            else
            {
                File.Move(temp, FilePath);
            }
        }

        private void TrySave(AppConfig config)
        {
            try
            {
                Save(config);
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Warn("生成默认 config.json 失败：" + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Warn("生成默认 config.json 失败：" + ex.Message);
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
                DiagnosticsLog.Warn(
                    "config.json 损坏，已备份并重建默认值：file=" + FilePath
                    + " backup=" + backup + " reason=" + reason);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static int GetInt(Dictionary<string, string> values, string key, int fallback)
        {
            if (values.TryGetValue(key, out var raw))
            {
                var text = raw.Trim().Trim('"');
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return fallback;
        }

        private static string GetString(Dictionary<string, string> values, string key, string fallback)
        {
            if (values.TryGetValue(key, out var raw))
            {
                var text = raw.Trim();
                return text.Length == 0 ? fallback : text;
            }

            return fallback;
        }

        private static AppTheme GetEnum(Dictionary<string, string> values, string key, AppTheme fallback)
        {
            if (values.TryGetValue(key, out var raw))
            {
                var text = raw.Trim().Trim('"');
                if (Enum.TryParse<AppTheme>(text, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }
            }

            return fallback;
        }
    }
}
