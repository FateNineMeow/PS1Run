using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Ps1Run.App.Localization;
using Ps1Run.App.Services;
using Ps1Run.App.ViewModels;
using Ps1Run.App.Views;
using Ps1Run.Core;
using Ps1Run.Core.Associations;
using Ps1Run.Core.Configuration;
using Ps1Run.Core.Dispatch;
using Ps1Run.Core.Infrastructure;
using Ps1Run.Core.PowerShell;
using Ps1Run.Core.Settings;

namespace Ps1Run.App
{
    /// <summary>
    /// 应用入口与生命周期（wiki 05 §3、§7）。
    /// 有脚本参数 → 无窗口直通；无参数 → 主界面。
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 必须最先执行：net11 下注册代码页提供程序（wiki 07 §2）
            CoreBootstrap.Initialize();
            base.OnStartup(e);

            // 程序配置（config.json）：首次运行自动生成，含日志天数 / 主题 / 语言
            var configStore = new ConfigStore();
            var config = configStore.Load();

            // 日志会话（F6.1）：记录 argv、进程上下文；当天第一次运行时清理过期日志
            DiagnosticsLog.BeginSession(
                "Ps1Run.App",
                string.Join(" ", e.Args ?? Array.Empty<string>()),
                config.LogDays);

            // 界面语言与主题（F7.x）：必须在创建窗口之前生效
            LocalizationManager.Instance.Load(config.Language);
            ThemeManager.Apply(config.Theme);

            DiagnosticsLog.Info(
                "程序配置：file=" + configStore.FilePath
                + " version=" + AppInfo.Describe()
                + " logDays=" + config.LogDays.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " theme=" + config.Theme
                + " language=" + config.Language);

            // 全局异常兜底（F6.3）：任何未处理异常都转成中文提示 + 日志
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            var options = CommandLineOptions.Parse(e.Args);
            var store = new SettingsStore();
            var settings = store.Load();

            if (options.ShowHelp)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                ShowUsage();
                Shutdown(ExitCodes.Success);
                return;
            }

            // 已提升的进程执行机器级注册/注销（F1.9）
            if (options.Intent == LaunchIntent.RegisterMachine
                || options.Intent == LaunchIntent.UnregisterMachine)
            {
                ExecuteMachineAssociation(options.Intent);
                Shutdown(ExitCodes.Success);
                return;
            }

            // 无参数或 --settings：显示主界面
            if ((options.Intent == LaunchIntent.None || options.Intent == LaunchIntent.Settings)
                && options.Errors.Count == 0)
            {
                ShowMainWindow(store, settings, configStore, config);
                return;
            }

            // 有参数：直通模式，全程不创建主窗口
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var exitCode = ExecuteHeadless(options, store, settings);
            Shutdown(exitCode);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DiagnosticsLog.EndSession(e.ApplicationExitCode);
            base.OnExit(e);
        }

        private void ShowMainWindow(
            SettingsStore store,
            AppSettings settings,
            ConfigStore configStore,
            AppConfig config)
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            var window = new Views.MainWindow
            {
                DataContext = new MainViewModel(store, settings, configStore, config),
            };

            MainWindow = window;
            window.Show();
        }

        private int ExecuteHeadless(
            CommandLineOptions options,
            SettingsStore store,
            AppSettings settings)
        {
            try
            {
                var picker = new WpfActionPicker();
                var executablePicker = new WpfExecutablePicker();
                var dispatcher = new LaunchDispatcher(
                    settings,
                    picker,
                    onRemember: action => RememberChoice(store, action),
                    executablePicker: executablePicker,
                    onPowerShellSelected: (edition, path) => RememberPowerShell(store, edition, path));

                var decision = dispatcher.Decide(options);
                DiagnosticsLog.Info(
                    "分发：intent=" + options.Intent
                    + " path=" + (options.ScriptPath ?? "<无>")
                    + " → " + decision.Action
                    + "（" + decision.Reason + "）");

                if (decision.ExitCode != ExitCodes.Success)
                {
                    DiagnosticsLog.Error("分发失败：" + decision.Reason);
                    ReportError(decision.Reason);
                    return decision.ExitCode;
                }

                if (decision.Action == LaunchAction.Cancel)
                {
                    // 取消：不启动进程、不改设置（wiki 05 §6）
                    return ExitCodes.Success;
                }

                return decision.Action == LaunchAction.Run
                    ? RunScript(dispatcher, decision.ScriptPath!)
                    : OpenInEditor(dispatcher, decision.ScriptPath!);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Error("未处理异常：" + ex);
                ReportError("发生未预期错误：" + ex.Message);
                return ExitCodes.UnexpectedError;
            }
        }

        private static int RunScript(LaunchDispatcher dispatcher, string scriptPath)
        {
            var resolution = dispatcher.ResolvePowerShell(scriptPath);
            if (resolution is null)
            {
                DiagnosticsLog.Error("未找到可用的 PowerShell（PS5 / PS7 都缺失）");
                ReportError("未找到可用的 PowerShell，请在设置中指定解释器路径。");
                return ExitCodes.NoInteractiveDesktop;
            }

            DiagnosticsLog.Info(
                "执行：edition=" + resolution.Edition
                + " encoding=" + (resolution.Encoding?.ToString() ?? "<固定策略>")
                + " exe=" + resolution.Target.ExecutablePath
                + (string.IsNullOrEmpty(resolution.Note) ? string.Empty : " note=" + resolution.Note));

            var result = new ProcessLauncher().StartPowerShell(resolution.Target, scriptPath);
            if (result.Started)
            {
                return ExitCodes.Success;
            }

            DiagnosticsLog.Error("启动 PowerShell 失败：" + result.Error);
            ReportError("启动 PowerShell 失败：" + result.Error);
            return ExitCodes.UnexpectedError;
        }

        private static int OpenInEditor(LaunchDispatcher dispatcher, string scriptPath)
        {
            var editor = dispatcher.ResolveEditor();
            if (editor is null)
            {
                DiagnosticsLog.Error("未找到可用编辑器（连记事本兜底都缺失）");
                ReportError("未找到可用编辑器，请在设置中指定。");
                return ExitCodes.NoInteractiveDesktop;
            }

            DiagnosticsLog.Info("编辑：editor=" + editor.DisplayName + " source=" + editor.Source);
            var result = new ProcessLauncher().StartEditor(editor, scriptPath);
            if (result.Started)
            {
                return ExitCodes.Success;
            }

            DiagnosticsLog.Error("启动编辑器失败：" + result.Error);
            ReportError("启动编辑器失败：" + result.Error);
            return ExitCodes.UnexpectedError;
        }

        private static void RememberChoice(SettingsStore store, LaunchAction action)
        {
            if (action == LaunchAction.Cancel)
            {
                return;
            }

            try
            {
                var current = store.Load();
                var updated = current with
                {
                    RunMode = action == LaunchAction.Run ? RunMode.Run : RunMode.Edit,
                };
                store.Save(updated);
                DiagnosticsLog.Info("已记住选择：RunMode=" + updated.RunMode);
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Error("保存设置失败：" + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Error("保存设置失败：" + ex.Message);
            }
        }

        private static void RememberPowerShell(SettingsStore store, PowerShellEdition edition, string path)
        {
            try
            {
                var current = store.Load();
                var updated = edition == PowerShellEdition.Ps5
                    ? current with { CustomPs5Path = path }
                    : current with { CustomPs7Path = path };
                store.Save(updated);
                DiagnosticsLog.Info("已记住执行程序：" + edition + " -> " + path);
            }
            catch (IOException ex)
            {
                DiagnosticsLog.Error("保存执行程序路径失败：" + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticsLog.Error("保存执行程序路径失败：" + ex.Message);
            }
        }

        private static void ExecuteMachineAssociation(LaunchIntent intent)
        {
            try
            {
                var service = new AssociationService();
                var result = intent == LaunchIntent.RegisterMachine
                    ? service.Register(AssociationScope.Machine)
                    : service.Unregister(AssociationScope.Machine);
                DiagnosticsLog.Info("机器级关联操作：" + intent + " -> " + result.Message);
                ReportError(result.Message);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Error("机器级关联操作失败：" + ex);
                ReportError("机器级关联操作失败：" + ex.Message);
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            DiagnosticsLog.Error("未处理异常（全局）：" + e.Exception);
            ReportError("发生未预期错误：" + e.Exception.Message);
            e.Handled = true;
        }

        private static void ShowUsage()
        {
            const string usage =
                "用法：\r\n"
                + "  Ps1Run.exe <脚本.ps1>            按设置的运行方式处理\r\n"
                + "  Ps1Run.exe --open <脚本.ps1>     按设置分发（选择模式会弹窗）\r\n"
                + "  Ps1Run.exe --run <脚本.ps1>      强制执行\r\n"
                + "  Ps1Run.exe --edit <脚本.ps1>     强制编辑\r\n"
                + "  Ps1Run.exe --ask <脚本.ps1>      本次强制弹窗选择\r\n"
                + "  Ps1Run.exe --settings            打开设置界面\r\n"
                + "  Ps1Run.exe --register-machine     注册机器级关联（需管理员）\r\n"
                + "  Ps1Run.exe --unregister-machine   注销机器级关联（需管理员）\r\n"
                + "  Ps1Run.exe                       打开主界面\r\n";

            try
            {
                MessageBox.Show(usage, "PS1Run 帮助", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static void ReportError(string message)
        {
            try
            {
                MessageBox.Show(message, "PS1Run", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (InvalidOperationException)
            {
                // 无交互桌面时忽略，退出码已表达结果
            }
        }
    }
}
