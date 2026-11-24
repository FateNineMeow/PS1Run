using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.Core.Localization
{
    /// <summary>
    /// 内置中文文案（默认语言 zh-cn）。
    /// 两个用途：① 首次运行生成 lang\zh-cn.json；② 任何语言文件缺键时的兜底，
    /// 避免界面出现空白或键名。
    /// </summary>
    public static class DefaultStrings
    {
        public const string DefaultLanguageCode = "zh-cn";

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

        /// <summary>把默认文案序列化成 lang\zh-cn.json 的内容（键按字母序，便于人工比对）。</summary>
        public static string ToJson()
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            var entries = ZhCn.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList();
            for (var i = 0; i < entries.Count; i++)
            {
                builder.Append("  ")
                    .Append(SimpleJson.Quote(entries[i].Key))
                    .Append(": ")
                    .Append(SimpleJson.Quote(entries[i].Value));
                builder.AppendLine(i == entries.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("}");
            return builder.ToString();
        }

        private static Dictionary<string, string> Build()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // 主界面：标题与分组
                ["App.Title"] = "PS1Run — PowerShell 脚本启动器",
                ["App.Subtitle"] = "决定双击 .ps1 时交给编辑器还是解释器",
                ["Group.RunMode"] = "运行方式（双击行为）",
                ["Group.Executor"] = "执行程序",
                ["Group.Editor"] = "编辑程序",
                ["Group.Detect"] = "自动探测（一键识别）",
                ["Group.Preferences"] = "外观、语言与日志",
                ["Group.Diagnostics"] = "诊断信息",
                ["Group.Actions"] = "操作",

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
                ["Label.LogDays"] = "日志保留天数",
                ["Hint.LogDays"] = "每天首次运行自动清理更早的日志（1–365 天）。",
                ["Button.PruneNow"] = "立即清理",
                ["Label.ConfigFile"] = "配置文件",
                ["Button.OpenConfig"] = "打开",

                // 主界面：操作按钮
                ["Button.Save"] = "保存设置",
                ["Button.Refresh"] = "刷新状态",
                ["Button.Register"] = "注册关联",
                ["Button.Unregister"] = "注销关联",
                ["Button.OpenWith"] = "系统“打开方式”…",
                ["Button.OpenDefaultApps"] = "打开默认应用设置",
                ["Button.RegisterMachine"] = "注册到所有用户（管理员）",
                ["Button.UnregisterMachine"] = "注销机器级关联",
                ["Button.CopyDiagnostics"] = "复制诊断信息",
                ["Button.OpenLogFolder"] = "打开日志目录",
                ["Button.OpenCurrentLog"] = "查看当日日志",
                ["Button.CopyRecentLogs"] = "复制最近日志",

                // 主界面：视图模型提示
                ["Main.Detection.Idle"] = "尚未探测。点击左侧按钮按探测链识别 PS5 / PS7 / 编辑程序。",
                ["Main.Detection.Done"] = "一键探测完成。",
                ["Main.Detection.Saved"] = "结果已自动保存。",
                ["Main.Detection.NotFound"] = "未找到（保留原设置）",
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

                // 主界面：诊断快照标签
                ["Status.Association"] = "关联：ProgId ",
                ["Status.Registered"] = "已注册",
                ["Status.NotRegistered"] = "未注册",
                ["Status.Verbs"] = "；动词 ",
                ["Status.OpenWithProgids"] = "扩展声明（OpenWithProgids）：",
                ["Status.Added"] = "已加入",
                ["Status.NotAdded"] = "未加入",
                ["Status.ExecutablePath"] = "程序路径：",
                ["Status.UserChoice"] = "UserChoice：",
                ["Status.Ps5"] = "PowerShell 5.1：",
                ["Status.Ps7"] = "PowerShell 7+：",
                ["Status.Editor"] = "编辑器：",
                ["Status.EditorMissing"] = "未找到可用编辑器",
                ["Status.SettingsFile"] = "设置文件：",
                ["Status.ConfigFile"] = "配置文件：",
                ["Status.LogDirectory"] = "日志目录：",
                ["Status.CurrentLog"] = "当日日志：",
                ["Status.NotFound"] = "未找到",

                // 选择窗
                ["Picker.Title"] = "选择处理方式",
                ["Picker.Remember"] = "以后都这样处理（不再询问）",
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
