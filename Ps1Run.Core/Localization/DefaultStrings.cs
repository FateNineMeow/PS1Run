using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.Core.Localization
{
    /// <summary>
    /// 内置中文文案（默认语言 zh-cn）。
    /// 两个用途：① 首次运行生成 lang\zh-cn.lang；② 任何语言文件缺键时的兜底，
    /// 避免界面出现空白或键名。
    /// </summary>
    public static class DefaultStrings
    {
        public const string DefaultLanguageCode = "zh-cn";

        /// <summary>
        /// 语言包版本号。语言包结构或键集合有较大变化时递增；
        /// 语言文件缺少该版本号（或低于当前值）说明是旧语言包，界面会提示更新。
        /// </summary>
        public const int PackVersion = 2;

        /// <summary>语言包中记录版本号的保留键（不是界面文案，加载时会被剔除）。</summary>
        public const string PackVersionKey = "Version";

        /// <summary>语言代码 → 下拉框显示名（语言自称，不随界面语言变化）。</summary>
        private static readonly Dictionary<string, string> DisplayNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "zh-cn", "简体中文" },
                { "zh-tw", "繁體中文" },
                { "en-us", "English" },
                { "ja-jp", "日本語" },
                { "ko-kr", "한국어" },
            };

        public static IReadOnlyDictionary<string, string> ZhCn { get; } = Build();

        public static string Describe(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return DefaultLanguageCode;
            }

            return DisplayNames.TryGetValue(code.Trim(), out var name) ? name : code.Trim();
        }

        /// <summary>
        /// 把默认文案序列化成 lang\zh-cn.lang 的内容（首行为版本号，键按字母序，便于人工比对）。
        /// </summary>
        public static string ToLang()
        {
            var builder = new StringBuilder();
            builder.Append(KeyValueTextParser.FormatLine(
                PackVersionKey, PackVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            var entries = ZhCn.OrderBy(pair => pair.Key, StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                builder.Append(KeyValueTextParser.FormatLine(entry.Key, KeyValueTextParser.Quote(entry.Value)));
            }

            return builder.ToString();
        }

        private static Dictionary<string, string> Build()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // 主界面：页签与状态徽标
                ["Tab.General"] = "常规",
                ["Tab.Association"] = "文件关联",
                ["Tab.LogsAndAppearance"] = "日志与外观",
                ["Tab.Rules"] = "脚本规则",
                ["Badge.Registered"] = "已注册",
                ["Badge.Partial"] = "部分注册",
                ["Badge.NotRegistered"] = "未注册",

                // 主界面：标题与分组
                ["App.Title"] = "PS1Run — PowerShell 脚本启动器",
                ["App.Subtitle"] = "决定双击 .ps1 时交给编辑器还是解释器",
                ["Group.RunMode"] = "运行方式（双击行为）",
                ["Group.Executor"] = "执行程序与编辑器",
                ["Group.Detect"] = "自动探测（一键识别）",
                ["Group.RuleCriteria"] = "脚本规则判断标准",
                ["Group.RuleList"] = "规则列表",
                ["Group.Appearance"] = "外观与语言",
                ["Group.Logs"] = "日志",
                ["Group.SettingsFiles"] = "配置文件与版本",
                ["Group.AssociationStatus"] = "关联状态",
                ["Group.Actions"] = "操作",
                ["Label.Verbs"] = "动词",
                ["Label.Extension"] = "扩展声明",
                ["Hint.AutoSaved"] = "更改即时生效，无需手动保存。",
                ["Hint.Association"] = "关联写入当前用户（HKCU\\Software\\Classes）；机器级注册需要管理员确认。系统“默认应用”由 UserChoice 决定，改变它需手动确认一次。",
                ["Button.Close"] = "关闭",

                // 主界面：运行方式
                ["RunMode.Run"] = "执行（直接运行脚本）",
                ["RunMode.Edit"] = "编辑（用编辑器打开）",
                ["RunMode.Ask"] = "选择（每次双击都弹窗，默认）",
                ["Hint.RunMode"] = "右键菜单的“运行 / 编辑”属显式意图，始终绕过“选择”。",

                // 主界面：执行程序
                ["Policy.Ps5"] = "Windows PowerShell 5.1",
                ["Policy.Ps7"] = "PowerShell 7+",
                ["Policy.Auto"] = "自动（按脚本编码选择）",
                ["Hint.Executor"] = "自动模式：UTF-8 / UTF-16 交给 PS7，ANSI（GBK）交给 PS5。缺失时可在下方手动指定。",
                ["Label.Ps5"] = "PS5.1",
                ["Label.Ps7"] = "PS7+",
                ["Button.Browse"] = "浏览…",

                // 主界面：编辑器与探测
                ["Label.Editor"] = "编辑器",
                ["Button.Detect"] = "一键探测 PS5 / PS7 / 编辑程序",

                // 主界面：外观、语言与日志
                ["Label.Theme"] = "主题",
                ["Theme.Auto"] = "自动（跟随系统）",
                ["Theme.Light"] = "浅色",
                ["Theme.Dark"] = "深色",
                ["Label.Language"] = "语言",
                ["Dialog.RestartForLanguage"] = "语言已切换为 {0}。新语言将在重启应用后生效。是否立即重启？",
                ["Dialog.RestartFailed"] = "无法自动重启应用，请手动关闭并重新打开。",
                ["Label.LogDays"] = "日志保留天数",
                ["Hint.LogDays"] = "每天首次运行自动清理更早的日志（1–365 天）。",
                ["Button.PruneNow"] = "立即清理",
                ["Label.ConfigFile"] = "配置文件",
                ["Button.OpenConfig"] = "打开",
                ["Label.AppVersion"] = "程序版本",
                ["Label.LanguagePack"] = "语言包版本",

                // 主界面：版本提醒（语言文件缺少版本号）
                ["Warning.Title"] = "语言包版本提醒",
                ["Warning.LanguagePackOutdated"] = "语言包 {0} 缺少版本号或低于当前版本（v{1}），部分文字会回退为中文。可重新生成 zh-cn.lang；其他语言包需自行更新，程序不会自动覆盖。",
                ["Warning.NoVersion"] = "无",
                ["Button.RebuildLanguagePack"] = "重新生成 zh-cn.lang",
                ["Hint.Version"] = "版本号用于判断配置文件与语言包是否与当前程序匹配。",

                // 主界面：操作按钮
                ["Button.Save"] = "保存设置",
                ["Button.Register"] = "注册关联",
                ["Button.Unregister"] = "注销关联",
                ["Button.OpenWith"] = "系统“打开方式”…",
                ["Button.OpenDefaultApps"] = "打开默认应用设置",
                ["Button.RegisterMachine"] = "注册到所有用户（管理员）",
                ["Button.UnregisterMachine"] = "注销机器级关联",
                ["Button.OpenLogFolder"] = "打开日志目录",
                ["Button.OpenCurrentLog"] = "查看当日日志",
                ["Button.CopyRecentLogs"] = "复制最近日志",
                ["Button.RuleRefresh"] = "刷新",
                ["Button.RuleAdd"] = "新增",
                ["Button.RuleEdit"] = "编辑规则",
                ["Button.RuleDelete"] = "删除",
                ["Button.RuleSave"] = "保存规则",
                ["Button.SelectScript"] = "选择文件…",
                ["Button.RuleOk"] = "确定",
                ["Button.RuleCancel"] = "取消",

                // 脚本规则页
                ["Label.RuleSize"] = "文件大小",
                ["Label.RuleModifiedTime"] = "修改时间",
                ["Label.RuleHash"] = "文件 HASH",
                ["Label.RuleSearch"] = "按路径筛选：",
                ["Label.RulePathInput"] = "脚本路径（可直接输入，也可选择文件）",
                ["Label.RuleAction"] = "处理方式",
                ["Hint.RulePathAlways"] = "文件路径默认参与匹配，不可取消。",
                ["Hint.RuleHash"] = "可能减缓脚本启动速度：需读取整个脚本计算 SHA-256。",
                ["Column.RulePath"] = "文件路径",
                ["Column.RuleAction"] = "动作",
                ["Column.RuleSize"] = "大小（字节）",
                ["Column.RuleModifiedTime"] = "修改时间（UTC）",
                ["Column.RuleHash"] = "文件 HASH",
                ["Rule.Action.Run"] = "执行",
                ["Rule.Action.Edit"] = "编辑",
                ["Rule.Empty"] = "暂无规则。可在选择窗勾选「仅此脚本」，或点「新增」手动创建。",
                ["Rule.Count"] = "共 {0} 条规则",
                ["Rule.Saved"] = "规则已保存到 ",
                ["Rule.SaveFailed"] = "保存规则失败：",
                ["Rule.Deleted"] = "已删除规则：",
                ["Rule.Refreshed"] = "已重新载入规则：",
                ["Rule.RefreshConfirm"] = "当前有未保存的规则修改，刷新将丢弃这些修改。继续？",
                ["Rule.CriteriaSaved"] = "判断标准已更新，规则指纹已同步重写。",
                ["Rule.EditorTitle.Add"] = "新增脚本规则",
                ["Rule.EditorTitle.Edit"] = "编辑脚本规则",
                ["Rule.InvalidExtension"] = "仅支持 .ps1 文件",
                ["Rule.FileMissing"] = "文件不存在，无法生成指纹",
                ["Rule.PathExists"] = "该路径已存在规则，保存后将覆盖原规则。",
                ["Rule.DeleteConfirm"] = "确认删除这条规则？",
                ["Rule.FingerprintPreview"] = "指纹预览：大小 {0} 字节 · 修改时间 {1} · HASH {2}",
                ["Rule.CriteriaSummary"] = "当前判断标准：文件路径（必选）{0}{1}{2}",
                ["Rule.Filter.Exe"] = "PowerShell 脚本 (*.ps1)|*.ps1|所有文件 (*.*)|*.*",

                // 主界面：视图模型提示
                ["Main.Detection.Idle"] = "尚未探测。点击左侧按钮按探测链识别 PS5 / PS7 / 编辑程序；已配置且文件存在的程序保持不变。",
                ["Main.Detection.Done"] = "一键识别完成。",
                ["Main.Detection.Saved"] = "结果已自动保存。",
                ["Main.Detection.NotFound"] = "未找到（保留原设置）",
                ["Main.Detection.Kept"] = "保留原设置，未修改",
                ["Main.Detection.Ps5Label"] = "PS5.1",
                ["Main.Detection.Ps7Label"] = "PS7+",
                ["Main.Detection.EditorLabel"] = "编辑器",
                ["Main.Filter.Exe"] = "可执行文件",
                ["Main.Filter.All"] = "所有文件",
                ["Main.Filter.PowerShell"] = "PowerShell 可执行文件",
                ["Main.Saved"] = "设置已保存：",
                ["Main.SaveFailed"] = "保存失败：",
                ["Main.PickEditorTitle"] = "选择编辑程序",
                ["Main.PickEditorSelected"] = "已选择编辑器：",
                ["Main.PickPowerShellTitle"] = "选择 ",
                ["Main.PickPowerShellSelected"] = "已指定 ",
                ["Main.AutoSavedSuffix"] = "（已自动保存）",
                ["Main.Elevation.Register"] = "已请求管理员权限，请在 UAC 对话框中确认机器级注册。",
                ["Main.Elevation.Unregister"] = "已请求管理员权限，请在 UAC 对话框中确认机器级注销。",
                ["Main.Elevation.Denied"] = "未能获取管理员权限（UAC 被取消或被拒绝）。",
                ["Main.OpenDefaultAppsFailed"] = "无法打开默认应用设置：",
                ["Main.OpenLogFolderFailed"] = "无法打开日志目录：",
                ["Main.LogFolderOpened"] = "日志目录：",
                ["Main.OpenLogFailed"] = "无法打开当日日志：",
                ["Main.OpenConfigFailed"] = "无法打开配置文件：",
                ["Main.LogDays.Invalid"] = "日志保留天数需为 1–365 的整数。",
                ["Main.LogDays.Saved"] = "日志保留天数已设为 ",
                ["Main.LogDays.Unit"] = " 天。",
                ["Main.PruneResult"] = "已清理过期日志：",
                ["Main.PruneUnit"] = " 个文件。",
                ["Main.VersionMissing"] = "缺少版本号",
                ["Main.LanguagePackRebuilt"] = "已重新生成语言包：",
                ["Main.LanguagePackRebuildFailed"] = "重新生成语言包失败：",

                // 主界面：关联状态卡片标签
                ["Status.Added"] = "已加入",
                ["Status.NotAdded"] = "未加入",
                ["Status.ExecutablePath"] = "程序路径：",
                ["Status.UserChoice"] = "UserChoice：",
                ["Status.UserChoice.ReadFailed"] = "无法读取 UserChoice（权限不足），默认程序状态未知。",
                ["Status.UserChoice.None"] = "系统未记录 UserChoice，ProgId 关联可直接生效。",
                ["Status.UserChoice.PointsToUs"] = "系统默认程序已指向本程序。",
                ["Status.UserChoice.Overridden"] = "系统当前默认程序为 {0}，本程序可能不生效；请在“打开方式”中手动勾选并确认（不允许伪造 Hash）。",
                ["Status.LogDirectory"] = "日志目录：",

                // 选择窗
                ["Picker.Title"] = "选择处理方式",
                ["Picker.Remember"] = "以后都这样处理（不再询问）",
                ["Picker.RememberScript"] = "以后都这样处理（仅此脚本）",
                ["Picker.RememberScriptTip"] = "按当前判断标准记录到 rule.txt；文件变化后可能不再命中。",
                ["Picker.Cancel"] = "取消 (Esc)",
                ["Picker.Edit"] = "编辑 (E)",
                ["Picker.Run"] = "执行 (Enter)",
                ["Picker.NoScript"] = "（未提供脚本路径）",
                ["Picker.Ps5Title"] = "执行时使用 Windows PowerShell 5.1",
                ["Picker.Ps7Title"] = "执行时使用 PowerShell 7+",
                ["Picker.UnknownTitle"] = "执行程序：未判定",
                ["Picker.NoInfo"] = "未获取到执行程序信息",
                ["Picker.PolicyPs5"] = "固定策略：PowerShell 5",
                ["Picker.PolicyPs7"] = "固定策略：PowerShell 7",
                ["Picker.AutoReason"] = "自动判定：",
                ["Picker.MissingExe"] = "未找到可执行文件",
                ["Picker.ConfigPs5Missing"] = "配置的 PowerShell 5.1 路径不存在：{0}",
                ["Picker.ConfigPs7Missing"] = "配置的 PowerShell 7+ 路径不存在：{0}",
                ["Picker.ConfigEditorMissing"] = "配置的编辑器路径不存在：{0}",
                ["Picker.EditorMissing"] = "未配置编辑器，且自动探测未找到可用编辑器；编辑操作将失败。",
                ["Picker.PackWarning"] = "语言包版本过旧（缺少版本号），部分文字可能显示为中文。",

                // 编码判型
                ["Encoding.Utf8Bom"] = "UTF-8 带 BOM",
                ["Encoding.Utf8NoBom"] = "UTF-8 无 BOM",
                ["Encoding.Utf16Le"] = "UTF-16 LE",
                ["Encoding.Utf16Be"] = "UTF-16 BE",
                ["Encoding.Ansi"] = "ANSI/GBK",
                ["Encoding.Empty"] = "空文件",
                ["Encoding.Unknown"] = "无法判定",
            };

            return map;
        }
    }
}
