using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Ps1Run.App.Localization;
using Ps1Run.App.ViewModels;
using Ps1Run.Core.PowerShell;
using Ps1Run.Core.Rules;

namespace Ps1Run.App.Views
{
    /// <summary>主界面（wiki 02 F2.7）。骨架阶段只做状态展示与设置入口。</summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += OnLoaded;
        }

        /// <summary>小屏幕（如 1366×768）上不让窗口超出工作区；内容已在 ScrollViewer 内可滚动。</summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var area = SystemParameters.WorkArea;
            if (Height > area.Height - 32)
            {
                Height = System.Math.Max(MinHeight, area.Height - 32);
            }

            if (Width > area.Width - 32)
            {
                Width = System.Math.Max(MinWidth, area.Width - 32);
            }
        }

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldVm)
            {
                oldVm.LanguageRestartRequested -= OnLanguageRestartRequested;
            }

            if (e.NewValue is MainViewModel newVm)
            {
                newVm.LanguageRestartRequested += OnLanguageRestartRequested;
            }
        }

        /// <summary>语言切换后征询是否重启；界面不做即时换语言，提示中告知重启后将使用的新语言。</summary>
        private void OnLanguageRestartRequested(object? sender, EventArgs e)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            var message = string.Format(
                CultureInfo.CurrentCulture,
                LocalizationManager.Loc("Dialog.RestartForLanguage"),
                vm.CurrentLanguageDisplayName);

            if (MessageBox.Show(this, message, LocalizationManager.Loc("App.Title"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            if (!TryRestartApplication())
            {
                MessageBox.Show(this, LocalizationManager.Loc("Dialog.RestartFailed"),
                    LocalizationManager.Loc("App.Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>拉起当前 exe 的新实例后关闭本进程；启动失败时保留当前窗口。</summary>
        private static bool TryRestartApplication()
        {
            try
            {
                using var current = Process.GetCurrentProcess();
                var exe = current.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exe))
                {
                    return false;
                }

                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                Application.Current.Shutdown();
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e) => ViewModel?.Save();

        private void OnRegisterClick(object sender, RoutedEventArgs e) =>
            ViewModel?.RegisterAssociation();

        private void OnUnregisterClick(object sender, RoutedEventArgs e) =>
            ViewModel?.UnregisterAssociation();

        private void OnAutoDetectClick(object sender, RoutedEventArgs e) => ViewModel?.AutoDetect();

        private void OnPickEditorClick(object sender, RoutedEventArgs e) => ViewModel?.PickEditor();

        private void OnOpenDefaultAppsClick(object sender, RoutedEventArgs e) =>
            ViewModel?.OpenDefaultAppsSettings();

        private void OnPickPs5Click(object sender, RoutedEventArgs e) =>
            ViewModel?.PickPowerShell(PowerShellEdition.Ps5);

        private void OnPickPs7Click(object sender, RoutedEventArgs e) =>
            ViewModel?.PickPowerShell(PowerShellEdition.Ps7);

        private void OnOpenWithClick(object sender, RoutedEventArgs e) =>
            ViewModel?.ShowOpenWithDialog();

        private void OnRegisterMachineClick(object sender, RoutedEventArgs e) =>
            ViewModel?.RegisterAssociationMachine();

        private void OnUnregisterMachineClick(object sender, RoutedEventArgs e) =>
            ViewModel?.UnregisterAssociationMachine();

        private void OnOpenLogFolderClick(object sender, RoutedEventArgs e) =>
            ViewModel?.OpenLogFolder();

        private void OnRebuildLanguagePackClick(object sender, RoutedEventArgs e) =>
            ViewModel?.RebuildLanguagePack();

        private void OnOpenCurrentLogClick(object sender, RoutedEventArgs e) =>
            ViewModel?.OpenCurrentLog();

        private void OnCopyRecentLogsClick(object sender, RoutedEventArgs e) =>
            CopyToClipboard(ViewModel?.BuildRecentLogReport());

        private void OnPruneNowClick(object sender, RoutedEventArgs e) =>
            ViewModel?.PruneLogsNow();

        private void OnOpenConfigClick(object sender, RoutedEventArgs e) =>
            ViewModel?.OpenConfigFile();

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        private void OnRuleRefreshClick(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm is null)
            {
                return;
            }

            if (vm.HasUnsavedRules
                && MessageBox.Show(this, LocalizationManager.Loc("Rule.RefreshConfirm"),
                    LocalizationManager.Loc("App.Title"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            vm.RefreshRules();
        }

        private void OnRuleAddClick(object sender, RoutedEventArgs e)
        {
            if (ShowRuleEditor(null, out var rule))
            {
                ViewModel?.AddRule(rule!);
            }
        }

        private void OnRuleEditClick(object sender, RoutedEventArgs e)
        {
            var selected = ViewModel?.SelectedRule;
            if (selected is null)
            {
                return;
            }

            if (ShowRuleEditor(selected, out var updated))
            {
                ViewModel?.UpdateRule(selected, updated!);
            }
        }

        private void OnRuleRowDoubleClick(object sender, MouseButtonEventArgs e) => OnRuleEditClick(sender, e);

        private void OnRuleDeleteClick(object sender, RoutedEventArgs e)
        {
            var selected = ViewModel?.SelectedRule;
            if (selected is null)
            {
                return;
            }

            if (MessageBox.Show(this,
                    LocalizationManager.Loc("Rule.DeleteConfirm") + Environment.NewLine + selected.Path,
                    LocalizationManager.Loc("App.Title"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ViewModel?.DeleteRule(selected);
            }
        }

        private void OnRuleSaveClick(object sender, RoutedEventArgs e) => ViewModel?.SaveRules();

        private bool ShowRuleEditor(ScriptRule? editing, out ScriptRule? result)
        {
            result = null;
            var vm = ViewModel;
            if (vm is null)
            {
                return false;
            }

            var window = new RuleEditorWindow(vm.CurrentSettings, vm.AllRules, editing)
            {
                Owner = this,
            };

            if (window.ShowDialog() == true && window.Rule is not null)
            {
                result = window.Rule;
                return true;
            }

            return false;
        }

        private static void CopyToClipboard(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            try
            {
                Clipboard.SetText(text);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // 剪贴板被其它进程占用时忽略
            }
        }
    }
}
