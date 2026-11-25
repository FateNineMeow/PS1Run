using System;
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

            // 先按主显示器居中，保证 ShowDialog 时窗口已经位于屏幕中央，
            // 不会先闪现在左上角再被 Loaded 事件挪走。
            PickerPlacement.CenterOnPrimaryMonitor(this);

            _viewModel = new PickerViewModel(request);
            DataContext = _viewModel;
            SourceInitialized += OnSourceInitialized;
        }

        /// <summary>ShowDialog 结束后的结果；null 表示取消/未选择。</summary>
        public PickResult? Result { get; private set; }

        // SourceInitialized 早于首次绘制：此时已经有 HWND 可做 DPI 换算，
        // 再把窗口挪到鼠标所在显示器正中央，过程对用户不可见。
        private void OnSourceInitialized(object? sender, EventArgs e)
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
