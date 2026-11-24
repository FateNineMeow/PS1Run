using System.Windows;
using System.Windows.Input;
using Ps1Run.App.Services;
using Ps1Run.App.ViewModels;
using Ps1Run.Core.Dispatch;

namespace Ps1Run.App.Views
{
    /// <summary>
    /// "选择"模式弹窗（wiki 05 §5）。
    /// 独立小窗、不占任务栏、定位到鼠标所在显示器居中；
    /// Esc / 关闭按钮 / 取消三者行为一致，且不写任何设置。
    /// </summary>
    public partial class PickerWindow : Window
    {
        private readonly PickerViewModel _viewModel;

        public PickerWindow()
            : this(new PickRequest(string.Empty))
        {
        }

        public PickerWindow(string scriptPath)
            : this(new PickRequest(scriptPath))
        {
        }

        public PickerWindow(PickRequest request)
        {
            InitializeComponent();
            _viewModel = new PickerViewModel(request);
            DataContext = _viewModel;
            Loaded += OnLoaded;
        }

        /// <summary>ShowDialog 结束后的结果；null 表示取消/未选择。</summary>
        public PickResult? Result { get; private set; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PickerPlacement.CenterOnCursorMonitor(this);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // E = 编辑（Enter / Esc 由 IsDefault / IsCancel 处理）
            if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                Complete(LaunchAction.Edit);
            }
        }

        private void OnRunClick(object sender, RoutedEventArgs e) => Complete(LaunchAction.Run);

        private void OnEditClick(object sender, RoutedEventArgs e) => Complete(LaunchAction.Edit);

        private void OnCancelClick(object sender, RoutedEventArgs e) => Complete(LaunchAction.Cancel);

        private void Complete(LaunchAction action)
        {
            Result = _viewModel.ToResult(action);
            DialogResult = action != LaunchAction.Cancel;
        }
    }
}
