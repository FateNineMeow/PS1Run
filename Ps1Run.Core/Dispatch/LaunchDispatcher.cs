using System;
using System.IO;
using Ps1Run.Core.Editors;
using Ps1Run.Core.Infrastructure;
using Ps1Run.Core.PowerShell;
using Ps1Run.Core.Settings;

namespace Ps1Run.Core.Dispatch
{
    /// <summary>退出码约定（wiki 05 §8）。</summary>
    public static class ExitCodes
    {
        /// <summary>成功，或用户主动取消。</summary>
        public const int Success = 0;

        /// <summary>未预期的内部错误（骨架阶段补充，wiki 未定义该类）。</summary>
        public const int UnexpectedError = 1;

        /// <summary>参数非法。</summary>
        public const int InvalidArguments = 2;

        /// <summary>脚本文件不存在。</summary>
        public const int FileNotFound = 3;

        /// <summary>无人交互桌面、窗口创建失败或目标程序不可用。</summary>
        public const int NoInteractiveDesktop = 4;
    }

    /// <summary>最终动作（wiki 05 §3）。</summary>
    public enum LaunchAction
    {
        Run = 0,
        Edit = 1,
        Cancel = 2,
    }

    /// <summary>选择窗返回结果。Remember=true 表示用户勾选了"以后都这样处理"。</summary>
    public sealed class PickResult
    {
        public PickResult(LaunchAction action, bool remember)
        {
            Action = action;
            Remember = remember;
        }

        public LaunchAction Action { get; }

        public bool Remember { get; }

        public static PickResult Cancel { get; } = new PickResult(LaunchAction.Cancel, false);
    }

    /// <summary>选择窗输入。</summary>
    public sealed class PickRequest
    {
        public PickRequest(
            string scriptPath,
            PowerShellPreview? powerShell = null,
            AppSettings? settings = null)
        {
            ScriptPath = scriptPath;
            DisplayName = Path.GetFileName(scriptPath);
            PowerShell = powerShell;
            Settings = settings;
        }

        public string ScriptPath { get; }

        public string DisplayName { get; }

        /// <summary>执行时的解释器预判结果，仅用于界面标识，不改变分发行为。</summary>
        public PowerShellPreview? PowerShell { get; }

        /// <summary>当前运行设置，供选择窗检查执行程序/编辑器配置是否有效。</summary>
        public AppSettings? Settings { get; }
    }

    /// <summary>
    /// 选择窗展示用的解释器预判（只读）。
    /// 与 <see cref="PowerShellResolution"/> 不同：不弹人工指定框、不改设置、不启动进程。
    /// </summary>
    public sealed class PowerShellPreview
    {
        public PowerShellPreview(
            PowerShellEdition edition,
            string? executablePath,
            ScriptEncodingKind? encoding,
            PowerShellPolicy policy,
            bool isFallback,
            string? note)
        {
            Edition = edition;
            ExecutablePath = executablePath;
            Encoding = encoding;
            Policy = policy;
            IsFallback = isFallback;
            Note = note;
        }

        public PowerShellEdition Edition { get; }

        public string? ExecutablePath { get; }

        /// <summary>自动策略下的编码判型结果；固定策略时为 null。</summary>
        public ScriptEncodingKind? Encoding { get; }

        public PowerShellPolicy Policy { get; }

        /// <summary>自动模式下首选版本缺失、已改用另一版本。</summary>
        public bool IsFallback { get; }

        /// <summary>需要提醒用户的情况（降级、未找到等）。</summary>
        public string? Note { get; }

        public bool IsAvailable => !string.IsNullOrWhiteSpace(ExecutablePath);
    }

    /// <summary>
    /// 选择窗抽象。Core 不依赖 WPF，由 App 提供实现（wiki 03 §4）。
    /// 返回 null 表示本环境无法弹窗（无交互桌面）。
    /// </summary>
    public interface IActionPicker
    {
        PickResult? Pick(PickRequest request);
    }

    /// <summary>分发结论。</summary>
    public sealed class DispatchDecision
    {
        public DispatchDecision(LaunchAction action, string? scriptPath, int exitCode, string reason)
        {
            Action = action;
            ScriptPath = scriptPath;
            ExitCode = exitCode;
            Reason = reason;
        }

        public LaunchAction Action { get; }

        public string? ScriptPath { get; }

        public int ExitCode { get; }

        public string Reason { get; }
    }

    /// <summary>执行程序解析结果（含自动模式的判型依据）。</summary>
    public sealed class PowerShellResolution
    {
        public PowerShellResolution(
            PowerShellTarget target,
            PowerShellEdition edition,
            ScriptEncodingKind? encoding,
            string? note = null)
        {
            Target = target;
            Edition = edition;
            Encoding = encoding;
            Note = note;
        }

        public PowerShellTarget Target { get; }

        public PowerShellEdition Edition { get; }

        /// <summary>自动模式下的编码判型结果；固定策略时为 null。</summary>
        public ScriptEncodingKind? Encoding { get; }

        /// <summary>降级或人工指定等需要让用户/日志知道的说明。</summary>
        public string? Note { get; }
    }

    /// <summary>
    /// 动作决策（wiki 05 §3，02 F2.2）。
    /// 运行方式只决定"执行还是编辑"，不决定用哪个解释器/编辑器。
    /// </summary>
    public sealed class LaunchDispatcher
    {
        private AppSettings _settings;
        private readonly IActionPicker? _picker;
        private readonly IExecutablePicker? _executablePicker;
        private readonly IFileSystem _fileSystem;
        private readonly ScriptEncodingDetector _encodingDetector;
        private readonly PowerShellLocator _powerShellLocator;
        private readonly EditorLocator _editorLocator;
        private readonly Action<LaunchAction>? _onRemember;
        private readonly Action<PowerShellEdition, string>? _onPowerShellSelected;

        public LaunchDispatcher(
            AppSettings settings,
            IActionPicker? picker = null,
            IFileSystem? fileSystem = null,
            Action<LaunchAction>? onRemember = null,
            IExecutablePicker? executablePicker = null,
            Action<PowerShellEdition, string>? onPowerShellSelected = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _picker = picker;
            _executablePicker = executablePicker;
            _fileSystem = fileSystem ?? PhysicalFileSystem.Instance;
            _encodingDetector = new ScriptEncodingDetector(_fileSystem);
            _powerShellLocator = new PowerShellLocator(_fileSystem);
            _editorLocator = new EditorLocator(_fileSystem);
            _onRemember = onRemember;
            _onPowerShellSelected = onPowerShellSelected;
        }

        public DispatchDecision Decide(CommandLineOptions options)
        {
            if (options.Errors.Count > 0)
            {
                return new DispatchDecision(
                    LaunchAction.Cancel,
                    null,
                    ExitCodes.InvalidArguments,
                    string.Join("；", options.Errors));
            }

            if (options.ShowHelp)
            {
                return new DispatchDecision(LaunchAction.Cancel, null, ExitCodes.Success, "显示帮助");
            }

            if (options.Intent == LaunchIntent.None || options.Intent == LaunchIntent.Settings)
            {
                return new DispatchDecision(LaunchAction.Cancel, null, ExitCodes.Success, "显示主界面");
            }

            var scriptPath = options.ScriptPath;
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                return new DispatchDecision(
                    LaunchAction.Cancel,
                    null,
                    ExitCodes.InvalidArguments,
                    "缺少脚本路径");
            }

            if (!_fileSystem.FileExists(scriptPath!))
            {
                return new DispatchDecision(
                    LaunchAction.Cancel,
                    scriptPath,
                    ExitCodes.FileNotFound,
                    "文件不存在：" + scriptPath);
            }

            switch (options.Intent)
            {
                case LaunchIntent.Run:
                    return new DispatchDecision(LaunchAction.Run, scriptPath, ExitCodes.Success, "显式 --run");
                case LaunchIntent.Edit:
                    return new DispatchDecision(LaunchAction.Edit, scriptPath, ExitCodes.Success, "显式 --edit");
            }

            // --ask 强制弹窗；--open 按设置
            var mode = options.Intent == LaunchIntent.Ask ? RunMode.Ask : _settings.RunMode;
            switch (mode)
            {
                case RunMode.Run:
                    return new DispatchDecision(LaunchAction.Run, scriptPath, ExitCodes.Success, "运行方式=执行");
                case RunMode.Edit:
                    return new DispatchDecision(LaunchAction.Edit, scriptPath, ExitCodes.Success, "运行方式=编辑");
                default:
                    return AskUser(scriptPath!);
            }
        }

        private DispatchDecision AskUser(string scriptPath)
        {
            if (_picker is null)
            {
                return new DispatchDecision(
                    LaunchAction.Cancel,
                    scriptPath,
                    ExitCodes.NoInteractiveDesktop,
                    "缺少选择窗实现（无交互桌面）");
            }

            var result = _picker.Pick(new PickRequest(scriptPath, PreviewPowerShell(scriptPath), _settings));
            if (result is null)
            {
                return new DispatchDecision(
                    LaunchAction.Cancel,
                    scriptPath,
                    ExitCodes.NoInteractiveDesktop,
                    "选择窗创建失败");
            }

            if (result.Action == LaunchAction.Cancel)
            {
                DiagnosticsLog.Info("用户在 Ask 弹窗选择取消, script=" + scriptPath);
                return new DispatchDecision(LaunchAction.Cancel, scriptPath, ExitCodes.Success, "用户取消");
            }

            var reason = result.Action == LaunchAction.Run ? "用户选择执行" : "用户选择编辑";
            if (result.Remember)
            {
                // "以后都这样处理"：交给宿主写设置（wiki 05 §5）
                _onRemember?.Invoke(result.Action);
                reason += "（记住选择）";
            }

            return new DispatchDecision(result.Action, scriptPath, ExitCodes.Success, reason);
        }

        /// <summary>
        /// 解析执行程序（wiki 07 §5 / §6）。
        /// 固定策略缺失 → 人工指定；自动策略缺失 → 降级到另一侧并给出说明；两侧都缺 → 返回 null。
        /// </summary>
        public PowerShellResolution? ResolvePowerShell(string scriptPath)
        {
            ScriptEncodingKind? encoding = null;
            PowerShellEdition edition;

            switch (_settings.PowerShellPolicy)
            {
                case PowerShellPolicy.Ps5:
                    edition = PowerShellEdition.Ps5;
                    break;
                case PowerShellPolicy.Ps7:
                    edition = PowerShellEdition.Ps7;
                    break;
                default:
                    encoding = SafeDetectEncoding(scriptPath);
                    edition = encoding == ScriptEncodingKind.Ansi
                        ? PowerShellEdition.Ps5
                        : PowerShellEdition.Ps7;
                    break;
            }

            var target = Locate(edition);
            if (target is null)
            {
                if (_settings.PowerShellPolicy == PowerShellPolicy.Auto)
                {
                    // 自动模式允许降级：能用但可能乱码，必须给出明确提示（wiki 07 §5）
                    var fallback = edition == PowerShellEdition.Ps5
                        ? PowerShellEdition.Ps7
                        : PowerShellEdition.Ps5;
                    var fallbackTarget = Locate(fallback);
                    if (fallbackTarget is not null)
                    {
                        var wanted = edition == PowerShellEdition.Ps5 ? "PS5" : "PS7";
                        var actual = fallback == PowerShellEdition.Ps5 ? "PS5" : "PS7";
                        return new PowerShellResolution(
                            fallbackTarget,
                            fallback,
                            encoding,
                            "按编码应选 " + wanted + "，但未找到；已降级为 " + actual + "，中文可能乱码。");
                    }

                    return null;
                }

                // 固定策略缺失：必须让用户人工指定，不能静默换成另一个版本
                return AskForPowerShell(edition, encoding);
            }

            return new PowerShellResolution(target, edition, encoding);
        }

        /// <summary>
        /// 只读预判执行程序，供选择窗标识"将用 PowerShell 5 还是 7"。
        /// 不弹人工指定框、不改设置、不启动进程，因此可在用户尚未决定执行前安全调用。
        /// 选择规则与 <see cref="ResolvePowerShell"/> 保持一致（含自动模式的降级判断）。
        /// </summary>
        public PowerShellPreview PreviewPowerShell(string scriptPath)
        {
            ScriptEncodingKind? encoding = null;
            PowerShellEdition edition;

            switch (_settings.PowerShellPolicy)
            {
                case PowerShellPolicy.Ps5:
                    edition = PowerShellEdition.Ps5;
                    break;
                case PowerShellPolicy.Ps7:
                    edition = PowerShellEdition.Ps7;
                    break;
                default:
                    encoding = SafeDetectEncoding(scriptPath);
                    edition = encoding == ScriptEncodingKind.Ansi
                        ? PowerShellEdition.Ps5
                        : PowerShellEdition.Ps7;
                    break;
            }

            var target = Locate(edition);
            if (target is not null)
            {
                return new PowerShellPreview(
                    edition,
                    target.ExecutablePath,
                    encoding,
                    _settings.PowerShellPolicy,
                    false,
                    null);
            }

            if (_settings.PowerShellPolicy == PowerShellPolicy.Auto)
            {
                var fallback = edition == PowerShellEdition.Ps5
                    ? PowerShellEdition.Ps7
                    : PowerShellEdition.Ps5;
                var fallbackTarget = Locate(fallback);
                if (fallbackTarget is not null)
                {
                    var wanted = edition == PowerShellEdition.Ps5 ? "PS5" : "PS7";
                    var actual = fallback == PowerShellEdition.Ps5 ? "PS5" : "PS7";
                    return new PowerShellPreview(
                        fallback,
                        fallbackTarget.ExecutablePath,
                        encoding,
                        _settings.PowerShellPolicy,
                        true,
                        "按编码应选 " + wanted + "，但未找到；执行时将降级为 " + actual + "，中文可能乱码。");
                }

                return new PowerShellPreview(
                    edition,
                    null,
                    encoding,
                    _settings.PowerShellPolicy,
                    true,
                    "未找到可用的 PowerShell，执行时将无法启动。");
            }

            return new PowerShellPreview(
                edition,
                null,
                encoding,
                _settings.PowerShellPolicy,
                false,
                "未找到 " + EditionName(edition) + "，执行时将提示你指定路径。");
        }

        private PowerShellResolution? AskForPowerShell(PowerShellEdition edition, ScriptEncodingKind? encoding)
        {
            if (_executablePicker is null)
            {
                DiagnosticsLog.Error("未找到 " + EditionName(edition) + "，且当前环境无法弹出文件选择框。");
                return null;
            }

            DiagnosticsLog.Info("未找到 " + EditionName(edition) + "，请求用户人工指定。");
            var chosen = _executablePicker.PickPowerShell(edition);
            if (string.IsNullOrWhiteSpace(chosen) || !_fileSystem.FileExists(chosen!))
            {
                DiagnosticsLog.Error("用户取消指定，或所选路径无效。");
                return null;
            }

            // 更新内存中的设置快照，使本次立即生效；持久化由宿主回调完成
            _settings = edition == PowerShellEdition.Ps5
                ? _settings with { CustomPs5Path = chosen }
                : _settings with { CustomPs7Path = chosen };
            _onPowerShellSelected?.Invoke(edition, chosen!);

            var target = new PowerShellTarget(edition, chosen!, "UserSelected");
            return new PowerShellResolution(
                target,
                edition,
                encoding,
                "已使用人工指定的 " + EditionName(edition) + " 路径，并写入设置。");
        }

        private static string EditionName(PowerShellEdition edition) =>
            edition == PowerShellEdition.Ps5 ? "Windows PowerShell 5.1" : "PowerShell 7+";

        public EditorDefinition? ResolveEditor()
        {
            return _editorLocator.Resolve(_settings.CustomEditorPath);
        }

        private PowerShellTarget? Locate(PowerShellEdition edition)
        {
            var custom = edition == PowerShellEdition.Ps5
                ? _settings.CustomPs5Path
                : _settings.CustomPs7Path;
            return _powerShellLocator.Locate(edition, custom);
        }

        private ScriptEncodingKind SafeDetectEncoding(string scriptPath)
        {
            try
            {
                return _encodingDetector.Detect(scriptPath);
            }
            catch (IOException)
            {
                return ScriptEncodingKind.Unknown;
            }
            catch (UnauthorizedAccessException)
            {
                return ScriptEncodingKind.Unknown;
            }
        }
    }
}
