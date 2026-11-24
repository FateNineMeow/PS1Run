using System.Windows;
using Ps1Run.App.ViewModels;
using Ps1Run.Core.PowerShell;

namespace Ps1Run.App.Views
{
    /// <summary>主界面（wiki 02 F2.7）。骨架阶段只做状态展示与设置入口。</summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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

        private void OnSaveClick(object sender, RoutedEventArgs e) => ViewModel?.Save();

        private void OnRegisterClick(object sender, RoutedEventArgs e) =>
            ViewModel?.RegisterAssociation();

        private void OnUnregisterClick(object sender, RoutedEventArgs e) =>
            ViewModel?.UnregisterAssociation();

        private void OnRefreshClick(object sender, RoutedEventArgs e) => ViewModel?.RefreshStatus();

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

        private void OnCopyDiagnosticsClick(object sender, RoutedEventArgs e) =>
            CopyToClipboard(ViewModel?.StatusText);

        private void OnOpenLogFolderClick(object sender, RoutedEventArgs e) =>
            ViewModel?.OpenLogFolder();

        private void OnOpenCurrentLogClick(object sender, RoutedEventArgs e) =>
            ViewModel?.OpenCurrentLog();

        private void OnCopyRecentLogsClick(object sender, RoutedEventArgs e) =>
            CopyToClipboard(ViewModel?.BuildRecentLogReport());

        private void OnPruneNowClick(object sender, RoutedEventArgs e) =>
            ViewModel?.PruneLogsNow();

        private void OnOpenConfigClick(object sender, RoutedEventArgs e) =>
            ViewModel?.OpenConfigFile();

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
