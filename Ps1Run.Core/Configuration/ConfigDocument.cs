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
    /// 单文件配置读写（软件根目录 config.cfg，纯文本键值格式，与语言包同款），
    /// 供 <see cref="ConfigStore"/> 与 <see cref="SettingsStore"/> 共用：
    /// 两者操作同一份文件的不同字段，写盘时先读回文件、只改自己负责的键，避免互相覆盖。
    /// 缺失字段用默认值、损坏文件备份重建、临时文件 + 替换的原子写入。
    /// 旧 config.json / settings.json 不再读取、不再迁移。
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
        public const string RuleMatchSizeKey = "RuleMatchSize";
        public const string RuleMatchModifiedTimeKey = "RuleMatchModifiedTime";
        public const string RuleMatchHashKey = "RuleMatchHash";

        /// <summary>写盘顺序：版本号在最前，其后为行为设置，最后程序配置与规则标准。</summary>
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
            RuleMatchSizeKey,
            RuleMatchModifiedTimeKey,
            RuleMatchHashKey,
        };

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// 读取完整配置。文件不存在时按默认值生成；
        /// 发现版本号不是当前值时补写为 2 并按默认值补齐新增字段。
        /// </summary>
        public static AppState Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("配置文件路径不能为空", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                var fresh = AppState.Default with { Version = AppState.CurrentVersion };
                TrySave(filePath, fresh);
                return fresh;
            }

            try
            {
                var values = KeyValueTextParser.Parse(File.ReadAllText(filePath, Encoding.UTF8));
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
                        RuleMatchSize = GetBool(values, RuleMatchSizeKey, AppSettings.Default.RuleMatchSize),
                        RuleMatchModifiedTime = GetBool(values, RuleMatchModifiedTimeKey, AppSettings.Default.RuleMatchModifiedTime),
                        RuleMatchHash = GetBool(values, RuleMatchHashKey, AppSettings.Default.RuleMatchHash),
                    },
                };

                if (state.Version != AppState.CurrentVersion)
                {
                    DiagnosticsLog.Warn(
                        "配置文件版本不是 " + AppState.CurrentVersion.ToString(CultureInfo.InvariantCulture)
                        + "，已按当前默认值补齐并重写：" + filePath);
                    state = state with { Version = AppState.CurrentVersion };
                    TrySave(filePath, state);
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
            values[RuleMatchSizeKey] = Number(settings.RuleMatchSize);
            values[RuleMatchModifiedTimeKey] = Number(settings.RuleMatchModifiedTime);
            values[RuleMatchHashKey] = Number(settings.RuleMatchHash);

            WriteValues(filePath, values);
        }

        private static AppState Recover(string filePath, string reason)
        {
            BackupCorrupted(filePath, reason);
            var recovered = AppState.Default with { Version = AppState.CurrentVersion };
            TrySave(filePath, recovered);
            return recovered;
        }

        /// <summary>尝试写盘；返回 false 时调用方需自行处理（不阻断启动）。</summary>
        private static bool TrySave(string filePath, AppState state)
        {
            try
            {
                Save(filePath, state);
                return true;
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Warn("生成默认 config.cfg 失败：" + ex.Message);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Warn("生成默认 config.cfg 失败：" + ex.Message);
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
                    "config.cfg 损坏，已备份并重建默认值：file=" + filePath
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

                return KeyValueTextParser.Parse(File.ReadAllText(filePath, Encoding.UTF8));
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

            foreach (var key in written)
            {
                builder.Append(KeyValueTextParser.FormatLine(key, FormatValue(key, values[key])));
            }

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

        /// <summary>数字字段不加引号，其余一律写成文本字符串。</summary>
        private static string FormatValue(string key, string value)
        {
            if (string.Equals(key, VersionKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, LogDaysKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, RuleMatchSizeKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, RuleMatchModifiedTimeKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, RuleMatchHashKey, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            return KeyValueTextParser.Quote(value ?? string.Empty);
        }

        private static string Number(bool value) => value ? "1" : "0";

        private static int GetInt(Dictionary<string, string> values, string key, int fallback)
        {
            if (values.TryGetValue(key, out var raw)
                && int.TryParse(raw.Trim().Trim('"'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static bool GetBool(Dictionary<string, string> values, string key, bool fallback)
        {
            if (!values.TryGetValue(key, out var raw))
            {
                return fallback;
            }

            var text = raw.Trim().Trim('"');
            if (text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (text == "0" || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
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
            if (values.TryGetValue(key, out var raw)
                && Enum.TryParse<T>(raw.Trim().Trim('"'), ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
