using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ps1Run.Core.Infrastructure;
using Ps1Run.Core.Settings;

namespace Ps1Run.Core.Configuration
{
    /// <summary>
    /// 单文件配置读写（软件根目录 config.json），供 <see cref="ConfigStore"/> 与
    /// <see cref="SettingsStore"/> 共用：两者操作同一份文件的不同字段，
    /// 写盘时先读回文件、只改自己负责的键，避免互相覆盖。
    /// 缺失字段用默认值、损坏文件备份重建、临时文件 + 替换的原子写入。
    /// </summary>
    public static class ConfigDocument
    {
        public const string VersionKey = "Version";
        public const string RunModeKey = "RunMode";
        public const string PowerShellPolicyKey = "PowerShellPolicy";
        public const string CustomPs5PathKey = "CustomPs5Path";
        public const string CustomPs7PathKey = "CustomPs7Path";
        public const string CustomEditorPathKey = "CustomEditorPath";
        public const string LogDaysKey = "LogDays";
        public const string ThemeKey = "Theme";
        public const string LanguageKey = "Language";

        /// <summary>写盘顺序：版本号在最前，其后行为设置，最后程序配置。</summary>
        private static readonly string[] CanonicalOrder =
        {
            VersionKey,
            RunModeKey,
            PowerShellPolicyKey,
            CustomPs5PathKey,
            CustomPs7PathKey,
            CustomEditorPathKey,
            LogDaysKey,
            ThemeKey,
            LanguageKey,
        };

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>旧版设置文件位置（%APPDATA%\Ps1Run\settings.json），仅用于一次性合并迁移。</summary>
        public static string LegacySettingsFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Ps1Run",
            "settings.json");

        /// <summary>
        /// 读取完整配置。文件不存在时按默认值生成；发现缺少版本号（旧文件）时
        /// 合并旧版 settings.json 并补写版本号。
        /// </summary>
        public static AppState Load(
            string filePath,
            bool importLegacySettings,
            string? legacySettingsFile = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("配置文件路径不能为空", nameof(filePath));
            }

            var legacyFile = string.IsNullOrEmpty(legacySettingsFile)
                ? LegacySettingsFile
                : legacySettingsFile!;

            if (!File.Exists(filePath))
            {
                var imported = importLegacySettings ? TryReadLegacySettings(legacyFile) : null;
                var fresh = new AppState
                {
                    Version = AppState.CurrentVersion,
                    Config = AppConfig.Default,
                    Settings = imported ?? AppSettings.Default,
                };

                var saved = TrySave(filePath, fresh);
                if (imported is not null)
                {
                    DiagnosticsLog.Info(
                        "已合并旧设置文件：" + legacyFile + " → " + filePath
                        + (saved && TryDeleteLegacySettings(legacyFile) ? "（原文件已删除）" : "（原文件保留：配置写入或删除失败）"));
                }

                return fresh;
            }

            try
            {
                var values = SimpleJson.ParseObject(File.ReadAllText(filePath, Encoding.UTF8));
                var hasVersion = values.ContainsKey(VersionKey);

                var state = new AppState
                {
                    Version = GetInt(values, VersionKey, 0),
                    Config = AppConfig.Normalize(new AppConfig
                    {
                        LogDays = GetInt(values, LogDaysKey, AppConfig.DefaultLogDays),
                        Theme = GetEnum(values, ThemeKey, AppConfig.Default.Theme),
                        Language = GetString(values, LanguageKey, AppConfig.DefaultLanguage),
                    }),
                    Settings = new AppSettings
                    {
                        RunMode = GetEnum(values, RunModeKey, AppSettings.Default.RunMode),
                        PowerShellPolicy = GetEnum(values, PowerShellPolicyKey, AppSettings.Default.PowerShellPolicy),
                        CustomPs5Path = GetStringOrNull(values, CustomPs5PathKey),
                        CustomPs7Path = GetStringOrNull(values, CustomPs7PathKey),
                        CustomEditorPath = GetStringOrNull(values, CustomEditorPathKey),
                    },
                };

                if (!hasVersion && importLegacySettings)
                {
                    // 旧版 config.json（无版本号）：把 settings.json 的内容并进来，再补写版本号
                    var imported = TryReadLegacySettings(legacyFile);
                    state = state with
                    {
                        Version = AppState.CurrentVersion,
                        Settings = imported ?? state.Settings,
                    };

                    var saved = TrySave(filePath, state);
                    DiagnosticsLog.Info(
                        "配置文件缺少版本号，已补写 Version=" + AppState.CurrentVersion.ToString(CultureInfo.InvariantCulture)
                        + (imported is null
                            ? string.Empty
                            : "，并合并旧设置文件 " + legacyFile
                              + (saved && TryDeleteLegacySettings(legacyFile) ? "（原文件已删除）" : "（原文件保留：配置写入或删除失败）"))
                        + "：" + filePath);
                }
                else if (importLegacySettings && File.Exists(legacyFile))
                {
                    // 已有版本号说明迁移已完成，清掉上次保留的旧文件；
                    // 删除失败不影响本次读取，下次启动会再试。
                    TryDeleteLegacySettings(legacyFile, "旧设置文件已迁移过，清理残留");
                }

                return state;
            }
            catch (IOException ex)
            {
                return Recover(filePath, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Recover(filePath, ex.Message);
            }
            catch (FormatException ex)
            {
                return Recover(filePath, ex.Message);
            }
        }

        /// <summary>写入完整配置；文件里已有的未知键会原样保留。</summary>
        public static void Save(string filePath, AppState state)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var values = ReadRaw(filePath);

            var config = AppConfig.Normalize(state.Config);
            var settings = state.Settings ?? AppSettings.Default;

            values[VersionKey] = AppState.CurrentVersion.ToString(CultureInfo.InvariantCulture);
            values[RunModeKey] = settings.RunMode.ToString();
            values[PowerShellPolicyKey] = settings.PowerShellPolicy.ToString();
            values[CustomPs5PathKey] = settings.CustomPs5Path ?? string.Empty;
            values[CustomPs7PathKey] = settings.CustomPs7Path ?? string.Empty;
            values[CustomEditorPathKey] = settings.CustomEditorPath ?? string.Empty;
            values[LogDaysKey] = config.LogDays.ToString(CultureInfo.InvariantCulture);
            values[ThemeKey] = config.Theme.ToString();
            values[LanguageKey] = config.Language;

            WriteValues(filePath, values);
        }

        private static AppState Recover(string filePath, string reason)
        {
            BackupCorrupted(filePath, reason);
            var recovered = AppState.Default with { Version = AppState.CurrentVersion };
            TrySave(filePath, recovered);
            return recovered;
        }

        /// <summary>尝试写盘；返回 false 时调用方必须保留旧设置文件，避免迁移丢数据。</summary>
        private static bool TrySave(string filePath, AppState state)
        {
            try
            {
                Save(filePath, state);
                return true;
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Warn("生成默认 config.json 失败：" + ex.Message);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Warn("生成默认 config.json 失败：" + ex.Message);
                return false;
            }
        }

        private static void BackupCorrupted(string filePath, string reason)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return;
                }

                var stamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                var backup = filePath + ".bad." + stamp;
                File.Copy(filePath, backup, overwrite: false);
                DiagnosticsLog.Warn(
                    "config.json 损坏，已备份并重建默认值：file=" + filePath
                    + " backup=" + backup + " reason=" + reason);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>读回现有键值；文件缺失或损坏时返回空表（写入方会覆盖成完整默认值）。</summary>
        private static Dictionary<string, string> ReadRaw(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                return SimpleJson.ParseObject(File.ReadAllText(filePath, Encoding.UTF8));
            }
            catch (IOException)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (UnauthorizedAccessException)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void WriteValues(string filePath, Dictionary<string, string> values)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");

            var written = new List<string>(CanonicalOrder.Length);
            foreach (var key in CanonicalOrder)
            {
                if (values.ContainsKey(key))
                {
                    written.Add(key);
                }
            }

            // 未知键（用户手工添加或未来字段）原样保留，按字母序追加
            var extras = new List<string>();
            foreach (var key in values.Keys)
            {
                if (Array.IndexOf(CanonicalOrder, key) < 0)
                {
                    extras.Add(key);
                }
            }

            extras.Sort(StringComparer.OrdinalIgnoreCase);
            written.AddRange(extras);

            for (var i = 0; i < written.Count; i++)
            {
                var key = written[i];
                builder.Append("  ")
                    .Append(SimpleJson.Quote(key))
                    .Append(": ")
                    .Append(FormatValue(key, values[key]))
                    .AppendLine(i == written.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("}");

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = filePath + ".tmp";
            File.WriteAllText(temp, builder.ToString(), Utf8NoBom);

            if (File.Exists(filePath))
            {
                File.Replace(temp, filePath, null);
            }
            else
            {
                File.Move(temp, filePath);
            }
        }

        /// <summary>数字字段不加引号，其余一律写成 JSON 字符串。</summary>
        private static string FormatValue(string key, string value)
        {
            if (string.Equals(key, VersionKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, LogDaysKey, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            return SimpleJson.Quote(value ?? string.Empty);
        }

        /// <summary>合并成功后删除旧设置文件；删除失败只记日志，不影响启动。</summary>
        private static bool TryDeleteLegacySettings(string filePath, string? reason = null)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                File.Delete(filePath);
                DiagnosticsLog.Info(
                    (reason ?? "旧设置文件已成功合并，删除原文件") + "：" + filePath);
                return true;
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Warn("删除旧设置文件失败（下次启动会重试）：" + ex.Message);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Warn("删除旧设置文件失败（下次启动会重试）：" + ex.Message);
                return false;
            }
        }

        /// <summary>读取旧版 settings.json（只解析，不迁移、不删除）。</summary>
        private static AppSettings? TryReadLegacySettings(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var values = SimpleJson.ParseObject(File.ReadAllText(filePath, Encoding.UTF8));
                if (values.Count == 0)
                {
                    return null;
                }

                return new AppSettings
                {
                    RunMode = GetEnum(values, RunModeKey, AppSettings.Default.RunMode),
                    PowerShellPolicy = GetEnum(values, PowerShellPolicyKey, AppSettings.Default.PowerShellPolicy),
                    CustomPs5Path = GetStringOrNull(values, CustomPs5PathKey),
                    CustomPs7Path = GetStringOrNull(values, CustomPs7PathKey),
                    CustomEditorPath = GetStringOrNull(values, CustomEditorPathKey),
                };
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Warn("读取旧设置文件失败：" + ex.Message);
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Warn("读取旧设置文件失败：" + ex.Message);
                return null;
            }
            catch (FormatException ex)
            {
                DiagnosticsLog.Warn("旧设置文件格式非法，已忽略：" + ex.Message);
                return null;
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
                if (text.Length > 0)
                {
                    return text;
                }
            }

            return fallback;
        }

        private static string? GetStringOrNull(Dictionary<string, string> values, string key)
        {
            return values.TryGetValue(key, out var raw) && raw.Length > 0 ? raw : null;
        }

        private static T GetEnum<T>(Dictionary<string, string> values, string key, T fallback)
            where T : struct
        {
            if (values.TryGetValue(key, out var raw))
            {
                var text = raw.Trim().Trim('"');
                if (Enum.TryParse<T>(text, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }
            }

            return fallback;
        }
    }
}
